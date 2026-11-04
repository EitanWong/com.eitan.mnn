using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    /// <summary>Local adapters over existing MNN task APIs. Loading and inference execute off the UI thread.</summary>
    public static class MNNConversationMnnProviders
    {
        public static IMNNConversationTextProvider Language(string directory, int threads = 4, int maximumTokens = 256,
            bool thinking = false, string cacheDirectory = null) => new LanguageProvider(directory, threads, maximumTokens, thinking, cacheDirectory);
        public static IMNNConversationAsrProvider Recognition(string directory, int threads = 1, string language = null) => new RecognitionProvider(directory, threads, language);
        public static IMNNConversationTtsProvider Speech(string directory, MNNSpeechSynthesisOptions options = null, int threads = 4) => new SpeechProvider(directory, options, threads);
        public static MNNRealtimeCapabilities EnergyCapabilities { get; } = new MNNRealtimeCapabilities(
            MNNRealtimeFeature.MemoryAudioInput | MNNRealtimeFeature.VoiceActivityDetection | MNNRealtimeFeature.BoundedScheduling,
            "MNN managed energy VAD v1", "Fixed 512-sample capture worker frames.", execution: MNNRealtimeExecution.Managed);
        private static MNNRealtimeCapabilities Capabilities(MNNRealtimeFeature features) => new MNNRealtimeCapabilities(
            features | (MNNPlatformSupport.IsSupported ? MNNRealtimeFeature.SupportedAbi | MNNRealtimeFeature.InProcessMnn : 0),
            MNNPlatformSupport.AbiContract + " / conversation task adapter", "Existing MNN managed task APIs.",
            "Turn-based model operations. Buffered speech; cancellation between native operations; realtime throughput is unverified.");
        private abstract class Provider : IMNNConversationProvider
        {
            protected readonly string Directory;
            protected readonly int Threads;
            private readonly object _gate = new object();
            private bool _prepared, _disposed;
            public MNNRealtimeCapabilities Capabilities { get; }
            protected Provider(string directory, int threads, MNNRealtimeFeature features)
            {
                if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Choose a model directory.", nameof(directory));
                if (threads < 1 || threads > 64) throw new ArgumentOutOfRangeException(nameof(threads));
                Directory = directory; Threads = threads; Capabilities = MNNConversationMnnProviders.Capabilities(features);
            }
            public Task PrepareAsync(CancellationToken token) => Task.Run(() =>
            {
                lock (_gate)
                {
                    token.ThrowIfCancellationRequested();
                    if (_disposed) throw new ObjectDisposedException(GetType().Name);
                    if (_prepared) return;
                    try { Load(); token.ThrowIfCancellationRequested(); _prepared = true; }
                    catch { Release(); throw; }
                }
            }, token);
            protected abstract void Load();
            protected abstract void Release();
            public void Dispose() { lock (_gate) { if (_disposed) return; Release(); _disposed = true; } }
        }
        private sealed class LanguageProvider : Provider, IMNNConversationTextProvider, IMNNConversationVoiceIntentProvider
        {
            private MNNLlm _model;
            private readonly int _tokens;
            private readonly bool _thinking;
            private readonly string _cache;
            internal LanguageProvider(string directory, int threads, int tokens, bool thinking, string cache) : base(directory, threads,
                MNNRealtimeFeature.TextInput | MNNRealtimeFeature.ContextRecovery | MNNRealtimeFeature.StableSpokenText)
            { if (tokens < 1 || tokens > 8192) throw new ArgumentOutOfRangeException(nameof(tokens)); _tokens = tokens; _thinking = thinking; _cache = cache; }
            protected override void Load() => _model = MNNLlm.LoadForConversation(Directory, Threads, _thinking, _cache);
            public Task GenerateAsync(IReadOnlyList<MNNOpenAIMessage> history, float[] audio, int sampleRate, Func<string, Task> delta, CancellationToken token)
            {
                if (audio != null) throw new NotSupportedException("Use a speech recognizer before this text model adapter.");
                return Generate(history, delta, _tokens, token);
            }
            public Task ClassifyVoiceIntentAsync(IReadOnlyList<MNNOpenAIMessage> history, Func<string, Task> delta, CancellationToken token) => Generate(history, delta, 128, token);
            private Task Generate(IReadOnlyList<MNNOpenAIMessage> history, Func<string, Task> delta, int tokens, CancellationToken token) => Task.Run(() =>
            {
                var messages = new MNNChatMessage[history.Count];
                for (int i = 0; i < messages.Length; ++i)
                {
                    if (history[i].Audio != null) throw new NotSupportedException("Use a speech recognizer before this text model adapter.");
                    messages[i] = new MNNChatMessage(history[i].Role, history[i].Text);
                }
                _model.GenerateConversationStreamingDeltas(messages, update => { token.ThrowIfCancellationRequested(); delta(update.Text).GetAwaiter().GetResult(); }, tokens, cancellationToken: token);
            }, token);
            protected override void Release() { _model?.Dispose(); _model = null; }
        }
        private sealed class RecognitionProvider : Provider, IMNNConversationAsrProvider
        {
            private MNNSpeechRecognizer _model;
            private readonly string _language;
            internal RecognitionProvider(string directory, int threads, string language) : base(directory, threads, MNNRealtimeFeature.MemoryAudioInput | MNNRealtimeFeature.SpeechRecognition) { _language = language; }
            protected override void Load() => _model = MNNSpeechRecognizer.Load(Directory, Threads, language: _language);
            public Task<string> TranscribeAsync(float[] audio, int sampleRate, CancellationToken token) => Task.Run(() => _model.Transcribe(audio, sampleRate, token), token);
            protected override void Release() { _model?.Dispose(); _model = null; }
        }
        private sealed class SpeechProvider : Provider, IMNNConversationTtsProvider, IMNNConversationSpeechLanguages
        {
            private MNNSpeechSynthesizer _model;
            private readonly MNNSpeechSynthesisOptions _options;
            internal SpeechProvider(string directory, MNNSpeechSynthesisOptions options, int threads) : base(directory, threads, MNNRealtimeFeature.IndependentTts | MNNRealtimeFeature.BufferedSpeech) {
                _options = options == null ? null : new MNNSpeechSynthesisOptions
                {
                    Voice = options.Voice, Language = options.Language, Precision = options.Precision,
                    ReferenceWavePath = options.ReferenceWavePath, ReferenceText = options.ReferenceText,
                    Steps = options.Steps, Seed = options.Seed, Speed = options.Speed
                };
            }
            protected override void Load() => _model = MNNSpeechSynthesizer.Load(Directory, _options, Threads);
            public int SampleRate => _model?.SampleRate ?? 24000;
            public IReadOnlyList<string> SupportedSpeechLanguages => _model?.Languages ?? Array.Empty<string>();
            public async Task SynthesizeAsync(string text, Func<float[], int, Task> pcm, CancellationToken token)
            { var audio = await Task.Run(() => _model.Synthesize(text, token), token).ConfigureAwait(false); token.ThrowIfCancellationRequested(); await pcm(audio.Waveform, audio.Waveform.Length).ConfigureAwait(false); }
            protected override void Release() { _model?.Dispose(); _model = null; }
        }
    }
}
