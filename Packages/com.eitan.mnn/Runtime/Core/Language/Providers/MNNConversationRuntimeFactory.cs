using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    public interface IMNNConversationRealtimeProvider : IMNNConversationProvider
    {
        IMNNRealtimeSessionRuntime CreateRuntime(IMNNConversationContext context, MNNRealtimeOutputPipeline output);
    }

    /// <summary>Registrations describe tasks without loading models. Route composition acquires only required stages.</summary>
    public sealed class MNNConversationRegistrations
    {
        public MNNConversationStageRegistration Model, Tts, Asr, Vad, Turn, Transcription;
        public bool AllowBufferedSpeech, TurnRequiresTranscript;
        internal MNNConversationRegistrations Snapshot() => (MNNConversationRegistrations)MemberwiseClone();
    }

    /// <summary>Composes route stages, submissions, diagnostics and lazy transcription.
    /// One owner serializes creation and submissions through MNNRealtimeSession. Stop the session before
    /// final ReleaseAsync, which releases optional transcription and failed creation resources.</summary>
    public sealed class MNNConversationRuntimeFactory : IMNNRealtimeRuntimeFactory
    {
        private readonly IMNNConversationContext _context;
        private readonly MNNConversationOptions _options;
        private readonly MNNConversationRegistrations _stages;
        private readonly MNNConversationTranscriptionService _transcription;
        private readonly List<MNNOpenAIMessage> _history = new List<MNNOpenAIMessage>();
        private readonly List<MNNConversationProviderCleanup> _failedCreationCleanup = new List<MNNConversationProviderCleanup>();
        private readonly Func<IMNNConversationProvider, IMNNConversationContext, MNNRealtimeOutputPipeline, IMNNRealtimeSessionRuntime> _createRealtime;
        private bool _released;

        public MNNConversationRuntime Active { get; private set; }
        public MNNConversationRuntimeDiagnostics Diagnostics { get; }
        public MNNConversationTelemetry TurnTelemetry => Active?.TurnTelemetry;

        public MNNConversationRuntimeFactory(IMNNConversationContext context, MNNConversationOptions options,
            MNNConversationRegistrations stages, MNNConversationRuntimeDiagnostics diagnostics = null,
            Func<IMNNConversationProvider, IMNNConversationContext, MNNRealtimeOutputPipeline, IMNNRealtimeSessionRuntime> createRealtime = null)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _options = (options ?? throw new ArgumentNullException(nameof(options))).Snapshot();
            _stages = (stages ?? throw new ArgumentNullException(nameof(stages))).Snapshot();
            Diagnostics = diagnostics ?? new MNNConversationRuntimeDiagnostics();
            _createRealtime = createRealtime;
            _context.RealtimeInactivitySeconds = _options.realtimeInactivitySeconds;
            _transcription = new MNNConversationTranscriptionService(_stages.Transcription ?? _stages.Asr, Diagnostics);
        }

        public MNNRealtimeFallbackRuntime CreateFallback(MNNRealtimeOutputPipeline output, MNNRealtimeCapabilities device,
            IReadOnlyCollection<MNNRealtimeRoute> excludedRoutes = null, bool requireMeasuredSpeech = true)
        {
            RequireActive();
            return new MNNRealtimeFallbackRuntime(this, output,
                _stages.Model?.Capabilities ?? throw new InvalidOperationException("Register a conversation model."),
                device, _stages.Tts?.Capabilities, _stages.Asr?.Capabilities, _stages.Vad?.Capabilities,
                excludedRoutes, requireMeasuredSpeech, _stages.AllowBufferedSpeech, _options.speechOutput);
        }

        public async Task ReleaseAsync()
        {
            _released = true;
            List<Exception> failures = null;
            try
            {
                await _transcription.ReleaseAsync().ConfigureAwait(false);
            }
            catch (Exception error)
            {
                (failures ?? (failures = new List<Exception>())).Add(error);
            }
            for (int i = _failedCreationCleanup.Count - 1; i >= 0; --i)
            {
                try
                {
                    _failedCreationCleanup[i].Dispose();
                    _failedCreationCleanup.RemoveAt(i);
                }
                catch (Exception error)
                {
                    (failures ?? (failures = new List<Exception>())).Add(error);
                }
            }
            if (failures != null) throw new AggregateException("Conversation factory cleanup failed; failed owners remain available for retry.", failures);
        }

        public Task<IMNNRealtimeSessionRuntime> CreateAsync(MNNRealtimeReport report, MNNRealtimeOutputPipeline output, CancellationToken token)
        {
            RequireActive();
            token.ThrowIfCancellationRequested();
            if (report == null) throw new ArgumentNullException(nameof(report));
            Active = null;
            var providers = new MNNConversationProviders();
            var registrations = new Dictionary<IMNNConversationProvider, MNNConversationStageRegistration>(MNNConversationProviders.Identity);
            var plan = MNNConversationStagePlan.ForRoute(_options, report.Route, _stages.AllowBufferedSpeech,
                _stages.Vad?.Create != null, _stages.Turn?.Create != null, _stages.TurnRequiresTranscript);
            IMNNConversationProvider Acquire(string name, MNNConversationStageRegistration registration)
            {
                var value = Diagnostics.Acquire(name, registration);
                if (value != null) registrations[value] = registration;
                return value;
            }
            try
            {
                providers.Model = Acquire("model", _stages.Model);
                if (report.Route == MNNRealtimeRoute.NativeDuplexSpeech ||
                    report.Route == MNNRealtimeRoute.EndToEndSpeech && !(providers.Model is IMNNConversationSpeechModelProvider) ||
                    report.Route == MNNRealtimeRoute.NativeDuplexTextToSpeech)
                {
                    if (_options.filterVoiceInput || _options.endRealtimeOnExitIntent)
                        throw new MNNRealtimeRouteUnavailableException(report.Route,
                            "Voice validation and model-based exit intent require the built-in turn-based runtime. Select a cascaded/buffered route, or explicitly disable voice validation and exit intent for a provider-owned continuous session.");
                    if (_context.RequiresUtteranceSpeakerSelection)
                        throw new MNNRealtimeRouteUnavailableException(report.Route, "Utterance speaker selection requires the built-in turn runtime.");
                    IMNNRealtimeSessionRuntime runtime;
                    if (_createRealtime != null) runtime = _createRealtime(providers.Model, _context, output);
                    else if (providers.Model is IMNNConversationRealtimeProvider realtime) runtime = realtime.CreateRuntime(_context, output);
                    else throw new MNNRealtimeRouteUnavailableException(report.Route, "The model provider does not implement a realtime session.");
                    return Task.FromResult<IMNNRealtimeSessionRuntime>(new MNNConversationProviderRuntime(providers.Model, _stages.Model, runtime, Diagnostics));
                }
                providers.Tts = plan.Tts ? Acquire("tts", _stages.Tts) : null;
                providers.Asr = plan.Asr ? Acquire("asr", _stages.Asr) : null;
                providers.Vad = plan.Vad ? Acquire("vad", _stages.Vad) : null;
                providers.Turn = plan.Turn ? Acquire("turn", _stages.Turn) : null;
                if (!(providers.Model is IMNNConversationTextProvider) || plan.Tts && !(providers.Tts is IMNNConversationTtsProvider) ||
                    plan.Asr && !(providers.Asr is IMNNConversationAsrProvider) || plan.Vad && !(providers.Vad is IMNNConversationVadProvider) ||
                    plan.Turn && !(providers.Turn is IMNNConversationTurnProvider))
                    throw new MNNRealtimeRouteUnavailableException(report.Route, "The selected stage does not implement its task interface.");
                providers.Prepare = (value, ct) => Diagnostics.PrepareAsync(registrations[value], value, ct);
                providers.Release = Diagnostics.Dispose;
                if (providers.Tts is IMNNConversationTtsDiagnostics timings) providers.SpeechTimings = () => timings.LastStageTimings;
                Active = new MNNConversationRuntime(_context, _options, output, report.Route, providers, _history);
                return Task.FromResult<IMNNRealtimeSessionRuntime>(Active);
            }
            catch (Exception error)
            {
                CleanupFailedCreation(new[] { providers.Model, providers.Tts, providers.Asr, providers.Vad, providers.Turn },
                    error, Diagnostics, cleanup => _failedCreationCleanup.Add(cleanup));
                throw;
            }
        }

        /// <summary>Attempts every distinct provider. A thrown AggregateException retains the retryable cleanup group,
        /// accessible through GetFailedCreationCleanup. Factory-created groups are also retained until ReleaseAsync.</summary>
        public static void DisposeUntransferredProviders(IEnumerable<IMNNConversationProvider> providers,
            Exception creationError, MNNConversationRuntimeDiagnostics diagnostics = null)
        {
            CleanupFailedCreation(providers, creationError, diagnostics, null);
        }

        public static MNNConversationProviderCleanup GetFailedCreationCleanup(AggregateException error)
        {
            if (error == null) throw new ArgumentNullException(nameof(error));
            return error.Data[typeof(MNNConversationProviderCleanup)] as MNNConversationProviderCleanup;
        }

        private static void CleanupFailedCreation(IEnumerable<IMNNConversationProvider> providers, Exception creationError,
            MNNConversationRuntimeDiagnostics diagnostics, Action<MNNConversationProviderCleanup> retain)
        {
            var cleanup = new MNNConversationProviderCleanup(providers, diagnostics);
            try
            {
                cleanup.Dispose();
            }
            catch (AggregateException error)
            {
                retain?.Invoke(cleanup);
                var failures = new List<Exception> { creationError };
                failures.AddRange(error.InnerExceptions);
                var combined = new AggregateException("Provider creation and cleanup failed.", failures);
                combined.Data[typeof(MNNConversationProviderCleanup)] = cleanup;
                throw combined;
            }
        }

        public Task SubmitAudioAsync(float[] audio, CancellationToken token)
        {
            RequireActive();
            return Active != null ? Active.SubmitAudioAsync(audio, token) :
                Task.FromException(new NotSupportedException("The provider-owned realtime session has no recorded audio adapter."));
        }

        public Task SubmitTextAsync(string text, CancellationToken token)
        {
            RequireActive();
            return Active != null ? Active.SubmitTextAsync(text, token) :
                Task.FromException(new NotSupportedException("The provider-owned realtime session has no text adapter."));
        }

        public Task ResetConversationAsync()
        {
            RequireActive();
            return Active != null ? Active.ResetConversationAsync() :
                Task.FromException(new NotSupportedException("This provider-owned session cannot reset history without reloading."));
        }

        public async Task<string> TranscribeAsync(float[] audio, CancellationToken token)
        {
            RequireActive();
            if (Active != null && Active.HasAsr) return await Active.TranscribeAsync(audio, token).ConfigureAwait(false);
            audio = await _context.PrepareVoiceAsync(audio, token).ConfigureAwait(false);
            return audio == null ? "" : await _transcription.TranscribeAsync(audio, token).ConfigureAwait(false);
        }

        private void RequireActive()
        {
            if (_released) throw new ObjectDisposedException(nameof(MNNConversationRuntimeFactory));
        }
    }
}
