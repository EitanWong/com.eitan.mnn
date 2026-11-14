using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MNN.Unity;

namespace MNN.Unity
{

    /// <summary>Conversation ports over the shared OpenAI HTTP, WAV, SSE and PCM services.</summary>
    public sealed class MNNOpenAIConversationProvider : IMNNConversationTextAssistanceProvider, IMNNConversationAsrProvider, IMNNConversationTtsProvider, IMNNConversationVoiceIntentProvider, IMNNConversationSpeechLanguages
    {
        private readonly MNNOpenAIClient _client;
        private readonly MNNOpenAIConversationOptions _settings;
        private readonly int _maximumTokens, _maximumCharacters;
        private readonly float _temperature;
        public MNNRealtimeCapabilities Capabilities { get; }
        public int SampleRate => MNNOpenAIClient.SpeechSampleRate;
        public IReadOnlyList<string> SupportedSpeechLanguages => Array.AsReadOnly(_settings.SpeechLanguages);
        public static MNNRealtimeCapabilities Describe(MNNOpenAIConversationOptions config, MNNOpenAIConversationStage stage) =>
            new MNNRealtimeCapabilities(MNNRealtimeFeature.BoundedCancellation |
                (stage == MNNOpenAIConversationStage.Tts
                    ? MNNRealtimeFeature.IndependentTts |
                      (config.SupportsProgressivePcm ? MNNRealtimeFeature.StreamingSpeech : MNNRealtimeFeature.None)
                    : stage == MNNOpenAIConversationStage.Asr
                        ? MNNRealtimeFeature.MemoryAudioInput | MNNRealtimeFeature.SpeechRecognition
                        : MNNRealtimeFeature.TextInput | MNNRealtimeFeature.StableSpokenText | MNNRealtimeFeature.ContextRecovery |
                          (config.AcceptsAudioInput ? MNNRealtimeFeature.MemoryAudioInput : MNNRealtimeFeature.None)),
                "OpenAI-compatible HTTP v1 / SSE / PCM16 24kHz",
                stage == MNNOpenAIConversationStage.Tts && config.SupportsProgressivePcm
                    ? "Endpoint progressive PCM capability was explicitly enabled; qualify it against the deployed service and model."
                    : "Managed transport with bounded payloads and cancellation; progressive synthesis is not assumed.",
                "Request/response audio input; no continuous understanding. Network throughput is unmeasured.",
                execution: MNNRealtimeExecution.RemoteApi);
        /// <param name="client">Optional borrowed transport; its owner must disable automatic redirects.</param>
        public MNNOpenAIConversationProvider(MNNOpenAIConversationOptions settings, string key, MNNOpenAIConversationStage stage,
            System.Net.Http.HttpClient client = null)
        {
            if (!Enum.IsDefined(typeof(MNNOpenAIConversationStage), stage)) throw new ArgumentOutOfRangeException(nameof(stage));
            _settings = (settings ?? throw new ArgumentNullException(nameof(settings))).Snapshot();
            _maximumTokens = _settings.MaximumTokens;
            _maximumCharacters = _settings.MaximumCharacters;
            _temperature = _settings.Temperature;
            Capabilities = Describe(_settings, stage);
            _client = new MNNOpenAIClient(_settings.Resolve(), key, TimeSpan.FromSeconds(_settings.TimeoutSeconds), client);
        }
        public Task PrepareAsync(CancellationToken token) { token.ThrowIfCancellationRequested(); return Task.CompletedTask; }
        public Task GenerateAsync(IReadOnlyList<MNNOpenAIMessage> history, float[] audio, int sampleRate,
            Func<string, Task> textDelta, CancellationToken token) =>
            _client.GenerateTextAsync(_settings.Model, history, textDelta, token, audio, sampleRate,
                _maximumCharacters, _maximumTokens, _temperature);
        public Task<string> TranscribeAsync(float[] audio, int sampleRate, CancellationToken token) =>
            _client.TranscribeAsync(_settings.Model, audio, sampleRate, token);
        public Task ClassifyVoiceIntentAsync(IReadOnlyList<MNNOpenAIMessage> prompt, Func<string, Task> textDelta, CancellationToken token) =>
            _client.GenerateTextAsync(_settings.Model, prompt, textDelta, token, maximumCharacters: 512, maximumTokens: 128, temperature: 0);
        public Task GenerateAssistanceAsync(MNNConversationTextTask task, IReadOnlyList<MNNOpenAIMessage> prompt,
            Func<string, Task> textDelta, CancellationToken token) =>
            _client.GenerateTextAsync(_settings.Model, prompt, textDelta, token, maximumCharacters: Math.Min(512, _maximumCharacters),
                maximumTokens: Math.Min(_maximumTokens, MNNConversationTextAssistance.MaximumTokens(task)), temperature: .2f);
        public Task SynthesizeAsync(string text, Func<float[], int, Task> pcm, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            string spoken = MNNSpeechTextCleaner.Clean(text);
            if (string.IsNullOrEmpty(spoken))
            {
                int silenceFrames = Math.Max(1, SampleRate / 100);
                return pcm(new float[silenceFrames], silenceFrames);
            }
            return _client.SynthesizeAsync(_settings.Model, _settings.Voice, spoken, pcm, token);
        }
        public void Dispose() => _client.Dispose();
    }
}
