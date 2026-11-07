using System;
using System.IO;
using System.Threading.Tasks;
using MNN.Unity;
using UnityEngine;

namespace MNN.Examples
{
    /// <summary>Independent microphone-to-reply sample. Inspector model directories, built-in GUI and Unity audio only.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("MNN/Examples/Standalone Conversation Example")]
    public sealed class MNNStandaloneConversationExample : MonoBehaviour
    {
        [Tooltip("Absolute directory, or relative to StreamingAssets/MNN/Models. No models are downloaded.")]
        [SerializeField] private string modelDirectory;
        [SerializeField] private string recognitionDirectory;
        [SerializeField] private string speechDirectory;
        [SerializeField] private string microphoneDevice;
        [SerializeField] private MNNUnityMicrophoneCapture microphone;
        [SerializeField] private MNNRealtimeAudioDevice playback;
        [SerializeField] private bool showInterface = true;
        private MNNStandaloneConversation _application;
        private string _draft = "Hello", _error;
        private bool _busy;
        private Vector2 _scroll;
        public MNNStandaloneConversation ApplicationOwner => _application;
        public Task Shutdown => _application?.Shutdown ?? Task.CompletedTask;

        /// <summary>Also usable by a custom UI; resolves existing local model directories without SiriAI.</summary>
        public async Task StartConversationAsync()
        {
            if (_application != null && !_application.IsClosed)
                throw new InvalidOperationException("Close the previous application successfully before starting again.");
            string model = ResolveDirectory(modelDirectory), recognition = ResolveDirectory(recognitionDirectory), speech = ResolveDirectory(speechDirectory);
            EnsureAudioEndpoints();
            var binding = new MNNUnityAudioDeviceBinding(microphone, playback, () => this != null && isActiveAndEnabled)
            { MicrophoneDevice = microphoneDevice };
            _application = new MNNStandaloneConversation(binding, new MNNRealtimeCapabilities(
                MNNRealtimeFeature.DuplexDevice | MNNRealtimeFeature.PresentationClock | MNNRealtimeFeature.HalfDuplexDevice,
                "Unity sample audio v1", "Shared Unity microphone/AudioSource binding with supervised estimated presentation receipts.",
                "Half-duplex; estimated device tail. No acoustic echo control or throughput qualification.", execution: MNNRealtimeExecution.Managed),
                MNNConversationExample.CreateLocalRegistrations(model, recognition, speech), TranscriptDirectory());
            await _application.StartAsync();
        }

        public Task StopConversationAsync() => _application?.StopAsync() ?? Task.CompletedTask;
        public Task SendTextAsync(string text) => RequireReady().Commands.SendTextAsync(text);
        public Task SetRealtimeAsync(bool enabled) => RequireReady().Commands.SetRealtimeAsync(enabled);
        public Task<bool> BeginRecordingAsync() => RequireReady().Commands.BeginRecordingAsync();
        public Task<string> FinishRecordingAsync(bool transcribeOnly = false) => RequireReady().Commands.FinishRecordingAsync(transcribeOnly);

        private MNNStandaloneConversation RequireReady()
        {
            if (_application?.IsReady != true) throw new InvalidOperationException("Start the conversation and wait for its models and devices.");
            return _application;
        }
        private static string ResolveDirectory(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Set all three installed model directories in the Inspector.");
            string path = Path.GetFullPath(Path.IsPathRooted(directory) ? directory :
                Path.Combine(UnityEngine.Application.streamingAssetsPath, "MNN", "Models", directory));
            if (!Directory.Exists(path)) throw new DirectoryNotFoundException("Installed model directory not found: " + path);
            return path;
        }
        private static string TranscriptDirectory()
        {
#if UNITY_EDITOR
            return Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "TestArtifacts~", "StandaloneConversation", "Transcript"));
#else
            return Path.Combine(UnityEngine.Application.temporaryCachePath, "MNN", "Conversation");
#endif
        }

        private void EnsureAudioEndpoints()
        {
            int listeners = 0;
            foreach (var listener in FindObjectsOfType<AudioListener>()) if (listener.isActiveAndEnabled) ++listeners;
            if (listeners > 1) throw new InvalidOperationException("Use exactly one active AudioListener for the example.");
            if (listeners == 0) new GameObject("Conversation Listener", typeof(AudioListener)).transform.SetParent(transform, false);
            if (microphone == null)
            {
                var input = new GameObject("Conversation Microphone"); input.transform.SetParent(transform, false);
                microphone = input.AddComponent<MNNUnityMicrophoneCapture>();
            }
            if (playback == null)
            {
                var output = new GameObject("Conversation Speaker"); output.transform.SetParent(transform, false);
                var source = output.AddComponent<AudioSource>();
                source.playOnAwake = false; source.spatialBlend = 0; source.pitch = 1; source.volume = 1;
                playback = output.AddComponent<MNNRealtimeAudioDevice>();
            }
        }

        private void Update() => _application?.Tick();
        private void OnDisable() => Observe(StopConversationAsync);
        private void OnDestroy() => Observe(StopConversationAsync);

        private async void Observe(Func<Task> operation)
        {
            try { await operation(); }
            catch (OperationCanceledException) { }
            catch (Exception error) { _error = error.Message; Debug.LogException(error); }
        }
        private async Task RunAsync(Func<Task> operation)
        {
            if (_busy) return;
            _busy = true; _error = null;
            try { await operation(); }
            finally { _busy = false; }
        }
        private async Task FinishDraftAsync()
        {
            string text = await FinishRecordingAsync(true);
            if (text != null) _draft = text;
        }

        private void OnGUI()
        {
            if (!showInterface) return;
            GUILayout.BeginArea(new Rect(16, 16, Mathf.Min(760, Screen.width - 32), Mathf.Max(100, Screen.height - 32)), GUI.skin.box);
            GUILayout.Label("MNN Standalone Conversation · Unity audio · buffered half-duplex");
            GUILayout.Label(_application?.Stage.ToString() ?? "Set three model directories in Inspector, then Start.");
            string error = _error ?? _application?.Fault?.Message;
            if (!string.IsNullOrEmpty(error)) GUILayout.Label(error);
            if (!string.IsNullOrEmpty(_application?.Warning)) GUILayout.Label(_application.Warning);
            GUI.enabled = !_busy;
            GUILayout.BeginHorizontal();
            GUI.enabled = !_busy && (_application == null || _application.IsClosed);
            if (GUILayout.Button("Start")) Observe(() => RunAsync(StartConversationAsync));
            GUI.enabled = _application != null && !_application.IsClosed;
            if (GUILayout.Button("Close / retry cleanup")) Observe(StopConversationAsync);
            GUILayout.EndHorizontal();
            bool ready = !_busy && _application?.IsReady == true;
            GUI.enabled = ready;
            _draft = GUILayout.TextArea(_draft, 4096, GUILayout.Height(64));
            if (GUILayout.Button("Send text")) Observe(() => RunAsync(() => SendTextAsync(_draft)));
            GUILayout.BeginHorizontal();
            bool recording = _application?.Ports.Recording == true;
            if (GUILayout.Button(recording ? "Send recording" : "Start recording"))
                Observe(() => RunAsync(recording ? (Func<Task>)(async () => await FinishRecordingAsync()) : async () => { await BeginRecordingAsync(); }));
            GUI.enabled = ready && recording;
            if (GUILayout.Button("Voice to draft")) Observe(() => RunAsync(FinishDraftAsync));
            GUI.enabled = ready;
            bool realtime = _application?.Commands?.IsRealtime == true;
            if (GUILayout.Button(realtime ? "Stop listening" : "Continuous listening")) Observe(() => RunAsync(() => SetRealtimeAsync(!realtime)));
            GUILayout.EndHorizontal();
            GUI.enabled = true;
            if (!string.IsNullOrEmpty(_application?.RecognitionPreview)) GUILayout.Label(_application.RecognitionPreview);
            _scroll = GUILayout.BeginScrollView(_scroll);
            if (_application?.TranscriptAvailable == true)
            {
                var transcript = _application.Transcript;
                // The small sample draws the latest page; shared storage owns the complete bounded-resident history.
                for (int i = Math.Max(0, transcript.Count - 32); i < transcript.Count; ++i)
                    GUILayout.Label(transcript[i].Role + ": " + transcript[i].Text);
            }
            GUILayout.EndScrollView(); GUILayout.EndArea();
        }
    }
}
