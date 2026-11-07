using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace MNN.Unity.Editor
{
    /// <summary>Editor device/lifecycle adapter. All conversation policy belongs to MNNConversationRuntime.</summary>
    internal sealed class MNNStudioRealtimeConversation
    {
        private readonly MNNConversationProviders _providers;
        private readonly MNNConversationOptions _options;
        private readonly List<MNNOpenAIMessage> _history;
        private readonly Task _previousRelease;
        private readonly Task _start;
        private Task _stop;
        private GameObject _deviceObject;
        private MNNRealtimeAudioDevice _device;
        private MNNUnityMicrophoneCapture _microphone;
        private MNNRealtimeOutputPipeline _output;
        private MNNRealtimeSession _session;
        private MNNConversationRuntime _runtime;
        private MNNConversationPorts _ports;
        private long _audibleUntil;
        private bool _stopping, _closed, _muted;
        internal bool Closed => _closed;
        internal string Error { get; private set; }
        internal bool Ready => !_stopping && _start.IsCompleted && !_start.IsFaulted && _session?.State == MNNRealtimeSessionState.Running;
        internal bool IsResponding => _runtime?.IsResponding ?? false;
        internal bool SpeakerAudible => (_output?.QueuedFrames ?? 0) > 0 || Stopwatch.GetTimestamp() < Interlocked.Read(ref _audibleUntil);
        internal bool ReplyPresented => _output != null && !IsResponding && _output.CurrentResponse.IsValid && _output.CanBeginNextResponse(_output.CurrentResponse);
        internal float Level => _microphone != null && !_muted ? _microphone.LastBlockRms * 4 : 0;
        internal string Diagnostics => _runtime?.TurnTelemetry.Summary ?? "Preparing voice models…";

        internal MNNStudioRealtimeConversation(MNNConversationProviders providers, MNNConversationOptions options,
            List<MNNOpenAIMessage> history, Task previousRelease)
        {
            _providers = providers.Snapshot();
            _options = options.Snapshot();
            _history = history;
            _previousRelease = previousRelease;
            // Keep assemblies/resources alive until the asynchronous producer join and release complete,
            // including when the window closes while a native model is still loading.
            EditorApplication.LockReloadAssemblies();
            EditorApplication.update += Tick;
            EditorApplication.quitting += BeforeQuit;
            _start = StartCoreAsync();
        }

        private async Task StartCoreAsync()
        {
            await _previousRelease;
            RequireActive();
            if (MNNStudioAudio.EditorAudioMuted)
                throw new InvalidOperationException("Unity Editor audio is muted. Enable Editor audio before starting voice.");
            var permission = Application.RequestUserAuthorization(UserAuthorization.Microphone);
            while (!permission.isDone) { RequireActive(); await Task.Delay(10); }
            RequireActive();
            if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
                throw new UnauthorizedAccessException("Microphone permission was not granted.");
            _deviceObject = new GameObject("MNN Studio Voice") { hideFlags = HideFlags.HideAndDontSave };
            _device = _deviceObject.AddComponent<MNNRealtimeAudioDevice>();
            _microphone = _deviceObject.AddComponent<MNNUnityMicrophoneCapture>();
            _device.CaptureEnabled = false;
            _microphone.StartCaptureNegotiated(_device, (format, capacity) =>
            {
                int rate = AudioSettings.outputSampleRate;
                _output = new MNNRealtimeOutputPipeline(new MNNPcmFormat(rate, 1), Math.Max(1, rate / 100), blockCapacity: 128);
                _device.Bind(_output, format, captureQueueBlocks: 64, maximumCaptureSamplesPerBlock: capacity);
                _device.CaptureEnabled = false;
            }, requestedSampleRate: Math.Min(96000, AudioSettings.outputSampleRate), clipLengthSeconds: 10, minimumDelayMilliseconds: 0);
            _ports = new MNNConversationPorts(_device, () => _session, () => SpeakerAudible);
            _runtime = new MNNConversationRuntime(_ports, _options, _output, MNNRealtimeRoute.CascadedSpeech, _providers, _history);
            _session = new MNNRealtimeSession(_runtime, _output);
            var deviceCapabilities = new MNNRealtimeCapabilities(MNNRealtimeFeature.DuplexDevice |
                MNNRealtimeFeature.HalfDuplexDevice | MNNRealtimeFeature.PresentationClock,
                "Unity Editor PCM device", "Shared Runtime capture and DSP callback; presentation uses estimated Unity DSP buffering.",
                execution: MNNRealtimeExecution.Managed);
            MNNRealtimePreflight.Check(MNNRealtimeRoute.CascadedSpeech, _providers.Model.Capabilities,
                deviceCapabilities, _providers.Tts.Capabilities, _providers.Asr.Capabilities,
                MNNConversationMnnProviders.EnergyCapabilities, requireMeasuredSpeech: false, allowBufferedSpeech: true).RequireSupported();
            _device.StartOutput();
            await _session.StartAsync();
            RequireActive();
            _ports.SetRealtime(!_muted);
            _device.CaptureEnabled = !_muted;
        }

        internal bool TryRead(out MNNConversationNotification notification)
        {
            if (_ports != null) return _ports.TryRead(out notification);
            notification = default;
            return false;
        }
        internal string TakeWarning() => _ports?.TakeInputWarning();
        internal bool TakeExit() => _ports?.TakeRealtimeExit() ?? false;
        internal void SetMuted(bool muted)
        {
            _muted = muted;
            _ports?.SetRealtime(!muted && Ready);
            if (_device != null) _device.CaptureEnabled = !muted && Ready;
        }
        internal Task InterruptAsync() => _session?.InterruptAsync() ?? Task.CompletedTask;

        private void Tick()
        {
            if (_closed) return;
            if (!_stopping)
            {
                try
                {
                    _microphone?.PollCapture(Time.realtimeSinceStartup, Stopwatch.GetTimestamp());
                    if (_microphone?.Fault != null) throw _microphone.Fault;
                    if (_device?.Fault != null) throw _device.Fault;
                    if (_session?.Fault != null) throw _session.Fault;
                    if (_start.IsFaulted) throw _start.Exception.GetBaseException();
                    if (_output != null && _device != null)
                    {
                        if (AudioSettings.outputSampleRate != _output.Format.SampleRate)
                            throw new InvalidOperationException("The audio output route changed. Restart voice to negotiate its new sample rate.");
                        long tail = (long)(_device.GetUnityDspBufferMilliseconds() * _output.Format.SampleRate / 1000);
                        _output.DrainReceipts(256);
                        _output.AcknowledgePresentation(Math.Max(0, _device.RenderedOutputFrames - tail), estimated: true);
                        if (_output.QueuedFrames > 0)
                            Interlocked.Exchange(ref _audibleUntil, Stopwatch.GetTimestamp() +
                                (long)((tail / (double)_output.Format.SampleRate + .1) * Stopwatch.Frequency));
                    }
                }
                catch (Exception error) { Error = error.GetBaseException().Message; Stop(); }
            }
            if (_stop == null || !_stop.IsCompleted) return;
            if (_stop.IsFaulted)
            {
                Error = _stop.Exception.GetBaseException().Message;
                // Retain live resources/reload protection after a failed join; Stop can retry.
                return;
            }
            _closed = true;
            if (_deviceObject != null) UnityEngine.Object.DestroyImmediate(_deviceObject);
            _deviceObject = null;
            EditorApplication.update -= Tick;
            EditorApplication.quitting -= BeforeQuit;
            EditorApplication.UnlockReloadAssemblies();
        }

        internal Task Stop()
        {
            if (_stop != null && (!_stop.IsCompleted || !_stop.IsFaulted)) return _stop;
            _stopping = true;
            _ports?.SetRealtime(false);
            if (_device != null) _device.CaptureEnabled = false;
            _output?.Interrupt();
            Exception deviceError = null;
            try { _microphone?.StopCapture(); }
            catch (Exception error) { deviceError = error; }
            try { _device?.StopOutput(); }
            catch (Exception error) { deviceError = deviceError == null ? error : new AggregateException(deviceError, error); }
            _stop = StopCoreAsync(deviceError, _session?.StopAsync());
            return _stop;
        }
        private async Task StopCoreAsync(Exception deviceError, Task shutdown)
        {
            try { await _start; }
            catch (Exception error) when (error is OperationCanceledException || _start.IsFaulted) { }
            if (_session != null) await (shutdown ?? _session.StopAsync()).ConfigureAwait(false);
            else await Task.Run(() => ReleaseUntransferredProviders()).ConfigureAwait(false);
            if (deviceError != null) throw deviceError;
        }
        private void ReleaseUntransferredProviders()
        {
            var unique = new HashSet<IMNNConversationProvider>(MNNConversationProviders.Identity);
            List<Exception> errors = null;
            foreach (var provider in new[] { _providers.Model, _providers.Tts, _providers.Asr, _providers.Vad, _providers.Turn })
            {
                if (provider == null || !unique.Add(provider)) continue;
                try { _providers.Dispose(provider); }
                catch (Exception error) { (errors ?? (errors = new List<Exception>())).Add(error); }
            }
            if (errors != null) throw new AggregateException(errors);
        }
        private void RequireActive()
        {
            if (_stopping) throw new OperationCanceledException("Studio voice startup was stopped.");
        }
        private void BeforeQuit() { Stop(); }
    }
}
