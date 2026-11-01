using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    /// <summary>
    /// Decorates concrete runtimes with bounded startup/recovery fallback. Inject into the existing Session;
    /// it remains the sole lifecycle coordinator. No inference, audio callback, response replay or native code.
    /// Lifecycle methods must be serialized by that Session. Diagnostics are immutable, thread-safe snapshots.
    /// </summary>
    public sealed class MNNRealtimeFallbackRuntime : IMNNRealtimeSessionRuntime
    {
        private readonly IMNNRealtimeRuntimeFactory _factory;
        private readonly MNNRealtimeOutputPipeline _output;
        private readonly MNNRealtimeCapabilities _model;
        private readonly MNNRealtimeCapabilities _device;
        private readonly MNNRealtimeCapabilities _tts;
        private readonly MNNRealtimeCapabilities _asr;
        private readonly MNNRealtimeCapabilities _vad;
        private readonly bool _requireMeasuredSpeech;
        private readonly bool _allowBufferedSpeech;
        private readonly bool _speechOutput;
        private readonly List<MNNRealtimeRoute> _excluded = new List<MNNRealtimeRoute>(5);
        private readonly List<IMNNRealtimeSessionRuntime> _created = new List<IMNNRealtimeSessionRuntime>(5);
        private MNNRealtimeRouteSelection _selection;
        private IReadOnlyList<MNNRealtimeRouteUnavailableException> _failures =
            Array.AsReadOnly(Array.Empty<MNNRealtimeRouteUnavailableException>());
        private IMNNRealtimeSessionRuntime _active;
        private CancellationToken _lifetimeToken;
        private bool _prepareStarted;
        private bool _prepared;
        private bool _listening;
        private bool _stopped;
        private bool _quiesced;
        private bool _disposed;

        public MNNRealtimeRouteSelection Selection => Volatile.Read(ref _selection);
        public MNNRealtimeReport CurrentReport => Selection.Selected;
        public IReadOnlyList<MNNRealtimeRouteUnavailableException> Failures => Volatile.Read(ref _failures);
        public MNNRealtimeOutputPipeline Output => _output;

        /// <summary>Validates initial selection without acquiring resources. Capability snapshots stay fixed.</summary>
        public MNNRealtimeFallbackRuntime(IMNNRealtimeRuntimeFactory factory, MNNRealtimeOutputPipeline output,
            MNNRealtimeCapabilities model, MNNRealtimeCapabilities device, MNNRealtimeCapabilities tts = null,
            MNNRealtimeCapabilities asr = null, MNNRealtimeCapabilities vad = null,
            IReadOnlyCollection<MNNRealtimeRoute> excludedRoutes = null, bool requireMeasuredSpeech = true, bool allowBufferedSpeech = false,
            bool speechOutput = true)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _output = output ?? throw new ArgumentNullException(nameof(output));
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _device = device ?? throw new ArgumentNullException(nameof(device));
            _tts = tts;
            _asr = asr;
            _vad = vad;
            _requireMeasuredSpeech = requireMeasuredSpeech;
            _allowBufferedSpeech = allowBufferedSpeech;
            _speechOutput = speechOutput;
            if (excludedRoutes != null) _excluded.AddRange(excludedRoutes);
            _selection = SelectNext();
            _selection.RequireSelected();
        }

        public async Task PrepareAsync(CancellationToken cancellationToken)
        {
            if (_prepareStarted || _stopped || _disposed)
                throw new InvalidOperationException("Fallback runtime can only be prepared once.");
            _prepareStarted = true;
            _lifetimeToken = cancellationToken;
            await ActivateNextAsync(false).ConfigureAwait(false);
        }

        public async Task StartListeningAsync(CancellationToken cancellationToken)
        {
            RequirePrepared();
            if (_listening)
                throw new InvalidOperationException("Runtime is already listening.");
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await _active.StartListeningAsync(cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                _listening = true;
            }
            catch (MNNRealtimeRouteUnavailableException error) when (error.Route == CurrentReport.Route)
            {
                await RejectActiveAsync(error).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                await ActivateNextAsync(true).ConfigureAwait(false);
            }
        }

        public Task RequestCancellationAsync(MNNResponseId response)
        {
            RequirePrepared();
            return _active.RequestCancellationAsync(response);
        }

        public Task WaitForInferenceQuiescenceAsync(MNNResponseId response)
        {
            RequirePrepared();
            return _active.WaitForInferenceQuiescenceAsync(response);
        }

        /// <summary>
        /// Session already silenced and joined the interrupted producer. A route-specific restore failure
        /// releases the entire old runtime, then prepares and starts the next one at a new turn boundary.
        /// No old response is generated again. New runtime Prepare must restore committed history.
        /// </summary>
        public async Task RestoreContextAsync(MNNResponseId response)
        {
            RequirePrepared();
            try
            {
                await _active.RestoreContextAsync(response).ConfigureAwait(false);
            }
            catch (MNNRealtimeRouteUnavailableException error) when (error.Route == CurrentReport.Route)
            {
                await RejectActiveAsync(error).ConfigureAwait(false);
                await ActivateNextAsync(true).ConfigureAwait(false);
            }
        }

        public async Task StopListeningAsync()
        {
            _stopped = true;
            _output.Interrupt();
            if (_active != null)
                await _active.StopListeningAsync().ConfigureAwait(false);
            _listening = false;
        }

        public async Task WaitForQuiescenceAsync()
        {
            _quiesced = false;
            if (_active != null)
                await _active.WaitForQuiescenceAsync().ConfigureAwait(false);
            _quiesced = true;
        }

        public async Task DisposeAsync()
        {
            if (_disposed)
                return;
            if (!_stopped || !_quiesced)
                throw new InvalidOperationException("Stop and confirm quiescence before disposing fallback runtime.");
            if (_active != null)
                await _active.DisposeAsync().ConfigureAwait(false);
            _active = null;
            _prepared = false;
            _disposed = true;
        }

        private MNNRealtimeRouteSelection SelectNext() =>
            MNNRealtimeRouteSelector.SelectBest(_model, _device, _tts, _asr, _vad, _excluded, _requireMeasuredSpeech, _allowBufferedSpeech, _speechOutput);

        private async Task ActivateNextAsync(bool startListening)
        {
            while (true)
            {
                _lifetimeToken.ThrowIfCancellationRequested();
                if (_output.IsFaulted)
                    throw new InvalidOperationException("Shared output is faulted; route fallback cannot repair it.");
                var selection = SelectNext();
                Volatile.Write(ref _selection, selection);
                var report = selection.RequireSelected();
                try
                {
                    var runtime = await _factory.CreateAsync(report, _output, _lifetimeToken).ConfigureAwait(false);
                    if (runtime == null || ReferenceEquals(runtime, this) || WasCreated(runtime))
                        throw new InvalidOperationException("Factory must return a fresh non-null runtime for each route.");
                    _active = runtime; // Ownership transfers even if cancellation raced factory completion.
                    _created.Add(runtime);
                    _quiesced = false;
                    _lifetimeToken.ThrowIfCancellationRequested();
                    await runtime.PrepareAsync(_lifetimeToken).ConfigureAwait(false);
                    _lifetimeToken.ThrowIfCancellationRequested();
                    if (startListening)
                    {
                        await runtime.StartListeningAsync(_lifetimeToken).ConfigureAwait(false);
                        _lifetimeToken.ThrowIfCancellationRequested();
                    }
                    _prepared = true;
                    _listening = startListening;
                    return;
                }
                catch (MNNRealtimeRouteUnavailableException error) when (error.Route == report.Route)
                {
                    await RejectActiveAsync(error).ConfigureAwait(false);
                }
            }
        }

        private async Task RejectActiveAsync(MNNRealtimeRouteUnavailableException error)
        {
            _output.Interrupt();
            _prepared = false;
            _listening = false;
            _excluded.Add(error.Route);
            var previous = Failures;
            var failures = new MNNRealtimeRouteUnavailableException[previous.Count + 1];
            for (int i = 0; i < previous.Count; ++i)
                failures[i] = previous[i];
            failures[previous.Count] = error;
            Volatile.Write(ref _failures, Array.AsReadOnly(failures));
            try
            {
                await ReleaseActiveAsync().ConfigureAwait(false);
            }
            catch (Exception cleanup)
            {
                // Do not classify shutdown errors as route failures. Keep ownership for Session.Stop retry.
                throw new AggregateException("Old route did not release cleanly; fallback stopped.", error, cleanup);
            }
        }

        private async Task ReleaseActiveAsync()
        {
            if (_active == null)
                return;
            Exception stopFailure = null;
            try { await _active.StopListeningAsync().ConfigureAwait(false); }
            catch (Exception error) { stopFailure = error; }
            try
            {
                await _active.WaitForQuiescenceAsync().ConfigureAwait(false);
            }
            catch (Exception error)
            {
                if (stopFailure != null)
                    throw new AggregateException(stopFailure, error);
                throw;
            }
            try
            {
                await _active.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception error)
            {
                if (stopFailure != null)
                    throw new AggregateException(stopFailure, error);
                throw;
            }
            _active = null;
            if (stopFailure != null)
                throw stopFailure;
        }

        private void RequirePrepared()
        {
            if (!_prepared || _active == null || _stopped || _disposed)
                throw new InvalidOperationException("Fallback runtime is not prepared or has stopped.");
        }

        private bool WasCreated(IMNNRealtimeSessionRuntime runtime)
        {
            foreach (var previous in _created)
            {
                if (ReferenceEquals(previous, runtime))
                    return true;
            }
            return false;
        }
    }
}
