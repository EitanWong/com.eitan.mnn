using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    /// <summary>Chinese/English Zipformer2 keyword spotting using the bundled official MNN C++ ABI.
    /// Each instance owns one stream. Reset before changing recording; dispose after use.</summary>
    public sealed class MNNKeywordSpotter : IDisposable
    {
        /// <summary>Shared wake defaults for file, streaming, Studio and application callers.</summary>
        public const float DefaultThreshold = .15f;
        public const int DefaultTrailingBlanks = 0; // Automatic: English 4, Chinese 1.
        public const int DefaultMaxActivePaths = 0; // Automatic: English 6, Chinese 4.

        private readonly object _gate = new object();
        private readonly MNNInterpreter _encoder;
        private readonly MNNSession _session;
        private readonly MNNSingleInputModule _decoder;
        private readonly MNNGenerationGraph _joiner;
        private readonly Dictionary<string, int[]> _shapes = new Dictionary<string, int[]>();
        private readonly Dictionary<string, float[]> _states = new Dictionary<string, float[]>();
        private readonly List<float> _pcm = new List<float>();
        private readonly string[] _tokens;
        private readonly int[] _processed = new int[1];
        private MNNKeywordDecoder _search;
        private int _firstFrame;
        private long _acceptedSamples;
        private readonly int _primingSamples;
        private bool _disposed, _finished, _faulted;
        public MNNBackendType Backend => _session.ActualBackend;
        public MNNKeywordLanguage Language { get; }

        private MNNKeywordSpotter(string directory, int threads)
        {
            var profile = MNNKeywordSpotterConfig.Read(directory);
            if (!MNNKwsModel.IsRepository(profile.repository) && !MNNEnglishKwsModel.IsRepository(profile.repository))
                throw new InvalidDataException("Unsupported KWS profile; install a supported Chinese or English wake model.");
            Language = profile.Language;
            // A silent encoder hop gives the English model acoustic context before a short first word.
            // This primes caches without waiting for microphone audio or shifting the caller's timeline.
            _primingSamples = Language == MNNKeywordLanguage.English ? 32 * 160 : 0;
            MNNKwsModel.Verify(directory);
            string assets = MNNKwsModel.AssetDirectory(directory);
            _tokens = ReadTokens(Path.Combine(assets, "tokens.txt"), Language == MNNKeywordLanguage.English ? 500 : 197);
            _shapes["x"] = new[] {1, 45, 80};
            int[] contexts = {64, 32, 16, 8, 16, 32}, heads = {4, 4, 4, 8, 4, 4}, kernels = {15, 15, 7, 7, 7, 15};
            for (int i = 0; i < 6; ++i)
            {
                AddState("cached_key_" + i, contexts[i], 1, heads[i] * 32);
                AddState("cached_nonlin_attn_" + i, 1, 1, contexts[i], 96);
                AddState("cached_val1_" + i, contexts[i], 1, heads[i] * 12);
                AddState("cached_val2_" + i, contexts[i], 1, heads[i] * 12);
                AddState("cached_conv1_" + i, 1, 128, kernels[i]);
                AddState("cached_conv2_" + i, 1, 128, kernels[i]);
            }
            AddState("embed_states", 1, 128, 3, 19);
            _shapes["processed_lens"] = new[] {1};
            try
            {
                _encoder = MNNInterpreter.CreateFromFile(Path.Combine(assets, MNNKwsModel.Encoder));
                _session = _encoder.CreateSession(new MNNSessionConfig {BackendType = MNNBackendType.CPU, ThreadCount = threads});
                _session.ResizeInputs(_shapes);
                _decoder = new MNNSingleInputModule(Path.Combine(assets, MNNKwsModel.Decoder), "y", "decoder_out", threads, MNNBackendType.CPU);
                _joiner = new MNNGenerationGraph(Path.Combine(assets, MNNKwsModel.Joiner), threads, backendType: MNNBackendType.CPU);
                Reset(Language == MNNKeywordLanguage.English ? MNNKeywordSpotterConfig.EnglishDefaultWords : MNNKwsModel.DefaultKeywords);
            }
            catch
            {
                _joiner?.Dispose(); _decoder?.Dispose(); _session?.Dispose(); _encoder?.Dispose();
                throw;
            }
        }

        public static MNNKeywordSpotter Load(string directory, int threadCount = 2)
        {
            MNNPlatformSupport.RequireSupported();
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentNullException(nameof(directory));
            if (threadCount < 1 || threadCount > 64) throw new ArgumentOutOfRangeException(nameof(threadCount));
            return new MNNKeywordSpotter(Path.GetFullPath(directory), threadCount);
        }

        private void AddState(string name, params int[] shape)
        {
            _shapes[name] = shape;
            _states[name] = new float[shape.Aggregate(1, (a, b) => checked(a * b))];
        }

        internal static string[] ReadTokens(string path, int vocabularySize = 197)
        {
            var tokens = new string[vocabularySize];
            foreach (string line in File.ReadAllLines(path, new UTF8Encoding(false, true)))
            {
                int split = line.LastIndexOf(' ');
                // Kaldi disambiguation symbols are not neural-network logits.
                if (line.StartsWith("#", StringComparison.Ordinal)) continue;
                if (split <= 0 || !int.TryParse(line.Substring(split + 1), out int id) || id < 0 || id >= tokens.Length || tokens[id] != null)
                    throw new InvalidDataException("Invalid KWS token table.");
                tokens[id] = line.Substring(0, split).Normalize();
            }
            if (tokens.Any(string.IsNullOrEmpty) || tokens.Distinct().Count() != tokens.Length || tokens[0] != "<blk>" || tokens[2] != "<unk>")
                throw new InvalidDataException("The KWS token table does not match its model profile.");
            return tokens;
        }

        /// <summary>Set ordinary wake words in the loaded model language, one phrase per line (e.g. Hey Siri or 小爱同学).
        /// Automatically prepares model input and resets the stream. Legacy token specifications remain accepted.
        /// trailingBlanks=0 and maxActivePaths=0 select language-specific confirmation and beam width;
        /// positive values explicitly override them.</summary>
        public void Reset(string keywords, float threshold = DefaultThreshold, float score = 1.0f, int trailingBlanks = DefaultTrailingBlanks, int maxActivePaths = DefaultMaxActivePaths)
        {
            lock (_gate)
            {
                ThrowIfDisposed();
                var search = new MNNKeywordDecoder(MNNKeywordText.Prepare(keywords, Language), _tokens, threshold, score,
                    trailingBlanks == DefaultTrailingBlanks ? (Language == MNNKeywordLanguage.English ? 4 : 1) : trailingBlanks,
                    maxActivePaths == DefaultMaxActivePaths ? (Language == MNNKeywordLanguage.English ? 6 : 4) : maxActivePaths,
                    context => _decoder.Run(context, 1, 2),
                    (encoded, decoded) =>
                    {
                        if (decoded.Length != 320) throw new InvalidDataException("Expected 320-wide KWS decoder output.");
                        return _joiner.Run("logit", new MNNGenerationGraph.Input("encoder_out", encoded, 1, 320),
                            new MNNGenerationGraph.Input("decoder_out", decoded, 1, 320));
                    }, Language == MNNKeywordLanguage.English ? 1 : 2, Language == MNNKeywordLanguage.English ? 18 : 38);
                _search = search;
                _pcm.Clear();
                if (_primingSamples > 0) _pcm.AddRange(new float[_primingSamples]);
                _firstFrame = 0; _processed[0] = 0; _acceptedSamples = 0;
                _finished = false; _faulted = false;
                foreach (var state in _states) Array.Clear(state.Value, 0, state.Value.Length);
            }
        }

        /// <summary>Consume normalized mono PCM at exactly 16 kHz (at most 30 seconds per call).
        /// Retains bounded audio/state between calls; matches can span arbitrary chunk boundaries.</summary>
        public MNNKeywordDetection[] AcceptWaveform(float[] samples, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                ThrowIfDisposed();
                if (_finished || _faulted) throw new InvalidOperationException("Reset the keyword spotter before feeding a finished or interrupted stream.");
                ValidateSamples(samples, 16000 * 30, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                _pcm.AddRange(samples); _acceptedSamples += samples.Length;
                try { return Process(cancellationToken); }
                catch { _faulted = true; throw; }
            }
        }

        /// <summary>Flush delayed final matches once; Reset starts the next recording.</summary>
        public MNNKeywordDetection[] InputFinished(CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                ThrowIfDisposed();
                if (_faulted) throw new InvalidOperationException("Reset the interrupted keyword stream.");
                if (_finished) return Array.Empty<MNNKeywordDetection>();
                _finished = true;
                if (_acceptedSamples == 0) return Array.Empty<MNNKeywordDetection>();
                _pcm.AddRange(new float[16000]);
                try { return Process(cancellationToken); }
                catch { _faulted = true; throw; }
            }
        }

        private MNNKeywordDetection[] Process(CancellationToken token)
        {
            var matches = new List<MNNKeywordDetection>();
            while ((_firstFrame + 44) * 160 + 280 <= _pcm.Count)
            {
                token.ThrowIfCancellationRequested();
                // Only the current chunk is copied; preceding samples preserve Kaldi's reflected-edge convention.
                var audio = _pcm.Take((_firstFrame + 44) * 160 + 280).ToArray();
                NormalizeQuietAudio(audio);
                var features = MNNZipformerFeatures.ExtractFrames(audio, _firstFrame, 45, token);
                _session.GetInput("x").CopyFromArray(features);
                foreach (var state in _states) _session.GetInput(state.Key).CopyFromArray(state.Value);
                _session.GetInput("processed_lens").CopyFromArray(_processed);
                _session.Run();
                var output = _session.GetOutput("encoder_out");
                var encoded = output.CopyToArray<float>();
                if (!output.Shape.SequenceEqual(new[] {1, 8, 320}) || encoded.Any(value => !Finite(value)))
                    throw new InvalidDataException("Expected finite [1,8,320] KWS encoder output.");
                foreach (var state in _states)
                {
                    var tensor = _session.GetOutput("new_" + state.Key);
                    if (!tensor.Shape.SequenceEqual(_shapes[state.Key])) throw new InvalidDataException("Invalid KWS state: " + state.Key);
                    CopyState(tensor, state.Value);
                    if (state.Value.Any(value => !Finite(value))) throw new InvalidDataException("Non-finite KWS state.");
                }
                CopyState(_session.GetOutput("new_processed_lens"), _processed);
                for (int frame = 0; frame < 8; ++frame)
                {
                    token.ThrowIfCancellationRequested();
                    var values = new float[320];
                    Array.Copy(encoded, frame * 320, values, 0, 320);
                    var match = _search.Advance(values);
                    if (match == null) continue;
                    float timeOffset = _primingSamples / 16000f;
                    if (match.EndSeconds <= timeOffset || match.StartSeconds >= (_acceptedSamples + _primingSamples) / 16000.0) continue;
                    matches.Add(new MNNKeywordDetection(match.Keyword, match.Confidence,
                        Math.Max(0, match.StartSeconds - timeOffset), Math.Max(0, match.EndSeconds - timeOffset), match.TokenIds.ToArray()));
                }
                int discard = (_firstFrame + 31) * 160;
                _pcm.RemoveRange(0, discard);
                _firstFrame = 1;
            }
            return matches.ToArray();
        }

        private static void NormalizeQuietAudio(float[] audio)
        {
            // Bound quiet-microphone gain in the model's private feature copy only.
            // Leave near-silence alone and preserve the caller's capture/command waveform.
            float peak = audio.Max(sample => Math.Abs(sample));
            if (peak < .002f || peak >= .25f) return;
            float gain = Math.Min(10, .25f / peak);
            for (int i = 0; i < audio.Length; ++i) audio[i] *= gain;
        }

        private static void CopyState<T>(MNNTensor tensor, T[] destination) where T : unmanaged
        {
            var source = tensor.MapForRead<T>();
            try { source.CopyTo(destination); }
            finally { tensor.Unmap(); }
        }

        /// <summary>Detect all keyword occurrences in finite 8–96 kHz mono PCM, up to ten minutes.
        /// Resets this instance's stream; threshold and boost apply unless a keyword overrides them.</summary>
        public MNNKeywordDetection[] Detect(float[] samples, int sampleRate, string keywords,
            float threshold = DefaultThreshold, float score = 1, CancellationToken cancellationToken = default)
        {
            if (sampleRate < 8000 || sampleRate > 96000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            ValidateSamples(samples, sampleRate * 600, cancellationToken);
            if (samples.Length == 0) throw new ArgumentException("Choose nonempty audio.", nameof(samples));
            lock (_gate)
            {
                Reset(keywords, threshold, score);
                var pcm = MNNSpeechDsp.Resample(samples, sampleRate, 16000, cancellationToken);
                var matches = new List<MNNKeywordDetection>();
                for (int start = 0; start < pcm.Length; start += 16000)
                {
                    var chunk = new float[Math.Min(16000, pcm.Length - start)];
                    Array.Copy(pcm, start, chunk, 0, chunk.Length);
                    matches.AddRange(AcceptWaveform(chunk, cancellationToken));
                }
                matches.AddRange(InputFinished(cancellationToken));
                return matches.ToArray();
            }
        }

        public MNNKeywordDetection[] DetectWaveFile(string path, string keywords, float threshold = DefaultThreshold, float score = 1,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var samples = MNNWaveAudio.Read(path, out int rate);
            return Detect(samples, rate, keywords, threshold, score, cancellationToken);
        }

        public Task<MNNKeywordDetection[]> DetectAsync(float[] samples, int sampleRate, string keywords,
            float threshold = DefaultThreshold, float score = 1, CancellationToken cancellationToken = default)
        {
            var copy = samples == null ? null : (float[])samples.Clone();
            return Task.Run(() => Detect(copy, sampleRate, keywords, threshold, score, cancellationToken), cancellationToken);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static void ValidateSamples(float[] samples, int maximum, CancellationToken token)
        {
            if (samples == null || samples.Length > maximum) throw new ArgumentException("Audio exceeds the supported duration or is null.", nameof(samples));
            for (int i = 0; i < samples.Length; ++i)
            {
                if ((i & 16383) == 0) token.ThrowIfCancellationRequested();
                if (!Finite(samples[i]) || Math.Abs(samples[i]) > 1) throw new ArgumentException("Audio must contain finite normalized PCM in [-1,1].", nameof(samples));
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(MNNKeywordSpotter));
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                _joiner.Dispose(); _decoder.Dispose(); _session.Dispose(); _encoder.Dispose();
                _pcm.Clear();
            }
        }
    }
}
