using System;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    /// <summary>Reusable exclusive duplex-device session. One control thread sequences permission, negotiation,
    /// warm-up, route supervision, estimated presentation, echo ownership and restart. Capture publication is worker-safe.</summary>
    public sealed partial class MNNRealtimeDeviceSession
    {
        private const int Fresh = 0, Opening = 1, Ready = 2, Closed = 3;
        private readonly MNNRealtimeDeviceBinding _binding;
        private readonly IMNNRealtimeDeviceClock _clock;
        private MNNRealtimeDeviceSessionOptions _options;
        private readonly Func<IMNNAudioCaptureSource, int, int, MNNEchoCancellationWorker> _echoFactory;
        private readonly CaptureProxy _capture;
        private MNNRealtimeOutputPipeline _output;
        private MNNEchoCancellationWorker _echo;
        private IDisposable _reference;
        private int _state, _captureRequested;
        private bool _inputOwned, _outputOwned;
        private bool _echoReleased;
        private Task _closeTask = Task.CompletedTask;
        private TaskCompletionSource<bool> _openingFinished;
        private CancellationTokenSource _openingCancellation;
        private Exception _fault;
        private long _presentedThrough;
        private double? _restartDeadline;
        private double _presentationTailMilliseconds;
        private double _nextSupervision;
        public MNNRealtimeEchoHealth BackendEchoHealth { get; } = new MNNRealtimeEchoHealth();
        public bool IsReady => Volatile.Read(ref _state) == Ready;
        public bool IsClosed => Volatile.Read(ref _state) == Closed;
        public bool HasBeenUsed => Volatile.Read(ref _state) != Fresh;
        public IMNNAudioCaptureSource Capture => _capture;
        public MNNRealtimeOutputPipeline Output => _output;
        public MNNEchoCancellationWorker Echo => _echo;
        public Exception Fault => Volatile.Read(ref _fault) ?? _echo?.Fault ?? _binding.Fault;
        public bool CanInterruptSpeech => IsReady && Fault == null && (BackendEchoHealth.Healthy || _echo?.Healthy == true);
        public int? EchoDelayMilliseconds => _echo?.Healthy == true ? _echo.DelayMilliseconds : (int?)null;
        public double PresentationTailMilliseconds => _presentationTailMilliseconds;
        public bool SpeechMayBeAudible => _binding.SpeechMayBeAudible;
        public float? SpeechVolume => _binding.SpeechVolume;
        public string EchoStatus => IsClosed ? "Echo processing is not active." :
            "Backend AEC: " + (BackendEchoHealth.Healthy ? "healthy" : "not qualified") +
            (_echo == null ? "" : "; additional model: " + _echo.Status);

        public MNNRealtimeDeviceSession(MNNRealtimeDeviceBinding binding, MNNRealtimeDeviceSessionOptions options = null,
            IMNNRealtimeDeviceClock clock = null, Func<IMNNAudioCaptureSource, int, int, MNNEchoCancellationWorker> echoFactory = null)
        {
            _binding = binding ?? throw new ArgumentNullException(nameof(binding));
            _options = (options ?? new MNNRealtimeDeviceSessionOptions()).Snapshot(); _options.Validate();
            _clock = clock ?? new MNNRealtimeDeviceClock(); _echoFactory = echoFactory;
            _capture = new CaptureProxy(this);
        }

        /// <summary>Reconfigure only after ResetForRetry. Existing device/model ownership is never overwritten.</summary>
        public void Configure(MNNRealtimeDeviceSessionOptions options)
        {
            if (HasBeenUsed) throw new InvalidOperationException("Configure a fresh or successfully reset device session.");
            var snapshot = (options ?? throw new ArgumentNullException(nameof(options))).Snapshot(); snapshot.Validate(); _options = snapshot;
        }

        public async Task<MNNRealtimeOutputPipeline> OpenAsync(CancellationToken token, Func<CancellationToken, Task> prepareModels = null)
        {
            if (HasBeenUsed) throw new InvalidOperationException("Reset a successfully closed session before reopening devices.");
            token.ThrowIfCancellationRequested(); _binding.ValidateFresh();
            Volatile.Write(ref _state, Opening);
            _openingFinished = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var openingCancellation = _openingCancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
            token = openingCancellation.Token;
            try
            {
                await _binding.RequestPermissionAsync(token);
                CheckStartup(token);
                if (prepareModels != null) await prepareModels(token);
                CheckStartup(token);
                _outputOwned = true; _binding.OpenPlayback();
                _binding.SetCaptureEnabled(false);
                if (_binding.OpensPlaybackFirst) BindOutput(default, 0);
                _inputOwned = true;
                _binding.StartCapture((format, capacity) => { if (!_binding.OpensPlaybackFirst) BindOutput(format, capacity); });
                await WaitUntilAsync(() => _binding.ReadState().Capturing && _binding.Capture.CaptureFormat.SampleRate != 0,
                    _options.CaptureWarmupSeconds, "Microphone did not produce a capture buffer.", token);
                var capture = _binding.Capture;
                if (_output == null || !_output.Format.Equals(_binding.OutputFormat)) BindOutput(capture.CaptureFormat, capture.MaximumCaptureSamples);
                await StartEchoProcessingAsync();
                CheckStartup(token);
                _binding.SetCaptureEnabled(_echo != null);
                _binding.StartPlayback();
                await WaitUntilAsync(() => _binding.ReadState().Playing, _options.OutputWarmupSeconds,
                    "Speaker output did not start.", token);
                Volatile.Write(ref _state, Ready); ApplyCaptureGate();
                return _output;
            }
            catch (Exception startup)
            {
                Close();
                try { await _closeTask; }
                catch (Exception cleanup) { throw new AggregateException("Audio startup and cleanup failed.", startup, cleanup); }
                throw;
            }
            finally
            {
                _openingCancellation = null; openingCancellation.Dispose();
                _openingFinished.TrySetResult(true);
            }
        }

        private void BindOutput(MNNPcmFormat captureFormat, int capacity)
        {
            var format = _binding.OutputFormat; format.RequireValid();
            var output = new MNNRealtimeOutputPipeline(format, Math.Max(1, format.SampleRate / 100),
                blockCapacity: _options.OutputBlockCapacity, segmentCapacity: _options.OutputSegmentCapacity);
            _binding.BindOutput(output, captureFormat, capacity); _output = output;
        }

        private async Task WaitUntilAsync(Func<bool> ready, double timeout, string message, CancellationToken token)
        {
            double deadline = _clock.Seconds + timeout;
            while (!ready())
            {
                CheckStartup(token);
                if (_clock.Seconds >= deadline) throw new TimeoutException(message);
                await _clock.DelayAsync(10, token);
            }
            CheckStartup(token);
        }

        private void CheckStartup(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (IsClosed || !_binding.ReadState().Active) throw new OperationCanceledException("Audio devices were disabled during startup.", token);
            if (Fault != null) throw new InvalidOperationException("Audio device initialization failed.", Fault);
        }

        /// <summary>Starts optional worker-owned echo inference after capture and output formats are negotiated.</summary>
        public void StartEchoProcessing()
        {
            if (!_options.EchoEnabled || _echo != null) return;
            if (IsClosed || _output == null) throw new InvalidOperationException("Negotiate output before starting echo processing.");
            var worker = CreateEchoWorker(_binding.Capture, _output.Format.SampleRate, _binding.ReferenceBlockFrames);
            _echo = worker;
            _echoReleased = false;
            _reference = _binding.BindEchoReference(worker);
        }
        private async Task StartEchoProcessingAsync()
        {
            if (!_options.EchoEnabled || _echo != null) return;
            var capture = _binding.Capture; int rate = _output.Format.SampleRate, frames = _binding.ReferenceBlockFrames;
            MNNEchoCancellationWorker worker = await Task.Run(() => CreateEchoWorker(capture, rate, frames));
            _echo = worker; _echoReleased = false;
            if (IsClosed) throw new OperationCanceledException("Audio was stopped while its echo model loaded.");
            _reference = _binding.BindEchoReference(worker);
        }

        private MNNEchoCancellationWorker CreateEchoWorker(IMNNAudioCaptureSource capture, int rate, int frames)
        {
            if (_echoFactory != null) return _echoFactory(capture, rate, frames) ?? throw new InvalidOperationException("Echo factory returned no worker.");
            if (string.IsNullOrWhiteSpace(_options.EchoModelDirectory)) throw new ArgumentException("Choose an installed echo model directory.");
            var model = new MNNDtlnEchoHopProcessor(MNNDtlnAec.Load(_options.EchoModelDirectory, 1, MNNBackendType.CPU));
            try { return new MNNEchoCancellationWorker(capture, model, rate, _options.Echo, frames); }
            catch { model.Dispose(); throw; }
        }

        public void SetCaptureEnabled(bool enabled)
        {
            Volatile.Write(ref _captureRequested, enabled ? 1 : 0); ApplyCaptureGate();
        }
        private void ApplyCaptureGate()
        {
            bool allowed = Volatile.Read(ref _captureRequested) != 0 && IsReady;
            _echo?.SetPublicationEnabled(allowed);
            _binding.SetCaptureEnabled(!IsClosed && (_echo != null || allowed));
        }
        public async Task PauseCaptureAsync(CancellationToken token)
        {
            await _binding.FlushCaptureAsync(token);
            Task tail = _echo?.PausePublicationAsync(token) ?? Task.CompletedTask;
            SetCaptureEnabled(false); await tail;
        }
        public Task FadeOutAsync() => _binding.FadeOutAsync();
        public void SetSpeechVolume(float volume)
        {
            if (float.IsNaN(volume) || float.IsInfinity(volume) || volume < 0 || volume > 1) throw new ArgumentOutOfRangeException(nameof(volume));
            _binding.SetSpeechVolume(volume);
        }

        private sealed class CaptureProxy : IMNNAudioCaptureSource
        {
            private readonly MNNRealtimeDeviceSession _owner;
            internal CaptureProxy(MNNRealtimeDeviceSession owner) => _owner = owner;
            private IMNNAudioCaptureSource Source => (IMNNAudioCaptureSource)_owner._echo ?? _owner._binding.Capture;
            public MNNPcmFormat CaptureFormat => Source?.CaptureFormat ?? default;
            public int MaximumCaptureSamples => Source?.MaximumCaptureSamples ?? 0;
            public long DroppedCaptureBlocks => Source?.DroppedCaptureBlocks ?? 0;
            public bool HasPendingCaptureFrames => Source?.HasPendingCaptureFrames ?? false;
            public bool TryReadCapture(float[] destination, int offset, out MNNAudioFrameInfo info, out int count)
            { info = default; count = 0; return Source != null && Source.TryReadCapture(destination, offset, out info, out count); }
        }
    }
}
