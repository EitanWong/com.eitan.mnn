using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    /// <summary>
    /// Composes shared capture/VAD/input, session interruption and speech output. Input, turn evaluation,
    /// text generation and synthesis have separate owners; no model/network work runs on a device callback.
    /// </summary>
    public partial class MNNConversationRuntime : IMNNRealtimeSessionRuntime
    {
        protected readonly object _sync = new object();
        protected readonly IMNNConversationContext _context;
        protected readonly MNNConversationOptions _config;
        protected readonly MNNRealtimeOutputPipeline _output;
        protected readonly MNNRealtimeRoute _route;
        protected readonly int _turnRetrySilenceSamples;
        protected readonly MNNConversationProviders _providers;
        protected readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        public MNNConversationMemory Memory { get; }
        protected MNNRealtimeRouteUnavailableException _routeFailure;
        protected MNNRealtimeUtteranceBuffer _utterance;
        protected readonly MNNBargeInPolicy _bargeIn = new MNNBargeInPolicy();
        protected MNNRealtimeModelInputBuffer _input;
        protected MNNRealtimeAudioCaptureWorker _capture;
        protected MNNConversationRecognitionWorker _recognitionPreview;
        protected TaskCompletionSource<bool> _recordingDrain;
        protected bool _inputFinished;
        protected Exception _inputFailure;
        protected Task _inputTask = Task.CompletedTask;
        protected Task _responseTask = Task.CompletedTask;
        protected Task _recovery = Task.CompletedTask;
        protected CancellationTokenSource _responseCancellation;
        protected CancellationTokenSource _turnCancellation;
        protected Task<VoiceCandidate> _turnTask;
        protected long _turnRevision, _rejectedRevision = -1;
        protected int _turnSilenceSamples, _nextTurnSilenceSamples;
        protected float[] _turnAudio;
        protected float[] _acceptedAudio;
        protected string _acceptedText;
        protected long _acceptedRevision;
        protected MNNResponseId _response;
        protected MNNConversationTelemetry.Trace _activeTrace;
        public bool TryReadAgentTrace(out MNNConversationTranscriptEntry entry)
        {
            if (_providers.Model is IMNNAgentTraceSource source) return source.TryReadTrace(out entry);
            entry = default; return false;
        }
        public MNNConversationTelemetry TurnTelemetry { get; } = new MNNConversationTelemetry();
        protected bool _stopped;
        private readonly HashSet<IMNNConversationProvider> _releasedProviders = new HashSet<IMNNConversationProvider>(MNNConversationProviders.Identity);
        protected int _echoSuppressed;
        protected bool HalfDuplex => _config.speechOutput && !_context.CanInterruptSpeech;
        protected int _inputRevision = -1;
        protected long _resumeAfter;
        protected long _inputGaps;
        public MNNRealtimeSynthesisMeter SynthesisMeter { get; } = new MNNRealtimeSynthesisMeter();
        public string InputDiagnostics => _capture == null ? "Not started" :
            "Capture processed/dropped/expired blocks: " + _capture.ProcessedBlocks + "/" +
            _capture.DroppedCaptureBlocks + "/" + _capture.ExpiredCaptureBlocks +
            "\nModel dropped/expired frames: " + (_input?.DroppedFrames ?? 0) + "/" + (_input?.ExpiredFrames ?? 0) +
            "\nInput gaps: " + Interlocked.Read(ref _inputGaps) +
            " · automatic echo suppression: " + (Volatile.Read(ref _echoSuppressed) != 0) +
            " · live echo control: " + _context.CanInterruptSpeech +
            " · incremental preview: " + (_recognitionPreview == null ? "unavailable" : _recognitionPreview.Fault?.Message ?? ("active; lost frames " + _recognitionPreview.DroppedFrames));
        public MNNConversationRuntime(IMNNConversationContext context, MNNConversationOptions config,
            MNNRealtimeOutputPipeline output, MNNRealtimeRoute route, MNNConversationProviders providers, List<MNNOpenAIMessage> history = null)
        {
            _context = context; _config = config.Snapshot(); _output = output; _route = route;
            _turnRetrySilenceSamples = _config.turnRetryMilliseconds * 16;
            _providers = providers?.Snapshot() ?? new MNNConversationProviders(); Memory = new MNNConversationMemory(history ?? new List<MNNOpenAIMessage>(), _config.maximumHistoryMessages);
            // Keep the configured speech confirmation window before VAD onset, so the
            // recognizer receives the first word while AEC/VAD confirms a live interruption.
            _utterance = new MNNRealtimeUtteranceBuffer(_config.minimumSpeechMilliseconds, _config.silenceMilliseconds, _config.maximumUtteranceSeconds,
                preRollMilliseconds: _config.minimumSpeechMilliseconds);
        }
        public async Task PrepareAsync(CancellationToken token)
        {
            _context.RealtimeInactivitySeconds = _config.realtimeInactivitySeconds;
            await _providers.PrepareAsync(_providers.Model, token).ConfigureAwait(false);
            await _providers.PrepareAsync(_providers.Tts, token).ConfigureAwait(false);
            await _providers.PrepareAsync(_providers.Asr, token).ConfigureAwait(false);
            await _providers.PrepareAsync(_providers.Vad, token).ConfigureAwait(false);
            await _providers.PrepareAsync(_providers.Turn, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            IMNNRealtimeVoiceActivity activity;
            if (_providers.Vad is IMNNConversationVadProvider vad)
            {
                var frame = new float[512];
                bool echoControlled = _context.CanInterruptSpeech;
                Action beforeFrame = () =>
                {
                    bool qualified = _context.CanInterruptSpeech;
                    if (qualified == echoControlled) return;
                    // Clear prequalification recurrent speech state without changing PCM time.
                    vad.Reset();
                    echoControlled = qualified;
                };
                if (vad is IMNNRealtimeVoiceActivity detector)
                    activity = new MNNRealtimeFrameVoiceActivity((samples, offset) =>
                    {
                        beforeFrame();
                        return detector.ProcessFrame(samples, offset);
                    }, reset: vad.Reset);
                else
                    activity = new MNNRealtimeFrameVoiceActivity((samples, offset) =>
                    {
                        beforeFrame();
                        Array.Copy(samples, offset, frame, 0, 512);
                        return vad.IsSpeech(frame, 512, 16000) ? 1 : 0;
                    }, reset: vad.Reset);

            }
            else activity = MNNRealtimeFrameVoiceActivity.Energy(_config.energyThreshold);
            // Preserve valid speech across short scheduler/GC stalls. Capacity is headroom,
            // never a batching delay; sustained overload still reports an actual queue loss.
            var maximumAge = _context.MaximumCaptureAge;
            _input = new MNNRealtimeModelInputBuffer(64, maximumAge);
            var source = _context.Capture;
            var processor = _context.CreateVoiceInputProcessor(new MNNPcmFormat(source.CaptureFormat.SampleRate, 1),
                source.MaximumCaptureSamples / source.CaptureFormat.Channels, activity);
            _capture = new MNNRealtimeAudioCaptureWorker(source, processor, _input,
                threadName: "MNN conversation capture", maximumCaptureAge: maximumAge);
            if (_providers.Asr is IMNNConversationStreamingAsrProvider streaming && !_context.RequiresUtteranceSpeakerSelection)
                _recognitionPreview = new MNNConversationRecognitionWorker(streaming.CreateStream(),
                    (_config.minimumSpeechMilliseconds * 16 + 511) / 512);
            _context.FlushInputAsync = DrainRecordingInputAsync;
            var capture = _capture;
            var input = _input;
            _context.LostInputFrames = () => capture.DroppedCaptureBlocks + capture.ExpiredCaptureBlocks +
                input.DroppedFrames + input.ExpiredFrames;
        }
        protected async Task DrainRecordingInputAsync()
        {
            await _capture.FlushAsync().ConfigureAwait(false);
            var drain = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_sync)
            {
                if (_stopped || _inputFinished)
                    throw new InvalidOperationException("Recording input stopped before it could drain.", _inputFailure);
                if (_recordingDrain != null)
                    throw new InvalidOperationException("A recording drain is already pending.");
                _recordingDrain = drain;
                _input.WakeWaiter();
            }
            await drain.Task.ConfigureAwait(false);
        }

        public Task StartListeningAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            _capture.Start();
            _inputTask = Task.Factory.StartNew(InputLoop, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            _ = ObserveCaptureAsync();
            return Task.CompletedTask;
        }
        protected async Task ObserveCaptureAsync()
        {
            try { await _capture.Completion.ConfigureAwait(false); }
            catch (Exception error) { ReportFault(error); }
        }
        public bool IsResponding { get { lock (_sync) return !_responseTask.IsCompleted; } }
        public bool HasAsr => _providers.Asr is IMNNConversationAsrProvider;
        public async Task<string> TranscribeAsync(float[] audio, CancellationToken token)
        {
            audio = await _context.PrepareVoiceAsync(audio, token).ConfigureAwait(false);
            if (audio == null) return "";
            return await TranscribePreparedAsync(audio, token).ConfigureAwait(false);
        }
        protected async Task<string> TranscribePreparedAsync(float[] audio, CancellationToken token)
        {
            await _asrGate.WaitAsync(token).ConfigureAwait(false);
            try { return await ((IMNNConversationAsrProvider)_providers.Asr).TranscribeAsync(audio, 16000, token).ConfigureAwait(false); }
            finally { _asrGate.Release(); }
        }
        public Task SubmitAudioAsync(float[] audio, CancellationToken token)
        {
            if (audio == null || audio.Length < 4000 || audio.Length > 960000)
                throw new ArgumentException("Submit 0.25–60 seconds of mono 16 kHz audio.");
            return StartResponse(null, audio, token);
        }
        public Task SubmitTextAsync(string text, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length > _config.maximumResponseCharacters)
                throw new ArgumentException("Submit nonempty bounded user text.");
            return StartResponse(text, null, token);
        }
        protected Task StartResponse(string text, float[] audio, CancellationToken token, int realtimeRevision = -1, bool voiceValidated = false, bool audioPrepared = false) =>
            StartResponseCore(text, audio, token, realtimeRevision, voiceValidated, audioPrepared, null);

        private Task StartResponseCore(string text, float[] audio, CancellationToken token, int realtimeRevision,
            bool voiceValidated, bool audioPrepared, MNNConversationEventReply eventReply)
        {
            Task responseTask; CancellationTokenSource assistanceCancellation;
            lock (_sync)
            {
                if (_stopped) return Task.FromCanceled(new CancellationToken(true));
                // A caller cannot queue turns behind slow inference. VAD/session cancellation joins first.
                if (!_responseTask.IsCompleted) throw new InvalidOperationException("Wait for the previous response to quiesce before submitting another turn.");
                ++_textAssistanceRevision; assistanceCancellation = _textAssistanceCancellation;
                _eventReply?.ObservePresentation(_output);
                _eventReply = eventReply;
                _responseCancellation?.Dispose();
                _responseCancellation = CancellationTokenSource.CreateLinkedTokenSource(token, _lifetime.Token);
                var responseToken = _responseCancellation.Token;
                var trace = new MNNConversationTelemetry.Trace { Route = _route.ToString() };
                _activeTrace = trace;
                int exitRevision = realtimeRevision >= 0 ? realtimeRevision : _context.Input.RealtimeRevision;
                _responseTask = Task.Run(() => RespondAsync(text, audio, responseToken, trace, realtimeRevision, voiceValidated, audioPrepared, exitRevision), CancellationToken.None);
                responseTask = _responseTask;
            }
            CancelAssistanceSource(assistanceCancellation);
            return responseTask;
        }
        protected static Exception CombineFailures(string message, Exception first, Exception second)
        {
            if (first == null) return second;
            if (second == null || ReferenceEquals(first, second)) return first;
            return new AggregateException(message, first, second);
        }
        public static bool TryGetRouteFailure(Exception error, MNNRealtimeRoute route,
            out MNNRealtimeRouteUnavailableException routeFailure)
        {
            routeFailure = error as MNNRealtimeRouteUnavailableException;
            if (routeFailure == null && error is AggregateException aggregate)
                routeFailure = aggregate.GetBaseException() as MNNRealtimeRouteUnavailableException;
            return routeFailure != null && routeFailure.Route == route;
        }
        public static void CancelProductionAfterSpeechFailure(CancellationTokenSource production, Exception speechError)
        {
            try { production.Cancel(); }
            catch (Exception cancellationError)
            {
                throw new AggregateException(
                    "Speech production failed and cancellation callbacks also failed.", speechError, cancellationError);
            }
        }
        protected void CommitPresentedHistory()
        {
            if (_response.IsValid && _output.Accepts(_response))
            {
                if (!_output.CanBeginNextResponse(_response))
                    throw new InvalidOperationException("Present or interrupt the previous reply before submitting the next turn.");
                _output.Interrupt();
            }
            Memory.CommitPresented(_output, _response);
            _response = default;
        }
        protected void RequestRouteFallback(MNNRealtimeRouteUnavailableException error)
        {
            Volatile.Write(ref _routeFailure, error);
            // Recovery joins this producer before RestoreContext reports the route failure. Never
            // replay a partly spoken request, and never replace resources from inside this worker.
            Volatile.Write(ref _recovery, _context.Session.InterruptAsync());
        }
        protected void ReportFault(Exception error)
        {
            // Never await shutdown from a worker that shutdown joins.
            if (!_lifetime.IsCancellationRequested && _context.Session != null) _ = _context.Session.ReportFaultAsync(error);
        }
        public Task RequestCancellationAsync(MNNResponseId response)
        {
            // Cancellation callbacks are provider code and run synchronously. Snapshot the owned
            // source under lock, then invoke callbacks without holding the runtime state lock.
            CancellationTokenSource cancellation;
            lock (_sync) cancellation = _responseCancellation;
            cancellation?.Cancel();
            return Task.CompletedTask;
        }
        public async Task WaitForInferenceQuiescenceAsync(MNNResponseId response)
        {
            Task task;
            lock (_sync) task = _responseTask;
            try { await task.ConfigureAwait(false); }
            finally { await JoinTextAssistanceAsync().ConfigureAwait(false); }
        }
        public Task RestoreContextAsync(MNNResponseId response)
        {
            // Called after the response owner joined. The factory retains only committed history
            // across route replacement; generated but unpresented text never enters the next model.
            CommitPresentedHistory();
            var failure = Volatile.Read(ref _routeFailure);
            if (failure != null) throw failure;
            return Task.CompletedTask;
        }
        public async Task ResetConversationAsync()
        {
            await JoinTextAssistanceAsync().ConfigureAwait(false);
            lock (_sync)
            {
                if (!_responseTask.IsCompleted || _context.Input.Realtime || _context.Input.Recording)
                    throw new InvalidOperationException("Join the current response and disable input before clearing memory.");
                Memory.Reset();
                _context.ResetIdentity();
                if (_response.IsValid) _output.RetireHistoryThrough(_response);
                _response = default;
                // Built-in local and HTTP providers receive the complete prompt each turn.
                // The next generation resets KV state using the empty managed history.
            }
        }
        public Task StopListeningAsync()
        {
            lock (_sync)
            {
                _stopped = true;
                Interlocked.Exchange(ref _recordingDrain, null)?.TrySetCanceled();
                if (_context != null)
                {
                    _context.FlushInputAsync = null;
                    _context.LostInputFrames = null;
                }
            }
            _lifetime.Cancel();
            _input?.WakeWaiter();
            return Task.CompletedTask;
        }
        public async Task WaitForQuiescenceAsync()
        {
            await _inputTask.ConfigureAwait(false);
            if (_recognitionPreview != null) await _recognitionPreview.StopAsync().ConfigureAwait(false);
            // Physical source may remain active during startup fallback. Stop has an explicit join budget;
            // callbacks copy/drop into the capture queue and never own provider resources.
            if (_capture != null && !_capture.Stop(TimeSpan.FromSeconds(2))) throw new TimeoutException("Capture worker did not quiesce.");
            await WaitForInferenceQuiescenceAsync(default).ConfigureAwait(false);
            if (_turnTask != null)
            {
                try { await _turnTask.ConfigureAwait(false); }
                catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
            }
        }
        public Task DisposeAsync()
        {
            // Called only after quiescence. The factory/session may retain this runtime for
            // diagnostics; release PCM snapshots, utterance storage and completed task closures.
            lock (_sync)
            {
                _stopped = true;
                Interlocked.Exchange(ref _recordingDrain, null)?.TrySetCanceled();
                if (_context != null)
                {
                    _context.FlushInputAsync = null;
                    _context.LostInputFrames = null;
                }
            }
            _acceptedAudio = _turnAudio = null;
            _utterance = null;
            _inputTask = _responseTask = Task.CompletedTask;
            _turnTask = null;
            List<Exception> failures = null;
            try { _recognitionPreview?.Dispose(); _recognitionPreview = null; }
            catch (Exception error) { (failures ?? (failures = new List<Exception>())).Add(error); }
            try { _capture?.Dispose(); _capture = null; }
            catch (Exception error) { (failures ?? (failures = new List<Exception>())).Add(error); }
            try { _input?.Dispose(); _input = null; }
            catch (Exception error) { (failures ?? (failures = new List<Exception>())).Add(error); }
            try { _turnCancellation?.Dispose(); _turnCancellation = null; }
            catch (Exception error) { (failures ?? (failures = new List<Exception>())).Add(error); }
            try { _responseCancellation?.Dispose(); _responseCancellation = null; }
            catch (Exception error) { (failures ?? (failures = new List<Exception>())).Add(error); }
            try { _textAssistanceCancellation?.Dispose(); _textAssistanceCancellation = null; }
            catch (Exception error) { (failures ?? (failures = new List<Exception>())).Add(error); }
            try { _lifetime.Dispose(); }
            catch (Exception error) { (failures ?? (failures = new List<Exception>())).Add(error); }
            DisposeProvider(ref _providers.Model, ref failures);
            DisposeProvider(ref _providers.Tts, ref failures);
            DisposeProvider(ref _providers.Asr, ref failures);
            DisposeProvider(ref _providers.Vad, ref failures);
            DisposeProvider(ref _providers.Turn, ref failures);
            if (failures != null) throw new AggregateException("One or more conversation resources failed to dispose.", failures);
            return Task.CompletedTask;
        }
        protected void DisposeProvider(ref IMNNConversationProvider provider, ref List<Exception> failures)
        {
            try
            {
                if (provider != null && !_releasedProviders.Contains(provider))
                {
                    _providers.Dispose(provider);
                    _releasedProviders.Add(provider);
                }
                provider = null;
            }
            catch (Exception error) { (failures ?? (failures = new List<Exception>())).Add(error); }
        }
    }
}
