using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    /// <summary>Offline greedy CTC ASR for the two verified ModelScope/Hugging Face SenseVoiceSmall profiles.</summary>
    public sealed class MNNSenseVoiceRecognizer : IDisposable
    {
        private readonly object _gate = new object();
        private readonly MNNGenerationGraph _graph;
        private readonly bool _huggingFace;
        private readonly string[] _tokens;
        private readonly int _language, _textNorm;
        private bool _disposed;
        public MNNBackendType Backend => _graph.Backend;

        private MNNSenseVoiceRecognizer(string directory, MNNSenseVoiceConfig config, int threads, MNNBackendType backendType)
        {
            string[] languages = {"auto", "zh", "en", "ja", "ko", "yue"};
            int[] ids = {0, 3, 4, 11, 12, 7};
            int language = Array.IndexOf(languages, config.language);
            if (language < 0) throw new ArgumentException("SenseVoice language must be auto, zh, en, ja, ko or yue.", nameof(config));
            _huggingFace = MNNSenseVoiceModel.IsHuggingFace(config);
            _language = ids[language];
            _textNorm = config.useInverseTextNormalization ? 14 : 15;
            MNNSenseVoiceModel.Verify(directory, config);
            _tokens = ReadTokens(MNNZipformerConfig.Resolve(directory, config.tokens));
            // The Android FP32 GPU route is opt-in: on Mali-G57 it is slower
            // than CPU and default GPU precision produces invalid logits.
#if UNITY_ANDROID && !UNITY_EDITOR
            if (backendType == MNNBackendType.Auto) backendType = MNNBackendType.CPU;
#endif
            var backend = MNNAcceleration.Resolve(MNNAcceleration.ForDynamicGraph(backendType));
            if (backend == MNNBackendType.Metal || _huggingFace) backend = MNNBackendType.CPU;
            var precision = backend == MNNBackendType.CPU ? MNNPrecisionMode.Normal : MNNPrecisionMode.High;
            string path = MNNZipformerConfig.Resolve(directory, config.model);
            var prepared = _huggingFace ? null : MNNSenseVoiceGraph.WithGreedyOutput(File.ReadAllBytes(path));
            _graph = new MNNGenerationGraph(path, threads, prepared,
                express: _huggingFace, backendType: backend, precisionMode: precision);
        }

        public static MNNSenseVoiceRecognizer Load(string directory, MNNSenseVoiceConfig config = null, int threadCount = 4, MNNBackendType backendType = MNNBackendType.Auto)
        {
            MNNPlatformSupport.RequireSupported();
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentNullException(nameof(directory));
            if (threadCount < 1 || threadCount > 64) throw new ArgumentOutOfRangeException(nameof(threadCount));
            return new MNNSenseVoiceRecognizer(Path.GetFullPath(directory), config ?? MNNSenseVoiceConfig.Read(directory), threadCount, backendType);
        }

        /// <summary>Recognize finite normalized mono PCM at 8–96 kHz, 85 ms to 60 seconds per recording.</summary>
        public MNNSpeechRecognition Recognize(float[] samples, int sampleRate, CancellationToken cancellationToken = default)
        {
            if (sampleRate < 8000 || sampleRate > 96000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (samples == null || samples.Length < Math.Ceiling(sampleRate * .085) || samples.Length > (long)sampleRate * 60)
                throw new ArgumentException("SenseVoice expects 85 ms to 60 seconds of mono audio.", nameof(samples));
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(MNNSenseVoiceRecognizer));
                bool silent = true;
                for (int i = 0; i < samples.Length; ++i)
                {
                    if ((i & 16383) == 0) cancellationToken.ThrowIfCancellationRequested();
                    silent &= samples[i] == 0;
                    if (float.IsNaN(samples[i]) || float.IsInfinity(samples[i]) || Math.Abs(samples[i]) > 1)
                        throw new ArgumentException("Audio must contain finite normalized PCM in [-1,1].", nameof(samples));
                }
                // Exact digital silence carries no speech; avoid acoustic hallucinations without a VAD threshold.
                if (silent) return new MNNSpeechRecognition(string.Empty, Array.Empty<int>(), Array.Empty<float>());
                var pcm = MNNSpeechDsp.Resample(samples, sampleRate, 16000, cancellationToken);
                var features = MNNSenseVoiceFeatures.Extract(pcm, out int frames, cancellationToken);
                var inputs = new[]
                {
                    new MNNGenerationGraph.Input(_huggingFace ? "x" : "speech", features, 1, frames, 560),
                    new MNNGenerationGraph.Input(_huggingFace ? "x_length" : "speech_lengths", new[] {frames}, 1),
                    new MNNGenerationGraph.Input("language", new[] {_language}, 1),
                    new MNNGenerationGraph.Input("text_norm", new[] {_textNorm}, 1)
                };
                // HF has data-dependent shape operators and needs the existing Express API.
                if (!_huggingFace)
                {
                    var result = _graph.RunOutputs(new[] { MNNSenseVoiceGraph.OutputName }, inputs)[0];
                    if (!(result.Data is int[] ids) || !result.Shape.SequenceEqual(new[] { 1, frames + 4 }))
                        throw new InvalidDataException("Unexpected SenseVoice token shape or type.");
                    return Decode(ids, cancellationToken);
                }
                var logits = _graph.Run("logits", inputs);
                if (logits.Length != checked((frames + 4) * 25055))
                    throw new InvalidDataException("Unexpected SenseVoice logits shape.");
                return Decode(GreedyIds(logits, cancellationToken), cancellationToken);
            }
        }

        private int[] GreedyIds(float[] logits, CancellationToken token)
        {
            var ids = new int[logits.Length / _tokens.Length];
            for (int f = 0; f < ids.Length; ++f)
            {
                token.ThrowIfCancellationRequested();
                int offset = f * _tokens.Length, best = 0;
                for (int k = 1; k < _tokens.Length; ++k)
                    if (logits[offset + k] > logits[offset + best]) best = k;
                ids[f] = best;
            }
            return ids;
        }

        private MNNSpeechRecognition Decode(int[] bestIds, CancellationToken token) => Decode(bestIds, _tokens, token);

        internal static MNNSpeechRecognition Decode(int[] bestIds, string[] tokens, CancellationToken token)
        {
            var ids = new List<int>();
            var times = new List<float>();
            int previous = -1;
            var text = new StringBuilder();
            for (int f = 0; f < bestIds.Length; ++f)
            {
                token.ThrowIfCancellationRequested();
                int best = bestIds[f];
                if (best < 0 || best >= tokens.Length) throw new InvalidDataException("Invalid SenseVoice token ID.");
                // These four encoder positions are trained with rich-label cross entropy;
                // CTC speech starts at position four, independently of blank/repeated labels.
                if (f < 4) continue;
                if (best != 0 && best != previous)
                {
                    string symbol = tokens[best];
                    if (!(symbol.StartsWith("<|", StringComparison.Ordinal) && symbol.EndsWith("|>", StringComparison.Ordinal)))
                    {
                        ids.Add(best);
                        times.Add(Math.Max(0, f - 4) * .06f);
                        text.Append(symbol);
                    }
                }
                previous = best;
            }
            return new MNNSpeechRecognition(text.ToString().Replace("▁", " ").Trim(), ids.ToArray(), times.ToArray());
        }

        internal static string[] ReadTokens(string path)
        {
            var tokens = new string[25055];
            foreach (string line in File.ReadAllLines(path, new UTF8Encoding(false, true)))
            {
                int split = Math.Max(line.LastIndexOf(' '), line.LastIndexOf('\t'));
                if (split <= 0 || !int.TryParse(line.Substring(split + 1), out int id) || id < 0 || id >= tokens.Length || tokens[id] != null)
                    throw new InvalidDataException("Invalid SenseVoice vocabulary.");
                tokens[id] = line.Substring(0, split);
            }
            if (tokens.Any(t => t == null)) throw new InvalidDataException("Incomplete SenseVoice vocabulary.");
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
                _disposed = true;
                _graph.Dispose();
            }
        }
    }
}
