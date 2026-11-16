using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MNN.Unity
{
    /// <summary>Shared capability owner. Applications inject models/capture and supply presentation groups;
    /// no application-specific state machine, UI, microphone transport or model-name routing is required.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MNNPresenceComponent), typeof(MNNFaceCameraComponent))]
    [AddComponentMenu("MNN/Realtime/Multimodal Interaction")]
    public sealed class MNNInteractionComponent : MonoBehaviour
    {
        private MNNInteractionSettings _settings = new MNNInteractionSettings();
        private readonly object _gate = new object();
        private MNNPresenceSnapshot _snapshot;
        private MNNMultimodalSpeechProcessor _speech;
        private MNNPresenceComponent _presence;
        private MNNFaceCameraComponent _camera;
        private Func<float[], CancellationToken, MNNMultimodalSpeechInput> _captureInput;
        private double _clockSeconds;
        private long _clockTicks;
        public MNNVisionContext VisionContext { get; } = new MNNVisionContext();
        public MNNPresenceComponent Presence => _presence != null ? _presence : (_presence = GetComponent<MNNPresenceComponent>());
        public MNNFaceCameraComponent Camera => _camera != null ? _camera : (_camera = GetComponent<MNNFaceCameraComponent>());
        public MNNInteractionSettings ReadSettings() { lock (_gate) return _settings.Snapshot(); }
        public bool CanStartInteraction { get { lock (_gate) return !_settings.RequiresPresenceForVoice || _snapshot.CanStartInteraction; } }
        public event Action<MNNPresenceSnapshot> Changed;

        public void StartCamera(WebCamTexture camera, IMNNPresenceSource source) => Camera.StartTracking(camera, source);
        public Task StopCameraAsync() => Camera.StopAsync();

        public void Configure(MNNInteractionSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            settings.Validate();
            lock (_gate)
            {
                _settings = settings.Snapshot(); _snapshot = default; _speech = null; _captureInput = null;
                _clockSeconds = Time.realtimeSinceStartupAsDouble;
                _clockTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            }
            VisionContext.Clear();
            Presence.Configure(settings.ToPresenceOptions(), settings.faceRecognition ? settings.allowedIdentities : null);
            Presence.enabled = settings.visualPresence;
            Camera.enabled = settings.visualPresence;
            Publish(Presence.Current);
        }

        /// <summary>Model and synchronized capture ownership stays with the caller. Reconfigure after settings change.
        /// Capture must return the actual original timestamps/track; never stamp old audio as fresh at recognition time.</summary>
        public void ConfigureSpeech(IMNNTargetSpeechExtractor audioVisual, IMNNTargetSpeechExtractor reference,
            Func<MNNMultimodalSpeechInput, CancellationToken, MNNSeparatedAudio> separate,
            Func<MNNSeparatedAudio, string, int> select,
            Func<float[], CancellationToken, MNNMultimodalSpeechInput> captureInput)
        {
            lock (_gate)
            {
                if (_settings.audioVisualSpeech && audioVisual == null || _settings.referenceSpeaker && reference == null ||
                    _settings.separateSpeakers && (separate == null || select == null))
                    throw new InvalidOperationException("An enabled target capability has no ready provider.");
                _captureInput = captureInput ?? throw new ArgumentNullException(nameof(captureInput));
                _speech = MNNMultimodalSpeechModels.CreateFromProviders(_settings.audioVisualSpeech ? audioVisual : null,
                    _settings.referenceSpeaker ? reference : null, _settings.separateSpeakers ? separate : null,
                    _settings.separateSpeakers ? select : null, _settings.RequiresPresenceForVoice);
            }
        }

        /// <summary>Recognition-worker entry. Null rejects a turn before ASR; a missing enabled provider is an error.</summary>
        public Task<float[]> PrepareVoiceAsync(float[] samples, CancellationToken token)
        {
            MNNMultimodalSpeechProcessor speech;
            Func<float[], CancellationToken, MNNMultimodalSpeechInput> capture;
            MNNPresenceSnapshot snapshot;
            MNNInteractionSettings settings;
            double now;
            lock (_gate)
            {
                speech = _speech; capture = _captureInput; snapshot = _snapshot; settings = _settings;
                now = _clockSeconds + (double)(System.Diagnostics.Stopwatch.GetTimestamp() - _clockTicks) / System.Diagnostics.Stopwatch.Frequency;
            }
            token.ThrowIfCancellationRequested();
            if (settings.RequiresPresenceForVoice && (!snapshot.CanStartInteraction || now - snapshot.EvaluatedAt > .75)) return Task.FromResult<float[]>(null);
            if (!settings.audioVisualSpeech && !settings.referenceSpeaker && !settings.separateSpeakers) return Task.FromResult(samples);
            if (speech == null || capture == null) throw new InvalidOperationException("Configure synchronized speech providers before enabling target pickup.");
            var input = capture(samples, token);
            // The capture adapter supplies a finished block; presence tick time and input clock must be the same.
            var result = speech.Process(input, snapshot, now, token);
            return Task.FromResult(result.Route == MNNMultimodalSpeechRoute.Rejected ? null : result.Audio);
        }

        private void OnEnable()
        {
            Presence.Changed += Publish;
            Camera.Observation += Observe;
            Camera.FrameCaptured += CaptureImage;
            Publish(Presence.Current);
        }
        private void Observe(MNNPresenceObservation observation)
        {
            Presence.Report(observation);
        }
        private void CaptureImage(MNNRgbImage image, double capturedAt)
        {
            lock (_gate)
                if (_settings.imageContext && _snapshot.CanStartInteraction)
                    VisionContext.Update(image, capturedAt, _snapshot.TrackId);
        }
        private void Update() { if (Presence.enabled) Publish(Presence.Current); }
        private void Publish(MNNPresenceSnapshot snapshot)
        {
            bool changed;
            lock (_gate)
            {
                changed = _snapshot.State != snapshot.State || _snapshot.TrackId != snapshot.TrackId ||
                    _snapshot.CanStartInteraction != snapshot.CanStartInteraction;
                _snapshot = snapshot;
            }
            if (!snapshot.CanStartInteraction) VisionContext.Clear();
            if (changed) Changed?.Invoke(snapshot);
        }
        private void OnDisable()
        {
            Presence.Changed -= Publish;
            Camera.Observation -= Observe;
            Camera.FrameCaptured -= CaptureImage;
            lock (_gate) { _snapshot = default; _speech = null; _captureInput = null; }
            VisionContext.Clear();
        }
    }
}
