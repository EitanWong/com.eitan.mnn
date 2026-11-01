using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>Reuses local models for scripts the preferred voice cannot pronounce. Never downloads models.
    /// Construction loads no native models; synthesize and dispose from one serialized owner.</summary>
    public sealed class MNNLocalSpeechFallback : IDisposable
    {
        private static string[] _applicationRoots = Array.Empty<string>();

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CaptureApplicationRoots()
        {
            _applicationRoots = new[]{Path.Combine(UnityEngine.Application.streamingAssetsPath, "MNN", "Models"),
                Path.Combine(UnityEngine.Application.persistentDataPath, "MNN", "Models")};
        }

        private readonly string _preferred;
        private readonly string[] _roots;
        private readonly int _sampleRate, _threads;
        private IDisposable _model;
        private Func<string, CancellationToken, MNNGeneratedAudio> _synthesize;
        private string[] _languages;
        private string _fallbackDirectory;
        private MNNTtsModelProfile _profile;
        public string LastModelDirectory { get; private set; }
        private bool _disposed;

        public MNNLocalSpeechFallback(string preferredDirectory, int sampleRate, int threads = 1, string[] modelRoots = null)
        {
            if (sampleRate < 8000 || sampleRate > 192000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (threads < 1 || threads > 16) throw new ArgumentOutOfRangeException(nameof(threads));
            _preferred = Path.GetFullPath(preferredDirectory);
            _sampleRate = sampleRate;
            _threads = threads;
            _roots = (modelRoots ?? new[]{Path.GetDirectoryName(_preferred),
#if UNITY_EDITOR
                Path.Combine(Directory.GetCurrentDirectory(), "Assets", "StreamingAssets", "MNN", "Models"),
#endif
            }.Concat(_applicationRoots).ToArray()).Where(path =>
                !string.IsNullOrWhiteSpace(path) && !path.Contains("://")).Select(Path.GetFullPath).Distinct().ToArray();
        }

        public MNNGeneratedAudio Synthesize(string text, string[] preferredLanguages,
            Func<MNNGeneratedAudio> preferred, CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(MNNLocalSpeechFallback));
            if (preferred == null) throw new ArgumentNullException(nameof(preferred));
            if (preferredLanguages == null) throw new ArgumentNullException(nameof(preferredLanguages));
            cancellationToken.ThrowIfCancellationRequested();
            if (!MNNSpeechLanguages.HasSpeech(text))
                return new MNNGeneratedAudio(new float[_sampleRate / 100], _sampleRate, text);
            if (MNNSpeechLanguages.SupportsText(text, preferredLanguages))
            {
                try
                {
                    var result = Convert(preferred(), cancellationToken);
                    LastModelDirectory = _preferred;
                    return result;
                }
                catch (MNNSpeechLanguageException) { }

            }
            bool mixed = MNNSpeechLanguages.Scripts(text).Contains("han") && MNNSpeechLanguages.Scripts(text).Contains("latin");
            if (_synthesize != null && (!mixed || _profile != MNNTtsModelProfile.Kokoro) && MNNSpeechLanguages.SupportsText(text, _languages))
            {
                try { var audio = Convert(_synthesize(text, cancellationToken), cancellationToken); LastModelDirectory = _fallbackDirectory; return audio; }
                catch (NotSupportedException) { Release(); }
            }
            var candidates = new List<(string Directory, MNNTtsModelProfile Profile, string[] Languages)>();
            foreach (string directory in InstalledDirectories())
            {
                if (directory == _preferred) continue;
                try
                {
                    var profile = MNNTtsModelProfiles.Detect(directory);
                    string[] languages = Languages(directory, profile);
                    if (languages.Length > 0 && MNNSpeechLanguages.SupportsText(text, languages))
                        candidates.Add((directory, profile, languages));
                }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException || error is NotSupportedException) { }
            }
            foreach (var candidate in candidates.OrderBy(item => Priority(item.Profile)).ThenBy(item => item.Directory, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                Release();
                try { Load(candidate.Directory, candidate.Profile); }
                catch (Exception error) when (error is IOException || error is ArgumentException || error is NotSupportedException)
                {
                    Release();
                    continue;
                }
                _languages = candidate.Languages;
                _fallbackDirectory = candidate.Directory;
                try
                {
                    var result = Convert(_synthesize(text, cancellationToken), cancellationToken);
                    LastModelDirectory = candidate.Directory;
                    return result;
                }
                catch (NotSupportedException) { Release(); }
            }
            throw new MNNSpeechLanguageException("No installed speech model supports this input (scripts: " +
                string.Join(", ", MNNSpeechLanguages.Scripts(text)) + "). Install a matching voice/model. The selected voice supports " +
                string.Join(", ", preferredLanguages) + ".");
        }

        private MNNGeneratedAudio Convert(MNNGeneratedAudio audio, CancellationToken token) => audio.SampleRate == _sampleRate ? audio :
            new MNNGeneratedAudio(MNNSpeechDsp.Resample(audio.Waveform, audio.SampleRate, _sampleRate, token), _sampleRate, audio.Text);

        private IEnumerable<string> InstalledDirectories()
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string root in _roots)
            {
                if (!Directory.Exists(root)) continue;
                string[] children;
                try { children = Directory.GetDirectories(root); }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException) { continue; }
                foreach (string child in children.OrderBy(path => path, StringComparer.Ordinal))
                    if (!Path.GetFileName(child).StartsWith(".", StringComparison.Ordinal) && seen.Add(child)) yield return child;
            }
        }

        internal static string[] Languages(string directory, MNNTtsModelProfile profile)
        {
            switch (profile)
            {
                case MNNTtsModelProfile.BertVits2: return new[]{"zh", "en"};
                case MNNTtsModelProfile.Kokoro:
                    return new[]{File.Exists(Path.Combine(directory, "lexicon-zh.txt")) ? "zh" : null,
                        File.Exists(Path.Combine(directory, "lexicon-us-en.txt")) ? "en" : null}.Where(value => value != null).ToArray();
                case MNNTtsModelProfile.Supertonic:
                    return MNNSupertonic.LanguagesForVersion((string)MNNModelData.Get(MNNModelData.Read(Path.Combine(directory, "mnn_models", "tts.json")), "tts_version"));
                case MNNTtsModelProfile.Piper: return MNNPiper.ReadLanguages(directory);
                case MNNTtsModelProfile.SherpaVitsChinese: return new[]{"zh"};
                case MNNTtsModelProfile.CosyVoice: return new[]{"zh", "en"};
                case MNNTtsModelProfile.Qwen3Explicit: return new[]{"zh", "en"};
                default: return Array.Empty<string>();
            }
        }

        private static int Priority(MNNTtsModelProfile profile) => profile == MNNTtsModelProfile.BertVits2 ? 0 :
            profile == MNNTtsModelProfile.Kokoro ? 1 : profile == MNNTtsModelProfile.Piper ? 2 :
            profile == MNNTtsModelProfile.Supertonic ? 3 : profile == MNNTtsModelProfile.SherpaVitsChinese ? 4 : 5;

        private void Load(string directory, MNNTtsModelProfile profile)
        {
            _profile = profile;
            switch (profile)
            {
                case MNNTtsModelProfile.Kokoro:
                    var kokoro = MNNKokoro.Load(directory, _threads); _model = kokoro;
                    _synthesize = (text, token) =>
                    {
                        bool chinese = MNNSpeechLanguages.Scripts(text).Contains("han");
                        string voice = kokoro.Voices.FirstOrDefault(value => value == (chinese ? "zf_xiaoxiao" : "af_heart")) ??
                            kokoro.Voices.FirstOrDefault(value => value.StartsWith(chinese ? "z" : "a", StringComparison.Ordinal)) ?? kokoro.Voices[0];
                        return kokoro.Synthesize(text, voice, cancellationToken: token);
                    }; break;
                case MNNTtsModelProfile.BertVits2:
                    var bert = MNNBertVits2.Load(directory, _threads); _model = bert;
                    _synthesize = (text, token) => bert.Synthesize(text, token); break;
                case MNNTtsModelProfile.Supertonic:
                    string precision = new[]{"fp16", "fp32", "int8"}.First(value => Directory.Exists(Path.Combine(directory, "mnn_models", value)));
                    var supertonic = MNNSupertonic.Load(directory, _threads, precision); _model = supertonic;
                    _synthesize = (text, token) => supertonic.Synthesize(text, supertonic.Voices[0], cancellationToken: token,
                        language: MNNSpeechLanguages.Select(text, supertonic.Languages)); break;
                case MNNTtsModelProfile.Piper:
                    var piper = MNNPiper.Load(directory, _threads); _model = piper;
                    _synthesize = (text, token) => piper.Synthesize(text, cancellationToken: token); break;
                case MNNTtsModelProfile.SherpaVitsChinese:
                    var sherpa = MNNSherpaVitsChinese.Load(directory, _threads); _model = sherpa;
                    _synthesize = (text, token) => sherpa.Synthesize(text, cancellationToken: token); break;
                case MNNTtsModelProfile.CosyVoice:
                    var cosy = MNNCosyVoice.Load(directory, _threads); _model = cosy;
                    _synthesize = (text, token) => cosy.Synthesize(text, cancellationToken: token); break;
                case MNNTtsModelProfile.Qwen3Explicit:
                    var qwen = MNNQwen3ExplicitTts.Load(directory, _threads); _model = qwen;
                    _synthesize = (text, token) => qwen.Synthesize(text, language: MNNSpeechLanguages.Scripts(text).Contains("han") ? "zh" : "en", cancellationToken: token); break;
                default: throw new NotSupportedException("No local fallback frontend for " + profile);
            }
        }

        private void Release() { _model?.Dispose(); _model = null; _synthesize = null; _languages = null; }
        public void Dispose() { if (_disposed) return; _disposed = true; Release(); }
    }
}
