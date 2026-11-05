using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
namespace MNN.Unity
{
    /// <summary>Complete local conversation task adapters, each owned by one session.</summary>
    public static partial class MNNConversationLocalProviders
    {
        public static IMNNConversationProvider Create(MNNConversationModelAdapter kind, string directory, MNNConversationLocalModelOptions options = null)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Choose a model directory.", nameof(directory));
            var settings = (options ?? new MNNConversationLocalModelOptions()).Snapshot();
            var caps = DescribeCapabilities(kind);
            int recognitionThreads = settings.RecognitionThreads;
            switch (kind)
            {
                case MNNConversationModelAdapter.Language:
                case MNNConversationModelAdapter.Omni:
                    return new Language(caps, null, directory, settings, settings.ScratchDirectory,
                    kind == MNNConversationModelAdapter.Omni, settings.Backend, settings.CacheRoot, settings.BuildIdentity, settings.DeviceIdentity);
                case MNNConversationModelAdapter.SpeechRecognition: return new ProfiledRecognition(caps, null, directory, recognitionThreads, settings.Backend, settings.Language);
                case MNNConversationModelAdapter.SpeechSynthesis:
                    return new ProfiledSpeech(caps, null, directory, settings.Threads, settings.Backend,
                    new MNNSpeechSynthesisOptions
                    {
                        Voice = settings.Voice,
                        Language = settings.Language,
                        Precision = settings.Precision,
                        ReferenceWavePath = settings.ReferenceWavePath,
                        ReferenceText = settings.ReferenceText,
                        Steps = settings.SynthesisSteps,
                        Seed = settings.SynthesisSeed,
                        Speed = settings.SynthesisSpeed
                    });
                case MNNConversationModelAdapter.Paraformer: return new ParaformerRecognition(caps, null, directory, recognitionThreads, settings.Backend);
                case MNNConversationModelAdapter.SenseVoice:
                case MNNConversationModelAdapter.Qwen3Asr: return new Recognition(caps, null, directory, recognitionThreads, kind, settings.Backend, settings.Language);
                case MNNConversationModelAdapter.Kokoro:
                case MNNConversationModelAdapter.CosyVoice:
                case MNNConversationModelAdapter.Supertonic:
                case MNNConversationModelAdapter.Piper:
                case MNNConversationModelAdapter.BertVits2:
                case MNNConversationModelAdapter.SherpaVitsChinese:
                case MNNConversationModelAdapter.MossNano:
                    return new Speech(caps, null, directory, settings.Threads, kind, settings.Voice,
                    settings.Language, settings.Precision, settings.SynthesisSteps, settings.Backend);
                case MNNConversationModelAdapter.VoiceActivity:
                case MNNConversationModelAdapter.Silero: return new Vad(caps, null, directory, settings.Threads, settings.Backend);
                case MNNConversationModelAdapter.AudioTurn:
                case MNNConversationModelAdapter.TextTurn:
                case MNNConversationModelAdapter.SmartTurn: return new Turn(caps, null, directory, settings.Threads, settings.Backend);
                default: throw new NotSupportedException("Unsupported local conversation task: " + kind);
            }
        }
        public static string PrepareKokoroText(string text) => text?.Replace('~', ' ').Trim();

        public static bool HasPronounceableText(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (char character in text)
                if (char.IsLetterOrDigit(character)) return true;
            return false;
        }

        public static List<MNNChatMessage> ConversationMessages(IReadOnlyList<MNNOpenAIMessage> history,
            Func<float[], int, string> saveAudio, out string lastAudio)
        {
            lastAudio = null;
            var messages = new List<MNNChatMessage>(history.Count);
            for (int i = 0; i < history.Count; ++i)
            {
                var item = history[i];
                string content = item.Text ?? "请听取这条语音并简短回答。";
                if (item.Audio != null)
                {
                    string path = saveAudio(item.Audio, item.SampleRate);
                    if (i == history.Count - 1) lastAudio = path;
                    // Official MNN multimodal chat content uses <audio> paths. KV is reset
                    // for each request, so prior audio must be supplied again as well.
                    else content = "<audio>" + path + "</audio>\n" + content;
                }
                messages.Add(new MNNChatMessage(item.Role, content));
            }
            return messages;
        }

        public static MNNRealtimeCapabilities DescribeCapabilities(MNNConversationModelAdapter kind)
        {
            bool supported = MNNPlatformSupport.IsSupported;
            var features = supported ? MNNRealtimeFeature.SupportedAbi | MNNRealtimeFeature.InProcessMnn : MNNRealtimeFeature.None;
            switch (kind)
            {
                case MNNConversationModelAdapter.Language:
                case MNNConversationModelAdapter.Omni:
                    features |= MNNRealtimeFeature.TextInput | MNNRealtimeFeature.StableSpokenText | MNNRealtimeFeature.ContextRecovery;
                    if (kind == MNNConversationModelAdapter.Omni) features |= MNNRealtimeFeature.MemoryAudioInput | MNNRealtimeFeature.BufferedSpeech;
                    break;
                case MNNConversationModelAdapter.SpeechSynthesis:
                case MNNConversationModelAdapter.Kokoro:
                case MNNConversationModelAdapter.CosyVoice:
                case MNNConversationModelAdapter.Supertonic:
                case MNNConversationModelAdapter.Piper:
                case MNNConversationModelAdapter.BertVits2:
                case MNNConversationModelAdapter.SherpaVitsChinese:
                case MNNConversationModelAdapter.MossNano:
                    features |= MNNRealtimeFeature.IndependentTts | MNNRealtimeFeature.BufferedSpeech; break;
                case MNNConversationModelAdapter.SpeechRecognition:
                case MNNConversationModelAdapter.Paraformer:
                case MNNConversationModelAdapter.SenseVoice:
                case MNNConversationModelAdapter.Qwen3Asr:
                    features |= MNNRealtimeFeature.MemoryAudioInput | MNNRealtimeFeature.SpeechRecognition; break;
                case MNNConversationModelAdapter.VoiceActivity:
                case MNNConversationModelAdapter.Silero:
                    features |= MNNRealtimeFeature.MemoryAudioInput | MNNRealtimeFeature.VoiceActivityDetection | MNNRealtimeFeature.BoundedScheduling; break;
                case MNNConversationModelAdapter.AudioTurn:
                case MNNConversationModelAdapter.TextTurn:
                case MNNConversationModelAdapter.SmartTurn:
                    features |= MNNRealtimeFeature.MemoryAudioInput | MNNRealtimeFeature.BoundedScheduling; break;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
            return new MNNRealtimeCapabilities(features, MNNPlatformSupport.AbiContract + " / " + kind,
                "Existing MNN.Unity managed task APIs; Runtime model acquisition; loaded Omni capabilities are checked before listening.",
                supported ? "Turn-based inference; cancellation between native operations. No continuous-audio, progressive-waveform or realtime-throughput claim." : MNNPlatformSupport.UnsupportedReason);
        }

        private sealed class Language : Provider, IMNNConversationTextProvider, IMNNConversationSpeechModelProvider, IMNNConversationVoiceIntentProvider, IMNNConversationRequestPlanner
        {
            protected override string ReadBackend() => _model == null ? "Not loaded" :
                "Language: " + _model.Backend + " · image/audio processor: " + _model.MediaBackend +
                " · " + _settings.Threads + " threads · " + _performance.PrecisionMode + "/" + _performance.PowerMode + Volatile.Read(ref _lastTiming) +
                " (model runtime selection; individual operator placement not exposed)";
            private readonly MNNConversationLocalModelOptions _settings;
            private readonly string _scratch;
            private readonly bool _omni;
            private MNNLlm _model;
            private MNNLlmPerformanceOptions _performance;
            private readonly string _cacheRoot, _buildIdentity, _deviceIdentity;
            private MNNLlmCache _cache;
            private string _lastTiming = "";
            internal Language(MNNRealtimeCapabilities caps, SynchronizationContext unity, string directory,
                MNNConversationLocalModelOptions settings, string scratch, bool omni, MNNBackendType backend,
                string cacheRoot, string buildIdentity, string deviceIdentity) : base(caps, unity, directory)
            {
                _settings = settings; _scratch = scratch; _omni = omni; Backend = backend;
                _cacheRoot = cacheRoot; _buildIdentity = buildIdentity; _deviceIdentity = deviceIdentity;
            }
            protected override void Load()
            {
                // Validation shares this model with replies. Greedy decoding keeps a voice
                // candidate's ACCEPT/REJECT decision from changing with conversational sampling.
                var performance = new MNNLlmPerformanceOptions
                {
                    ThreadCount = _settings.Threads,
                    BackendType = Backend
                };
                if (!_settings.UseDeviceLanguageDefaults)
                {
                    performance.PrecisionMode = MNNPrecisionMode.Low;
                    performance.PowerMode = MNNPowerMode.High;
                }
#if UNITY_ANDROID && !UNITY_EDITOR
                // The PJA110 real-chat comparison did not establish a Low/High
                // speed advantage. Preserve the existing Android CPU profile.
                if (!_settings.UseDeviceLanguageDefaults)
                {
                    performance.PrecisionMode = MNNPrecisionMode.Normal;
                    performance.PowerMode = MNNPowerMode.Normal;
                }
#endif
                _performance = performance;
                if (!string.IsNullOrEmpty(_buildIdentity))
                    _cache = MNNLlmCache.Acquire(_cacheRoot, Directory, _buildIdentity, _deviceIdentity, performance);
                _model = _settings.DeterministicDecisions ? MNNLlm.Load(Directory, performance, _settings.Thinking, cacheDirectory: _cache?.DirectoryPath ?? _settings.CacheDirectory) :
                    MNNLlm.LoadForConversation(Directory, performance, _settings.Temperature, _settings.Thinking, cacheDirectory: _cache?.DirectoryPath ?? _settings.CacheDirectory);
                var required = MNNModelCapabilities.AudioInput | (_settings.SpeechOutput ? MNNModelCapabilities.SpeechOutput : 0);
                if (_omni && (_model.Capabilities & required) != required)
                    throw new InvalidOperationException("The selected Omni model does not support the configured audio input/output features.");
            }
            public async Task GenerateAsync(IReadOnlyList<MNNOpenAIMessage> history, float[] audio, int sampleRate,
                Func<string, Task> textDelta, CancellationToken token) =>
                await GenerateCoreAsync(history, audio, sampleRate, textDelta, false, token).ConfigureAwait(false);
            public Task<MNNMultimodalResult> GenerateSpeechAsync(IReadOnlyList<MNNOpenAIMessage> history,
                Func<string, Task> textDelta, CancellationToken token) => GenerateCoreAsync(history, null, 16000, textDelta, true, token);
            public async Task ClassifyVoiceIntentAsync(IReadOnlyList<MNNOpenAIMessage> prompt,
                Func<string, Task> textDelta, CancellationToken token) =>
                await GenerateCoreAsync(prompt, null, 16000, textDelta, false, token, 128).ConfigureAwait(false);
            public async Task PlanRequestAsync(IReadOnlyList<MNNOpenAIMessage> prompt,
                Func<string, Task> textDelta, CancellationToken token) =>
                await GenerateCoreAsync(prompt, null, 16000, textDelta, false, token, 256).ConfigureAwait(false);
            private async Task<MNNMultimodalResult> GenerateCoreAsync(IReadOnlyList<MNNOpenAIMessage> history,
                float[] audio, int rate, Func<string, Task> delta, bool speech, CancellationToken token, int? tokenBudget = null)
            {
                if (_settings.TextOnly)
                {
                    if (audio != null) throw new NotSupportedException("Use a speech recognizer before this text model adapter.");
                    foreach (var message in history) if (message.Audio != null) throw new NotSupportedException("Use a speech recognizer before this text model adapter.");
                }
                var waves = new List<string>();
                try
                {
                    string SaveAudio(float[] samples, int sampleRate)
                    {
                        token.ThrowIfCancellationRequested();
                        if ((_model.Capabilities & MNNModelCapabilities.AudioInput) == 0) throw new NotSupportedException("Selected model does not accept audio.");
                        if (string.IsNullOrWhiteSpace(_scratch)) throw new InvalidOperationException("Configure a scratch directory for file-backed multimodal audio input.");
                        System.IO.Directory.CreateDirectory(_scratch);
                        string path = Path.Combine(_scratch, "turn-" + Guid.NewGuid().ToString("N") + ".wav");
                        waves.Add(path);
                        File.WriteAllBytes(path, MNNWaveAudio.EncodePcm16(samples, sampleRate));
                        return path;
                    }
                    var messages = ConversationMessages(history, SaveAudio, out string wave);
                    if (wave == null && audio != null) wave = SaveAudio(audio, rate);
                    var result = await _model.GenerateConversationStreamingDeltasAsync(messages, update =>
                    {
                        token.ThrowIfCancellationRequested();
                        delta(update.Text).GetAwaiter().GetResult();
                    }, tokenBudget ?? _settings.MaximumReplyTokens, audioPath: wave, generateSpeech: speech,
                        maxAudioTokens: _settings.MaximumAudioTokens, cancellationToken: token).ConfigureAwait(false);
                    Volatile.Write(ref _lastTiming, string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        " · first text {0:F0} ms · prefill {1:F0} ms ({2} new / {5} reused tokens) · decode {3:F0} ms · callback wait {4:F0} ms",
                        result.FirstTextMicroseconds / 1000d, result.PrefillMicroseconds / 1000d, result.PromptTokens,
                        result.DecodeMicroseconds / 1000d, result.ProgressCallbackMicroseconds / 1000d, result.ReusedPromptTokens));
                    return result;
                }
                finally
                {
                    PublishBackend();
                    foreach (string path in waves) if (File.Exists(path)) File.Delete(path);
                }
            }
            protected override void ReleaseModels()
            {
                try { _model?.Dispose(); }
                finally { _model = null; _cache?.Dispose(); _cache = null; }
            }
        }

        private sealed class Recognition : Provider, IMNNConversationAsrProvider
        {
            protected override string ReadBackend() => _sense != null ? "Recognition: " + _sense.Backend + " · " + _threads + " ASR threads" :
                _qwen != null ? "Language: " + _qwen.Backend + " · audio processor: " + _qwen.MediaBackend : "Not loaded";
            private readonly int _threads;
            private readonly MNNConversationModelAdapter _kind;
            private MNNSenseVoiceRecognizer _sense;
            private readonly string _recognitionLanguage;
            private MNNQwen3AsrRecognizer _qwen;
            internal Recognition(MNNRealtimeCapabilities caps, SynchronizationContext unity, string directory, int threads, MNNConversationModelAdapter kind, MNNBackendType backend, string language)
                : base(caps, unity, directory) { _threads = threads; _kind = kind; Backend = backend; _recognitionLanguage = language; }
            protected override void Load()
            {
                if (_kind == MNNConversationModelAdapter.SenseVoice)
                {
                    var config = MNNSenseVoiceConfig.Read(Directory);
                    if (!string.IsNullOrWhiteSpace(_recognitionLanguage)) config.language = _recognitionLanguage.Trim().ToLowerInvariant();
                    _sense = MNNSenseVoiceRecognizer.Load(Directory, config, _threads, backendType: Backend);
                }
                else _qwen = MNNQwen3AsrRecognizer.Load(Directory,
                    string.IsNullOrWhiteSpace(_recognitionLanguage) ? null : _recognitionLanguage,
                    _threads, backendType: Backend);
            }
            public async Task<string> TranscribeAsync(float[] audio, int rate, CancellationToken token)
            {
                try
                {
                    return _sense != null ? (await _sense.RecognizeAsync(audio, rate, token).ConfigureAwait(false)).Text :
                        (await _qwen.RecognizeAsync(audio, rate, token).ConfigureAwait(false)).Text;
                }
                finally { PublishBackend(); }
            }
            protected override void ReleaseModels() { _sense?.Dispose(); _sense = null; _qwen?.Dispose(); _qwen = null; }
        }

        private sealed class Speech : Provider, IMNNConversationTtsProvider, IMNNConversationTtsDiagnostics, IMNNConversationSpeechLanguages
        {
            protected override string ReadBackend()
            {
                string primary = DescribePrimaryBackend();
                string fallback = _languageFallback?.LastModelDirectory;
                return string.IsNullOrEmpty(fallback) ? primary : primary +
                    "\nAdditional language fallback model: " + fallback + " · backend not exposed; may remain resident until provider release";
            }
            private string DescribePrimaryBackend()
            {
                if (_supertonic != null) return string.Join(" · ", _supertonic.StageBackends.Select(item => item.Key + ": " + item.Value));
                if (_bertVits2 != null) return string.Join(" · ", _bertVits2.StageBackends.Select(item => item.Key + ": " + item.Value));
                if (_piper != null) return "Synthesis: " + _piper.Backend;
                if (_cosy != null) return "Language: " + _cosy.Backend + " · flow/vocoder: not reported";
                if (_kokoro != null || _sherpaVits != null) return "CPU · fixed Kokoro / Sherpa VITS adapter contract";
                return "Not reported by the managed model API";
            }
            private readonly int _threads;
            private readonly MNNConversationModelAdapter _kind;
            private readonly string _requestedVoice;
            private readonly string _language;
            private readonly string _precision;
            private readonly int _synthesisSteps;
            private string _voice;
            private MNNKokoro _kokoro;
            private MNNCosyVoice _cosy;
            private MNNSupertonic _supertonic;
            private MNNPiper _piper;
            private MNNLocalSpeechFallback _languageFallback;
            private MNNBertVits2 _bertVits2;
            private MNNSherpaVitsChinese _sherpaVits;
            private MNNMossNano _moss;
            public int SampleRate => ActualSampleRate;
            public IReadOnlyList<string> SupportedSpeechLanguages =>
                _kokoro == null && _cosy == null && _supertonic == null && _piper == null && _bertVits2 == null && _sherpaVits == null && _moss == null
                    ? Array.Empty<string>() : PreferredLanguages();
            public MNNConversationSpeechTimings LastStageTimings => _cosy == null
                ? default
                : new MNNConversationSpeechTimings(_cosy.LastTokenSeconds * 1000, _cosy.LastFlowSeconds * 1000,
                    _cosy.LastVocoderSeconds * 1000);
            internal Speech(MNNRealtimeCapabilities caps, SynchronizationContext unity, string directory, int threads, MNNConversationModelAdapter kind, string voice, string language, string precision, int synthesisSteps, MNNBackendType backend)
                : base(caps, unity, directory) { _threads = threads; _kind = kind; _requestedVoice = voice; _language = language; _precision = precision; _synthesisSteps = synthesisSteps; Backend = backend; }
            protected override void Load()
            {
                if (_kind == MNNConversationModelAdapter.Kokoro) _kokoro = MNNKokoro.Load(Directory, _threads);
                else if (_kind == MNNConversationModelAdapter.CosyVoice) _cosy = MNNCosyVoice.Load(Directory, _threads, backendType: Backend);
                else if (_kind == MNNConversationModelAdapter.Supertonic) _supertonic = MNNSupertonic.Load(Directory, _threads,
                    string.IsNullOrWhiteSpace(_precision) ? "fp16" : _precision.Trim().ToLowerInvariant(), backendType: Backend);
                else if (_kind == MNNConversationModelAdapter.Piper) _piper = MNNPiper.Load(Directory, _threads, backendType: Backend);
                else if (_kind == MNNConversationModelAdapter.BertVits2) _bertVits2 = MNNBertVits2.Load(Directory, _threads, backendType: Backend);
                else if (_kind == MNNConversationModelAdapter.SherpaVitsChinese) _sherpaVits = MNNSherpaVitsChinese.Load(Directory, _threads);
                else _moss = MNNMossNano.Load(Directory, _threads);
                var voices = _kokoro?.Voices ?? _supertonic?.Voices ?? _piper?.Voices ?? _sherpaVits?.Voices ?? _moss?.Voices;
                _voice = SelectVoice(_requestedVoice, voices, _kind == MNNConversationModelAdapter.MossNano ? "Junhao" :
                    _kind == MNNConversationModelAdapter.Kokoro ? "zf_xiaoxiao" : _kind == MNNConversationModelAdapter.Supertonic ? "M1" : null);
                _languageFallback = new MNNLocalSpeechFallback(Directory, ActualSampleRate, _threads);
                if (_supertonic != null && !string.IsNullOrWhiteSpace(_language) &&
                    Array.IndexOf(_supertonic.Languages, MNNSpeechLanguages.Normalize(_language)) < 0)
                    throw new ArgumentException("Unsupported Supertonic language. Choose: " + string.Join(", ", _supertonic.Languages));
            }
            private static string SelectVoice(string requested, string[] voices, string fallback)
            {
                if (voices == null || voices.Length == 0) return null;
                if (!string.IsNullOrWhiteSpace(requested))
                {
                    if (Array.IndexOf(voices, requested) < 0)
                        throw new ArgumentException("Select an installed voice, or leave Voice empty for automatic selection: " + string.Join(", ", voices));
                    return requested;
                }
                if (!string.IsNullOrWhiteSpace(fallback) && Array.IndexOf(voices, fallback) >= 0) return fallback;
                return voices[0];
            }
            public int ActualSampleRate => _kokoro?.SampleRate ?? _cosy?.SampleRate ?? _supertonic?.SampleRate ?? _piper?.SampleRate ?? _bertVits2?.SampleRate ?? _sherpaVits?.SampleRate ?? (_moss == null ? 24000 : 48000);
            public async Task SynthesizeAsync(string text, Func<float[], int, Task> pcm, CancellationToken token)
            {
                try
                {
                    token.ThrowIfCancellationRequested();
                    text = MNNSpeechTextCleaner.Clean(text);
                    if (string.IsNullOrEmpty(text))
                    {
                        int silenceFrames = Math.Max(1, SampleRate / 100);
                        await pcm(new float[silenceFrames], silenceFrames).ConfigureAwait(false);
                        return;
                    }
                    if (_piper == null && !MNNSpeechLanguages.SupportsText(text, PreferredLanguages()))
                    {
                        var fallback = await Task.Run(() => _languageFallback.Synthesize(text, PreferredLanguages(),
                            () => throw new MNNSpeechLanguageException("Selected model cannot pronounce this input."), token), token).ConfigureAwait(false);
                        await pcm(fallback.Waveform, fallback.Waveform.Length).ConfigureAwait(false);
                        return;
                    }
                    if (_kokoro != null)
                    {
                        string spoken = PrepareKokoroText(text);
                        if (string.IsNullOrWhiteSpace(spoken))
                        {
                            token.ThrowIfCancellationRequested();
                            await pcm(new float[SampleRate / 10], SampleRate / 10).ConfigureAwait(false);
                            return;
                        }
                        var kokoroResult = await _kokoro.SynthesizeAsync(spoken, _voice, cancellationToken: token).ConfigureAwait(false);
                        token.ThrowIfCancellationRequested();
                        if (kokoroResult.SampleRate != SampleRate) throw new InvalidOperationException("Unexpected TTS sample rate.");
                        await pcm(kokoroResult.Waveform, kokoroResult.Waveform.Length).ConfigureAwait(false);
                        return;
                    }

                    if (_cosy != null)
                    {
                        await Task.Run(() => _cosy.SynthesizeStreaming(text, (samples, count, _) =>
                        {
                            token.ThrowIfCancellationRequested();
                            pcm(samples, count).GetAwaiter().GetResult();
                        }, cancellationToken: token), token).ConfigureAwait(false);
                        return;
                    }

                    var result = await Task.Run(() =>
                    {
                        if (_supertonic != null) return _supertonic.Synthesize(text, voice: string.IsNullOrWhiteSpace(_voice) ? "M1" : _voice,
                            steps: _synthesisSteps < 1 ? 4 : _synthesisSteps,
                            language: MNNSpeechLanguages.Select(text, _supertonic.Languages, _language), cancellationToken: token);
                        if (_piper != null) return _piper.Synthesize(text, string.IsNullOrWhiteSpace(_voice) ? null : _voice, token);
                        if (_bertVits2 != null) return _bertVits2.Synthesize(text, token);
                        if (_sherpaVits != null) return _sherpaVits.Synthesize(text, string.IsNullOrWhiteSpace(_voice) ? 0 : Array.IndexOf(_sherpaVits.Voices, _voice), cancellationToken: token);
                        return _moss.Synthesize(text, string.IsNullOrWhiteSpace(_voice) ? "Junhao" : _voice, cancellationToken: token);
                    }, token).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    if (result.SampleRate != ActualSampleRate) throw new InvalidOperationException("Unexpected TTS sample rate.");
                    await pcm(result.Waveform, result.Waveform.Length).ConfigureAwait(false);
                }
                finally { PublishBackend(); }
            }
            private string[] PreferredLanguages()
            {
                if (_piper != null) return _piper.Languages;
                if (_supertonic != null) return _supertonic.Languages;
                if (_sherpaVits != null) return new[] { "zh" };
                return new[] { "zh", "en" };
            }
            protected override void ReleaseModels() { _languageFallback?.Dispose(); _languageFallback = null; _kokoro?.Dispose(); _kokoro = null; _cosy?.Dispose(); _cosy = null; _supertonic?.Dispose(); _supertonic = null; _piper?.Dispose(); _piper = null; _bertVits2?.Dispose(); _bertVits2 = null; _sherpaVits?.Dispose(); _sherpaVits = null; _moss?.Dispose(); _moss = null; }
        }

        private sealed class Vad : Provider, IMNNConversationVadProvider, IMNNRealtimeVoiceActivity
        {
            protected override string ReadBackend() => _model == null ? "Not loaded" : "VAD: " + _model.Backend;
            private MNNProfiledVoiceActivity _model;
            private readonly int _threads;
            internal Vad(MNNRealtimeCapabilities caps, SynchronizationContext unity, string directory, int threads, MNNBackendType backend)
                : base(caps, unity, directory) { _threads = threads; Backend = backend; }
            protected override void Load() => _model = MNNProfiledVoiceActivity.Load(Directory, _threads, Backend);
            public bool IsSpeech(float[] frame, int count, int rate)
            {
                if (count != 512 || rate != 16000) throw new ArgumentException("Capture VAD expects 512 samples at 16 kHz.");
                return ProcessFrame(frame, 0).IsSpeechActive;
            }
            public MNNRealtimeVadUpdate ProcessFrame(float[] frame, int offset) => _model.ProcessFrame(frame, offset);
            public void Reset() => _model.Reset();
            protected override void ReleaseModels() { _model?.Dispose(); _model = null; }
        }

        private sealed class Turn : Provider, IMNNConversationTurnProvider, IMNNConversationTranscriptTurnProvider
        {
            protected override string ReadBackend() => _model == null ? "Not loaded" : "Turn detection: " + _model.Backend;
            private readonly int _threads;
            private MNNTurnDetector _model;
            internal Turn(MNNRealtimeCapabilities caps, SynchronizationContext unity, string directory, int threads, MNNBackendType backend)
                : base(caps, unity, directory) { _threads = threads; Backend = backend; }
            protected override void Load() => _model = MNNTurnDetector.Load(Directory, _threads, Backend);
            public bool RequiresTranscript => _model.RequiresTranscript;
            public Task<bool> IsTurnCompleteAsync(float[] audio, int rate, CancellationToken token) =>
                IsTurnCompleteAsync(audio, rate, null, token);
            public Task<bool> IsTurnCompleteAsync(float[] audio, int rate, string transcript, CancellationToken token) =>
                Task.Run(() => _model.IsComplete(audio, rate, transcript, token), token);
            protected override void ReleaseModels() { _model?.Dispose(); _model = null; }
        }
    }
}
