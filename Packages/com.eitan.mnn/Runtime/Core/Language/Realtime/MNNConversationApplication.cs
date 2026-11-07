using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    /// <summary>One-shot conversation application owner. Call control methods and Tick on the creating UI thread.
    /// Reuse package devices, runtime factory, commands and transcript storage with any registered providers.</summary>
    public class MNNConversationApplication
    {
        private readonly MNNRealtimeCapabilities _deviceCapabilities;
        private readonly MNNConversationOptions _options;
        private readonly MNNConversationRegistrations _registrations;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private MNNConversationRuntimeFactory _factory;
        private MNNRealtimeSession _session;
        private Task _startTask, _stopTask;
        private TaskCompletionSource<bool> _startupFinished;
        private bool _stopping, _sessionStopped, _commandsJoined, _factoryReleased, _transcriptDisposed;
        private MNNResponseId _displayedResponse;
        public MNNRealtimeDeviceSession Devices { get; }
        public MNNConversationPorts Ports { get; }
        public MNNConversationCommands Commands { get; private set; }
        public MNNConversationTranscriptStore Transcript { get; }
        public MNNConversationStage Stage { get; private set; } = MNNConversationStage.Idle;
        public Exception Fault { get; private set; }
        public string Warning { get; private set; }
        public string RecognitionPreview { get; private set; }
        public bool IsReady => !_stopping && Devices.IsReady && _session?.State == MNNRealtimeSessionState.Running;
        public bool IsClosed { get; private set; }
        public bool TranscriptAvailable => !_transcriptDisposed;
        public Task Shutdown => _stopTask ?? Task.CompletedTask;
        public MNNConversationTelemetry Telemetry => _factory?.TurnTelemetry;
        public MNNConversationRuntimeDiagnostics Diagnostics => _factory?.Diagnostics;

        public MNNConversationApplication(MNNRealtimeDeviceBinding binding, MNNRealtimeCapabilities deviceCapabilities,
            MNNConversationRegistrations registrations, string transcriptDirectory,
            MNNConversationOptions options = null, MNNRealtimeDeviceSessionOptions deviceOptions = null)
        {
            _deviceCapabilities = deviceCapabilities ?? throw new ArgumentNullException(nameof(deviceCapabilities));
            _registrations = registrations ?? throw new ArgumentNullException(nameof(registrations));
            _options = options ?? new MNNConversationOptions();
            Devices = new MNNRealtimeDeviceSession(binding, deviceOptions);
            Transcript = new MNNConversationTranscriptStore(transcriptDirectory);
            Ports = new MNNConversationPorts(Devices.Capture, () => _session, () => Devices.SpeechMayBeAudible)
            {
                CaptureGate = Devices.SetCaptureEnabled,
                PauseCaptureAsync = Devices.PauseCaptureAsync,
                CaptureReady = () => Devices.IsReady
            };
        }

        public Task StartAsync()
        {
            if (_stopping) throw new InvalidOperationException("Create a fresh application after successful shutdown.");
            if (_startTask != null) return _startTask;
            var completion = Signal();
            _startTask = completion.Task;
            _startupFinished = Signal();
            _ = CompleteStartAsync(completion);
            return _startTask;
        }

        private async Task CompleteStartAsync(TaskCompletionSource<bool> completion)
        {
            Exception failure = null;
            try
            {
                Stage = MNNConversationStage.Loading;
                var output = await Devices.OpenAsync(_lifetime.Token);
                _lifetime.Token.ThrowIfCancellationRequested();
                _factory = new MNNConversationRuntimeFactory(Ports, _options, _registrations);
                _session = new MNNRealtimeSession(_factory.CreateFallback(output, _deviceCapabilities,
                    requireMeasuredSpeech: false), output);
                ConfigureCommands();
                await _session.StartAsync(_lifetime.Token);
                _lifetime.Token.ThrowIfCancellationRequested();
                Stage = MNNConversationStage.ModelReady;
            }
            catch (Exception error) { failure = error; }
            finally { _startupFinished.TrySetResult(true); }
            if (failure != null)
            {
                if (!(failure is OperationCanceledException)) Fault = failure;
                try { await StopAsync(); }
                catch (Exception cleanup) { failure = new AggregateException("Application startup and cleanup failed.", failure, cleanup); }
                if (failure is OperationCanceledException) completion.TrySetCanceled();
                else completion.TrySetException(failure);
            }
            else completion.TrySetResult(true);
        }

        private void ConfigureCommands()
        {
            var bindings = Ports.CreateCommandBindings();
            bindings.Active = () => !_stopping;
            bindings.Lifetime = () => _lifetime.Token;
            bindings.SupportsRealtime = () => _factory.Active != null;
            bindings.SupportsRecordedVoice = () => _factory.Active != null;
            bindings.SupportsTranscription = () => _registrations.Asr != null || _registrations.Transcription != null;
            bindings.CaptureTailMilliseconds = () => Math.Max(500, (int)Math.Ceiling(Devices.PresentationTailMilliseconds));
            bindings.ActivateRealtime = () => Ports.SetRealtime(true);
            bindings.SubmitAudioAsync = _factory.SubmitAudioAsync;
            bindings.SubmitTextAsync = _factory.SubmitTextAsync;
            bindings.TranscribeAsync = _factory.TranscribeAsync;
            bindings.BackgroundFailure = error => Fault = error;
            Commands = new MNNConversationCommands(bindings);
        }

        /// <summary>Bound notification consumption and physical presentation acknowledgement to one UI frame.</summary>
        public void Tick()
        {
            if (_stopping) return;
            Devices.Tick();
            Ports.CanInterruptSpeech = Devices.CanInterruptSpeech;
            if (Fault != null || Devices.Fault != null || _session?.Fault != null)
            {
                Fault = Fault ?? Devices.Fault ?? _session.Fault;
                _ = ObserveStopAsync();
                return;
            }
            for (int i = 0; i < 64 && Ports.TryRead(out var notification); ++i)
            {
                if (notification.Text == null) { Stage = notification.Stage; continue; }
                if (!notification.Assistant) Transcript.Add(MNNChatRole.User, notification.Text);
                else if (_displayedResponse != notification.Response)
                {
                    Transcript.Add(MNNChatRole.Assistant, notification.Text);
                    _displayedResponse = notification.Response;
                }
                else Transcript.UpdateLast(notification.Text);
            }
            Warning = Ports.TakeInputWarning() ?? Warning;
            RecognitionPreview = Ports.TakeRecognitionPreview() ?? RecognitionPreview;
            if (Ports.TakeRealtimeExit() && Commands != null) _ = ObserveExitAsync();
        }

        private async Task ObserveExitAsync()
        {
            try { await Commands.SetRealtimeAsync(false); }
            catch (Exception error) { Fault = error; await ObserveStopAsync(); }
        }
        private async Task ObserveStopAsync()
        {
            try { await StopAsync(); }
            catch (Exception error) { Fault = Combine(Fault, error); }
        }

        /// <summary>Silence and stop devices synchronously, then join startup/commands/runtime and release providers.
        /// Failed owners remain attached; call again to retry. A fresh instance owns the next conversation.</summary>
        public Task StopAsync()
        {
            if (IsClosed) return Task.CompletedTask;
            if (_stopTask != null && !_stopTask.IsCompleted) return _stopTask;
            _stopping = true;
            var completion = Signal(); _stopTask = completion.Task;
            var failures = new List<Exception>();
            Attempt(() => _lifetime.Cancel(), failures);
            Attempt(() => Ports.SetRealtime(false), failures);
            Attempt(() => Commands?.CancelRecording(), failures);
            Task deviceClose = Task.CompletedTask;
            Attempt(() => deviceClose = Devices.CloseAsync(), failures);
            _ = CompleteStopAsync(deviceClose, failures, completion);
            return _stopTask;
        }

        private async Task CompleteStopAsync(Task deviceClose, List<Exception> failures, TaskCompletionSource<bool> completion)
        {
            if (_startupFinished != null) await _startupFinished.Task;
            if (!_sessionStopped)
            {
                try { if (_session != null) await _session.StopAsync(); _sessionStopped = true; }
                catch (Exception error) { _sessionStopped = _session.ResourcesReleased; failures.Add(error); }
            }
            if (!_commandsJoined)
            {
                if (Commands != null)
                {
                    await JoinCommandAsync(Commands.PreparationFinished, failures);
                    await JoinCommandAsync(Commands.RecordingStarted, failures);
                    await JoinCommandAsync(Commands.CaptureFinished, failures);
                    await JoinCommandAsync(Commands.InterruptionFinished, failures);
                    await JoinCommandAsync(Commands.SubmissionFinished, failures);
                }
                _commandsJoined = true;
            }
            bool devicesClosed = false;
            try { await deviceClose; devicesClosed = true; }
            catch (Exception error) { failures.Add(error); }
            if (!_factoryReleased)
            {
                try { if (_factory != null) await _factory.ReleaseAsync(); _factoryReleased = true; }
                catch (Exception error) { failures.Add(error); }
            }
            if (!_transcriptDisposed) Attempt(() => { Transcript.Dispose(); _transcriptDisposed = true; }, failures);
            IsClosed = _sessionStopped && _commandsJoined && devicesClosed && _factoryReleased && _transcriptDisposed;
            if (IsClosed) _lifetime.Dispose();
            if (failures.Count == 0)
            {
                Stage = MNNConversationStage.Stopped;
                completion.TrySetResult(true);
            }
            else
            {
                var error = new AggregateException(IsClosed ? "Application closed after an operation fault; all resources were released." :
                    "Application shutdown failed; retained owners can be retried.", failures);
                Fault = Combine(Fault, error); Stage = MNNConversationStage.Error; completion.TrySetException(error);
            }
        }

        private static async Task JoinCommandAsync(Task task, List<Exception> failures)
        {
            try { await task; }
            catch (OperationCanceledException) { }
            catch (Exception error) { failures.Add(error); }
        }
        private static void Attempt(Action action, List<Exception> failures)
        { try { action(); } catch (Exception error) { failures.Add(error); } }
        private static TaskCompletionSource<bool> Signal() => new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private static Exception Combine(Exception first, Exception second) => first == null ? second : new AggregateException(first, second);
    }
}
