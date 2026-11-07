using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MNN.Unity
{
    /// <summary>Unity microphone/AudioSource primitives for the shared device session. Contains no application preferences or UI policy.</summary>
    public sealed class MNNUnityAudioDeviceBinding : MNNRealtimeDeviceBinding
    {
        private readonly MNNUnityMicrophoneCapture _microphone;
        private readonly MNNRealtimeAudioDevice _playback;
        private readonly Func<bool> _active;
        private AudioSource _source;
        private AudioClip _speakerClip;
        private MNNSpeechOutputFilter _filter;
        public string MicrophoneDevice { get; set; }
        public int MaximumInputSampleRate { get; set; } = 96000;
        public override IMNNAudioCaptureSource Capture => _playback;
        public override MNNPcmFormat OutputFormat => new MNNPcmFormat(AudioSettings.outputSampleRate, 1);
        public override MNNPlaybackWaveform Waveform => _filter?.Waveform;
        public override bool SpeechMayBeAudible => _filter?.MayBeAudible == true;
        public override Exception Fault => _microphone?.Fault ?? _playback?.Fault;
        public override float? SpeechVolume => _source != null ? _source.volume : (float?)null;
        public override int ReferenceBlockFrames { get { AudioSettings.GetDSPBufferSize(out int frames, out _); return frames * 2; } }
        public MNNUnityAudioDeviceBinding(MNNUnityMicrophoneCapture microphone, MNNRealtimeAudioDevice playback, Func<bool> active = null)
        { _microphone = microphone; _playback = playback; _active = active; }
        private bool Active => (_active?.Invoke() ?? true) && _microphone != null && _microphone.isActiveAndEnabled && _playback != null && _playback.isActiveAndEnabled;
        public override void ValidateFresh()
        {
            if (!Active) throw new InvalidOperationException("Assign active Unity microphone and playback components.");
            if (_microphone.gameObject == _playback.gameObject) throw new ArgumentException("Use separate microphone and playback objects.");
            _source = _playback.GetComponent<AudioSource>();
            if (_source == null || _source.isPlaying || _source.clip != null || _source.mute || _source.volume <= 0 ||
                _source.spatialBlend != 0 || _source.pitch != 1 || _microphone.IsCapturing || _microphone.IsWaitingForBuffer || _playback.IsOutputRunning)
                throw new InvalidOperationException("Use exclusive, stopped, audible 2D audio endpoints at pitch 1.");
            if (MaximumInputSampleRate < 8000 || MaximumInputSampleRate > 192000) throw new ArgumentOutOfRangeException(nameof(MaximumInputSampleRate));
        }
        public override async Task RequestPermissionAsync(CancellationToken token)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            throw new PlatformNotSupportedException("Unity microphone capture requires a browser-specific implementation on WebGL.");
#else
            var permission = Application.RequestUserAuthorization(UserAuthorization.Microphone);
            while (!permission.isDone)
            {
                token.ThrowIfCancellationRequested();
                if (!Active) throw new OperationCanceledException("Audio was disabled while requesting microphone permission.", token);
                await Task.Delay(10, token);
            }
            token.ThrowIfCancellationRequested();
            if (!Application.HasUserAuthorization(UserAuthorization.Microphone)) throw new UnauthorizedAccessException("Microphone permission was not granted.");
#endif
        }
        public override void StartCapture(Action<MNNPcmFormat, int> negotiated) =>
            _microphone.StartCaptureNegotiated(_playback, negotiated, string.IsNullOrWhiteSpace(MicrophoneDevice) ? null : MicrophoneDevice,
                Math.Min(MaximumInputSampleRate, AudioSettings.outputSampleRate), 10, 0);
        public override void BindOutput(MNNRealtimeOutputPipeline output, MNNPcmFormat captureFormat, int capacity)
        {
            _playback.Bind(output, captureFormat, Math.Max(2, captureFormat.SampleRate / (capacity / captureFormat.Channels)), capacity);
            _boundOutput = output;
        }
        public override void StartPlayback()
        {
            if (!_playback.IsOutputRunning)
            {
                _filter = _playback.GetComponent<MNNSpeechOutputFilter>() ?? _playback.gameObject.AddComponent<MNNSpeechOutputFilter>();
                _filter.Configure(GetBoundOutput(), _playback.GetUnityDspBufferMilliseconds());
                _playback.StartOutput(); _speakerClip = _source.clip;
            }
            else _source.Play();
        }
        private MNNRealtimeOutputPipeline _boundOutput;
        private MNNRealtimeOutputPipeline GetBoundOutput() => _boundOutput ?? throw new InvalidOperationException("Bind output before starting playback.");
        public override void SetCaptureEnabled(bool enabled) { if (_playback != null) _playback.CaptureEnabled = enabled; }
        public override Task FlushCaptureAsync(CancellationToken token) => _microphone.FlushCaptureAsync(token);
        public override IDisposable BindEchoReference(MNNEchoCancellationWorker worker)
        {
            AudioListener listener = null;
            foreach (var candidate in UnityEngine.Object.FindObjectsOfType<AudioListener>())
                if (candidate.isActiveAndEnabled)
                { if (listener != null) throw new InvalidOperationException("Echo reference requires exactly one active AudioListener."); listener = candidate; }
            if (listener == null) throw new InvalidOperationException("Add an active AudioListener before using Unity echo processing.");
            var tap = listener.GetComponent<MNNUnityEchoReferenceTap>() ?? listener.gameObject.AddComponent<MNNUnityEchoReferenceTap>();
            tap.Bind(worker, OutputFormat.SampleRate, _playback.GetUnityDspBufferMilliseconds());
            return new ReferenceLease(tap, worker);
        }
        public override MNNRealtimeDeviceSnapshot ReadState()
        {
            bool source = _source != null;
            bool matches = source && _source.spatialBlend == 0 && _source.pitch == 1 &&
                (_speakerClip == null || _source.clip == _speakerClip);
            return new MNNRealtimeDeviceSnapshot(Active, _microphone != null && _microphone.isActiveAndEnabled,
                _microphone?.IsCapturing == true, _microphone?.IsWaitingForBuffer == true,
                _playback != null && _playback.isActiveAndEnabled, _playback?.IsOutputRunning == true,
                source && _source.isPlaying, source && !_source.mute && _source.volume > 0, matches, OutputFormat,
                _playback?.RenderedOutputFrames ?? 0, _playback != null ? _playback.GetUnityDspBufferMilliseconds() : 0,
                source ? _source.volume : 0, source && _speakerClip != null && _source.clip == _speakerClip && _source.loop && _source.isActiveAndEnabled,
                AudioListener.pause, Fault);
        }
        public override void SetPresentationTail(double milliseconds) => _filter?.SetPresentationTail(milliseconds);
        public override Task FadeOutAsync() => _filter?.FadeOutAsync() ?? Task.CompletedTask;
        public override void SetSpeechVolume(float volume)
        { if (_source == null) throw new InvalidOperationException("Assistant playback is not open."); _source.volume = volume; }
        public override void StopCapture() { if (_microphone != null) _microphone.StopCapture(); }
        public override void StopPlayback() { if (_playback != null) _playback.StopOutput(); }
        public override void ResetForRetry() { _boundOutput = null; _speakerClip = null; _filter = null; _source = null; }
        private sealed class ReferenceLease : IDisposable
        {
            private MNNUnityEchoReferenceTap _tap; private readonly MNNEchoCancellationWorker _worker;
            internal ReferenceLease(MNNUnityEchoReferenceTap tap, MNNEchoCancellationWorker worker) { _tap = tap; _worker = worker; }
            public void Dispose() { if (_tap != null) _tap.Detach(_worker); _tap = null; }
        }
    }
}
