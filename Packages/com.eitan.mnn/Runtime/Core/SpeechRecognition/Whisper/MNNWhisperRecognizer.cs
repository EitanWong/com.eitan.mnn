using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    /// <summary>Whisper greedy ASR using the verified encoder/decoder profiles.</summary>
    public sealed class MNNWhisperRecognizer : IDisposable
    {
        /// <summary>Whether the registered Whisper frontend implements Chinese token decoding.</summary>
        public static bool SupportsChinese(MNNWhisperConfig config) => MNNWhisperModel.ForConfig(config).ChineseAvailable;

        private readonly object _gate = new object();
        private readonly MNNGenerationGraph _encoder, _decoder;
        private readonly byte[][] _tokens;
        private readonly MNNWhisperModel.Profile _profile;
        private int[] _initialTokens;
        private string _language;
        private bool _disposed;
        public MNNBackendType Backend => _decoder.Backend;
        public string ModelName => _profile.Name;
        public string Language
        {
            get { lock (_gate) return _language; }
            set
            {
                lock (_gate)
                {
                    if (_disposed) throw new ObjectDisposedException(nameof(MNNWhisperRecognizer));
                    if (value != "en" && !(_profile.ChineseAvailable && (value == "zh" || value == "ta")))
                        throw new ArgumentException("Whisper language must be en; zh and ta are available for the small profile.", nameof(value));
                    _language = value;
                    _initialTokens = _profile.Multilingual ? new[] {50258, value == "zh" ? 50260 : value == "ta" ? 50287 : 50259, 50359, 50363} : new[] {50257, 50362};
                }
            }
        }

        private MNNWhisperRecognizer(string directory, MNNWhisperConfig config, int threads)
        {
            _profile = MNNWhisperModel.ForConfig(config);
            Language = config.language;
            MNNWhisperModel.Verify(directory, config);
            _tokens = ReadTokens(MNNZipformerConfig.Resolve(directory, config.tokens), _profile.EndToken);
            // Keep the decoder's data-dependent slicing on the verified CPU Express path.
            _encoder = new MNNGenerationGraph(MNNZipformerConfig.Resolve(directory, config.encoder), threads, backendType: MNNBackendType.CPU);
            try
            {
                _decoder = new MNNGenerationGraph(MNNZipformerConfig.Resolve(directory, config.decoder), threads, express: true, backendType: MNNBackendType.CPU);
            }
            catch { _encoder.Dispose(); throw; }
        }

        public static MNNWhisperRecognizer Load(string directory, MNNWhisperConfig config = null, int threadCount = 4)
        {
            MNNPlatformSupport.RequireSupported();
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentNullException(nameof(directory));
            if (threadCount < 1 || threadCount > 64) throw new ArgumentOutOfRangeException(nameof(threadCount));
            return new MNNWhisperRecognizer(Path.GetFullPath(directory), config ?? MNNWhisperConfig.Read(directory), threadCount);
        }

        /// <summary>Finite normalized mono PCM, 8–96 kHz, 25 ms to 29.5 seconds. Token timestamps are unavailable.</summary>
        public MNNSpeechRecognition Recognize(float[] samples, int sampleRate, CancellationToken cancellationToken = default)
        {
            if (sampleRate < 8000 || sampleRate > 96000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (samples == null || samples.Length < Math.Ceiling(sampleRate * .025) || samples.Length > sampleRate * 29.5)
                throw new ArgumentException("Whisper expects 25 ms to 29.5 seconds of mono audio.", nameof(samples));
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(MNNWhisperRecognizer));
                bool silent = true;
                for (int i = 0; i < samples.Length; ++i)
                {
                    if ((i & 16383) == 0) cancellationToken.ThrowIfCancellationRequested();
                    if (float.IsNaN(samples[i]) || float.IsInfinity(samples[i]) || Math.Abs(samples[i]) > 1)
                        throw new ArgumentException("Audio must contain finite normalized PCM in [-1,1].", nameof(samples));
                    silent &= samples[i] == 0;
                }
                if (silent) return new MNNSpeechRecognition(string.Empty, Array.Empty<int>(), Array.Empty<float>());
                var pcm = MNNSpeechDsp.Resample(samples, sampleRate, 16000, cancellationToken);
                var mel = MNNWhisperFeatures.Extract(pcm, out _, out int padded, cancellationToken);
                var cross = _encoder.RunOutputs(new[] {"n_layer_cross_k", "n_layer_cross_v"},
                    new MNNGenerationGraph.Input("mel", mel, 1, 80, padded));
                int[] crossShape = {_profile.Layers, 1, padded / 2, _profile.Width};
                if (cross.Any(output => !output.Shape.SequenceEqual(crossShape)))
                    throw new InvalidDataException("Unexpected Whisper encoder cache shape.");
                var selfShape = new[] {_profile.Layers, 1, 448, _profile.Width};
                var selfK = new MNNGenerationGraph.Input("in_n_layer_self_k_cache", new float[_profile.Layers * 448 * _profile.Width], selfShape);
                var selfV = new MNNGenerationGraph.Input("in_n_layer_self_v_cache", new float[_profile.Layers * 448 * _profile.Width], selfShape);
                var tokens = (int[])_initialTokens.Clone();
                int offset = 0;
                var predicted = new List<int>(); var bytes = new List<byte>();
                while (offset + tokens.Length < 448)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var inputs = new List<MNNGenerationGraph.Input>
                    {
                        new MNNGenerationGraph.Input("tokens", tokens, 1, tokens.Length), selfK, selfV,
                        new MNNGenerationGraph.Input("offset", new[] {offset}, 1)
                    };
                    // Cross-attention constants remain bound to this graph throughout the recording.
                    // Rebind them on the first step of every request; later tokens only update self KV.
                    if (offset == 0)
                    {
                        inputs.Add(cross[0].AsInput("n_layer_cross_k")); inputs.Add(cross[1].AsInput("n_layer_cross_v"));
                    }
                    var outputs = _decoder.RunOutputs(new[] {"logits", "out_n_layer_self_k_cache", "out_n_layer_self_v_cache"}, inputs.ToArray());
                    if (!outputs[0].Shape.SequenceEqual(new[] {1, tokens.Length, _profile.VocabularySize}) ||
                        !outputs[1].Shape.SequenceEqual(selfShape) || !outputs[2].Shape.SequenceEqual(selfShape))
                        throw new InvalidDataException("Unexpected Whisper decoder output shape.");
                    var logits = outputs[0].Values; int start = logits.Length - _profile.VocabularySize, best = 0;
                    // Text tokens plus EOT only; suppress prompts/language/timestamps for this profile.
                    for (int k = 1; k <= _profile.EndToken; ++k) if (logits[start + k] > logits[start + best]) best = k;
                    if (best == _profile.EndToken)
                        return new MNNSpeechRecognition(Encoding.UTF8.GetString(bytes.ToArray()).Trim(), predicted.ToArray(), Array.Empty<float>());
                    predicted.Add(best); bytes.AddRange(_tokens[best]);
                    offset += tokens.Length; tokens = new[] {best};
                    selfK = outputs[1].AsInput("in_n_layer_self_k_cache"); selfV = outputs[2].AsInput("in_n_layer_self_v_cache");
                }
                throw new InvalidDataException("Whisper reached its token context without an end-of-transcript token.");
            }
        }

        internal static byte[][] ReadTokens(string path, int count = 50256)
        {
            var tokens = new byte[count][];
            foreach (string line in File.ReadAllLines(path, new UTF8Encoding(false, true)))
            {
                int split = line.LastIndexOf(' ');
                if (split <= 0 || !int.TryParse(line.Substring(split + 1), out int id) || id < 0 || id >= tokens.Length || tokens[id] != null)
                    throw new InvalidDataException("Invalid Whisper vocabulary.");
                string encoded = line.Substring(0, split);
                // sherpa's multilingual vocabulary reserves its final text slot as an
                // empty base64 marker. Accept it only at that pinned protocol position.
                if (count == 50257 && id == 50256 && encoded == "=")
                {
                    tokens[id] = Array.Empty<byte>();
                    continue;
                }
                try { tokens[id] = Convert.FromBase64String(encoded); }
                catch (FormatException error) { throw new InvalidDataException("Invalid Whisper token bytes.", error); }
                if (tokens[id].Length == 0) throw new InvalidDataException("Empty Whisper token bytes.");
            }
            if (tokens.Any(t => t == null)) throw new InvalidDataException("Incomplete Whisper vocabulary.");
            return tokens;
        }

        public MNNSpeechRecognition RecognizeWaveFile(string path, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var samples = MNNWaveAudio.Read(path, out int rate);
            return Recognize(samples, rate, cancellationToken);
        }
        public Task<MNNSpeechRecognition> RecognizeAsync(float[] samples, int sampleRate, CancellationToken cancellationToken = default)
        {
            var copy = samples == null ? null : (float[])samples.Clone();
            return Task.Run(() => Recognize(copy, sampleRate, cancellationToken), cancellationToken);
        }
        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true; _decoder.Dispose(); _encoder.Dispose();
            }
        }
    }
}
