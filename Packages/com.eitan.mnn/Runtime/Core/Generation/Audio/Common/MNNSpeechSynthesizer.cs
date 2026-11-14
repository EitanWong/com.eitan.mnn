using System;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>Buffered synthesis over all registered TTS export protocols. Owns one task runner.</summary>
    public sealed partial class MNNSpeechSynthesizer : IDisposable
    {
        private readonly IDisposable _owner;
        private readonly Func<string, CancellationToken, MNNGeneratedAudio> _synthesize;
        public int SampleRate { get; }
        public string[] Languages { get; }
        private MNNSpeechSynthesizer(IDisposable owner, int rate, string[] languages, Func<string, CancellationToken, MNNGeneratedAudio> synthesize)
        { _owner = owner; SampleRate = rate; Languages = languages; _synthesize = synthesize; }
        /// <summary>Check the detected frontend's required local assets without loading weights.</summary>
        public static bool HasProfile(string directory) => IsComplete(directory, DetectModelKind(directory));
        public static bool RequiresReferenceAudio(string directory)
        {
            var profile = MNNTtsModelProfiles.Detect(directory);
            return profile == MNNSpeechModelKind.IndexTts25 || profile == MNNSpeechModelKind.FireRedTts3 ||
                profile == MNNSpeechModelKind.IndicF5 || profile == MNNSpeechModelKind.Pocket || profile == MNNSpeechModelKind.DhVaani;
        }
        public static bool RequiresReferenceAudio(MNNRemoteModel model)
        {
            string repository = model?.repositoryId ?? model?.modelScopeId;
            return MNNReferenceSpeechModel.IsRepository(repository) || MNNVerifiedTtsModel.IsIndicF5Repository(repository) ||
                MNNVerifiedTtsModel.IsPocketRepository(repository) || MNNVerifiedTtsModel.IsDhVaaniRepository(repository);
        }
        public static bool RequiresReferenceText(MNNRemoteModel model)
        {
            string repository = model?.repositoryId ?? model?.modelScopeId;
            return MNNReferenceSpeechModel.IsFireRed(repository) || MNNVerifiedTtsModel.IsIndicF5Repository(repository) ||
                MNNVerifiedTtsModel.IsDhVaaniRepository(repository);
        }
        public static bool SupportsRepository(MNNRemoteModel model)
        {
            if (model == null) return false;
            string repository = model.repositoryId ?? model.modelScopeId;
            return model.provider == MNNModelSource.ModelScope && MNNVerifiedTtsModel.IsRepository(repository) ||
                model.provider == MNNModelSource.HuggingFace && (MNNVerifiedTtsModel.IsHuggingFaceRepository(repository) ||
                    MNNReferenceSpeechModel.IsRepository(repository) || MNNCosyVoiceModel.IsRepository(repository));
        }
        public static MNNSpeechSynthesizer Load(string directory, MNNSpeechSynthesisOptions options = null, int threadCount = 4,
            MNNBackendType backendType = MNNBackendType.Auto)
        {
            var requested = options ?? new MNNSpeechSynthesisOptions();
            options = new MNNSpeechSynthesisOptions { Voice = requested.Voice, Language = requested.Language,
                Precision = requested.Precision, ReferenceWavePath = requested.ReferenceWavePath, ReferenceText = requested.ReferenceText,
                Steps = requested.Steps, Seed = requested.Seed, Speed = requested.Speed };
            var profile = MNNTtsModelProfiles.Detect(directory);
            MNNReferenceAudio reference = null;
            if (RequiresReferenceAudio(directory))
            {
                if (string.IsNullOrWhiteSpace(options.ReferenceWavePath))
                    throw new ArgumentException("Select a reference WAV recording for this speech model.");
                reference = MNNReferenceAudio.FromWaveFile(options.ReferenceWavePath);
                if ((profile == MNNSpeechModelKind.FireRedTts3 || profile == MNNSpeechModelKind.IndicF5 || profile == MNNSpeechModelKind.DhVaani) &&
                    string.IsNullOrWhiteSpace(options.ReferenceText)) throw new ArgumentException("Enter the exact transcript of the reference WAV recording.");
            }
            Func<string, string> language = text => MNNSpeechLanguages.Select(text, new[] { "zh", "en" }, options.Language);
            string voice = string.IsNullOrWhiteSpace(options.Voice) ? null : options.Voice;
            switch (profile)
            {
                case MNNSpeechModelKind.IndexTts25:
                    var index = MNNIndexTts25.Load(directory, threadCount);
                    return new MNNSpeechSynthesizer(index, 22050, new[] { "zh", "en" }, (text, token) => index.Synthesize(text, reference, language(text), options.Seed, token));
                case MNNSpeechModelKind.FireRedTts3:
                    var fire = MNNFireRedTts3.Load(directory, threadCount);
                    return new MNNSpeechSynthesizer(fire, 24000, new[] { "zh", "en" }, (text, token) => fire.Synthesize(text, reference, options.ReferenceText, language(text), options.Seed, token));
                case MNNSpeechModelKind.IndicF5:
                    var indicF5 = MNNIndicF5Tts.Load(directory, threadCount);
                    return new MNNSpeechSynthesizer(indicF5, 24000, new[] { "hi", "bn", "te", "mr", "ta", "gu", "kn", "ml", "pa", "or", "as" },
                        (text, token) => indicF5.Synthesize(text, reference, options.ReferenceText, options.Seed, options.Steps < 1 ? 32 : options.Steps, options.Speed, token));
                case MNNSpeechModelKind.Pocket:
                    var pocket = MNNPocketTts.Load(directory, threadCount);
                    return new MNNSpeechSynthesizer(pocket, 24000, new[] { "en" }, (text, token) => pocket.Synthesize(text, reference, options.Seed, cancellationToken: token));
                case MNNSpeechModelKind.DhVaani:
                    var dh = MNNDhVaani.Load(directory, threadCount);
                    return new MNNSpeechSynthesizer(dh, 24000, new[] { "hi", "en" }, (text, token) => dh.Synthesize(text, reference, options.ReferenceText,
                        options.Steps < 1 ? 32 : options.Steps, options.Speed, options.Seed, token));
                case MNNSpeechModelKind.Qwen3Explicit:
                    var qwen = MNNQwen3ExplicitTts.Load(directory, threadCount);
                    return new MNNSpeechSynthesizer(qwen, 24000, new[] { "zh", "en" },
                        (text, token) => qwen.Synthesize(text, language(text), options.Seed, cancellationToken: token, voice: voice));
                case MNNSpeechModelKind.CosyVoice300M:
                    var cosy300 = MNNCosyVoice300M.Load(directory, threadCount, acousticBackend: backendType);
                    return new MNNSpeechSynthesizer(cosy300, cosy300.SampleRate, new[] { "zh", "en" },
                        (text, token) => cosy300.Synthesize(text, options.Seed, cancellationToken: token));
                case MNNSpeechModelKind.MmsTamil:
                    var tamil = MNNMmsTamil.Load(directory, threadCount, string.IsNullOrWhiteSpace(options.Precision) ? "int8" : options.Precision);
                    return new MNNSpeechSynthesizer(tamil, tamil.SampleRate, new[] { "ta" }, (text, token) => tamil.Synthesize(text, options.Speed, token));
                case MNNSpeechModelKind.IndicVits:
                    var indic = MNNIndicVits.Load(directory, threadCount);
                    int speaker = voice == null ? 18 : Array.IndexOf(MNNIndicVits.SpeakerNames, voice);
                    if (speaker < 0) { indic.Dispose(); throw new ArgumentException("Unknown IndicVITS voice: " + voice); }
                    return new MNNSpeechSynthesizer(indic, indic.SampleRate, new[] { IndicVoiceLanguage(MNNIndicVits.SpeakerNames[speaker]) },
                        (text, token) => indic.Synthesize(text, speaker, cancellationToken: token));
                case MNNSpeechModelKind.CosyVoice:
                    var cosy = MNNCosyVoice.Load(directory, threadCount, backendType: backendType);
                    return new MNNSpeechSynthesizer(cosy, cosy.SampleRate, new[] { "zh", "en" }, (text, token) => cosy.Synthesize(text, options.Seed, token));
                case MNNSpeechModelKind.Kokoro:
                    var kokoro = MNNKokoro.Load(directory, threadCount);
                    return new MNNSpeechSynthesizer(kokoro, kokoro.SampleRate, new[] { "zh", "en" }, (text, token) => kokoro.Synthesize(text, voice, options.Speed, token));
                case MNNSpeechModelKind.Piper:
                    var piper = MNNPiper.Load(directory, threadCount, backendType: backendType);
                    return new MNNSpeechSynthesizer(piper, piper.SampleRate, piper.Languages, (text, token) => piper.Synthesize(text, voice, token));
                case MNNSpeechModelKind.Supertonic:
                    var super = MNNSupertonic.Load(directory, threadCount, string.IsNullOrWhiteSpace(options.Precision) ? "fp16" : options.Precision, backendType: backendType);
                    return new MNNSpeechSynthesizer(super, super.SampleRate, super.Languages, (text, token) => super.Synthesize(text, voice ?? "M1",
                        options.Steps < 1 ? 4 : options.Steps, options.Speed, options.Seed, token, string.IsNullOrWhiteSpace(options.Language) ? "en" : options.Language));
                case MNNSpeechModelKind.BertVits2:
                    var bert = MNNBertVits2.Load(directory, threadCount, backendType: backendType);
                    return new MNNSpeechSynthesizer(bert, bert.SampleRate, new[] { "zh", "en" }, bert.Synthesize);
                case MNNSpeechModelKind.SherpaVitsChinese:
                    var sherpa = MNNSherpaVitsChinese.Load(directory, threadCount);
                    return new MNNSpeechSynthesizer(sherpa, sherpa.SampleRate, new[] { "zh" }, (text, token) => sherpa.Synthesize(text, 0, options.Speed, token));
                case MNNSpeechModelKind.MossNano:
                    var moss = MNNMossNano.Load(directory, threadCount);
                    return new MNNSpeechSynthesizer(moss, 48000, new[] { "zh", "en" }, (text, token) => moss.Synthesize(text, voice ?? "Junhao", options.Seed, cancellationToken: token));
                default: throw new NotSupportedException("No supported speech synthesis export profile in " + directory);
            }
        }
        private static string IndicVoiceLanguage(string voice)
        {
            switch (voice.Split('_')[0])
            {
                case "ASM": return "as";
                case "BEN": return "bn";
                case "BRX": return "brx";
                case "DOI": return "doi";
                case "KAN": return "kn";
                case "MAI": return "mai";
                case "MAL": return "ml";
                case "MAR": return "mr";
                case "NEP": return "ne";
                case "PAN": return "pa";
                case "SAN": return "sa";
                case "TAM": return "ta";
                case "TEL": return "te";
                default: throw new ArgumentException("Unknown IndicVITS voice: " + voice);
            }
        }
        public MNNGeneratedAudio Synthesize(string text, CancellationToken cancellationToken = default) => _synthesize(text, cancellationToken);
        public void Dispose() => _owner.Dispose();
    }
}
