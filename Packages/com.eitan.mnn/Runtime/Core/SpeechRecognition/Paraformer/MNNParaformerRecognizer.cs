using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    /// <summary>Chinese greedy ASR for the pinned 8404-token Paraformer online graph exports.</summary>
    public sealed class MNNParaformerRecognizer : IDisposable
    {
        private readonly object _gate = new object();
        private readonly MNNSingleInputModule _encoder;
        private readonly MNNGenerationGraph _decoder;
        private readonly string[] _tokens;
        private readonly string[] _outputs = new[] {"logits"}.Concat(Enumerable.Range(0, 16).Select(i => "out_cache_" + i)).ToArray();
        private static readonly float[] PositionScales = Enumerable.Range(0, 280)
            .Select(i => (float)Math.Exp(i * -0.03301197265941284)).ToArray();
        private bool _disposed;
        public MNNBackendType Backend => _encoder.Backend;
        public MNNBackendType DecoderBackend => _decoder.Backend;

        private MNNParaformerRecognizer(string directory, MNNParaformerConfig config, int threads, MNNBackendType backend)
        {
            MNNParaformerModel.ResolveRevision(null, config.repository, false);
            MNNParaformerModel.Verify(directory);
            _tokens = ReadTokens(Path.Combine(directory, "tokenizer.txt"));
            var selected = backend == MNNBackendType.Auto ? MNNBackendType.CPU : backend;
            try
            {
                string path = Path.Combine(directory, "encoder.mnn");
                var prepared = MNNParaformerGraph.PrepareEncoder(File.ReadAllBytes(path), 20);
                _encoder = new MNNSingleInputModule(path, "speech", MNNParaformerGraph.EncoderOutput, threads, selected, prepared);
                _decoder = new MNNGenerationGraph(Path.Combine(directory, "decoder.mnn"), threads, express: true, backendType: selected);
            }
            catch
            {
                _decoder?.Dispose();
                _encoder?.Dispose();
                throw;
            }
        }

        public static MNNParaformerRecognizer Load(string directory, MNNParaformerConfig config = null,
            int threadCount = 4, MNNBackendType backendType = MNNBackendType.Auto)
        {
            MNNPlatformSupport.RequireSupported();
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentNullException(nameof(directory));
            if (threadCount < 1 || threadCount > 64) throw new ArgumentOutOfRangeException(nameof(threadCount));
            if (!Enum.IsDefined(typeof(MNNBackendType), backendType)) throw new ArgumentOutOfRangeException(nameof(backendType));
            return new MNNParaformerRecognizer(Path.GetFullPath(directory), config ?? MNNParaformerConfig.Read(directory), threadCount, backendType);
        }

        /// <summary>Recognize normalized mono PCM at 8–96 kHz, 85 ms to ten minutes. No timestamp alignment.</summary>
        public MNNSpeechRecognition Recognize(float[] samples, int sampleRate, CancellationToken cancellationToken = default)
        {
            if (sampleRate < 8000 || sampleRate > 96000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (samples == null || samples.Length < Math.Ceiling(sampleRate * .085) || samples.Length > (long)sampleRate * 600)
                throw new ArgumentException("Paraformer expects 85 ms to ten minutes of mono audio.", nameof(samples));
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(MNNParaformerRecognizer));
                cancellationToken.ThrowIfCancellationRequested();
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
                Array.Resize(ref pcm, checked(pcm.Length + 16000)); // Flush acoustic right context before the CIF tail.
                var features = MNNSenseVoiceFeatures.Extract(pcm, out int frames, cancellationToken);
                var states = Enumerable.Range(0, 16).Select(i =>
                    new MNNGenerationGraph.Input("in_cache_" + i, new float[5120], 1, 512, 10)).ToArray();
                var overlap = new float[10 * 560];
                var chunk = new float[20 * 560];
                var encoded = new float[20 * 512];
                var hidden = new float[512];
                var embeddings = new List<float>(11 * 512);
                var ids = new List<int>();
                float integral = 0;
                for (int start = 0; start + 10 <= frames; start += 10)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Array.Copy(overlap, chunk, overlap.Length);
                    for (int t = 0; t < 10; ++t)
                        for (int k = 0; k < 560; ++k)
                        {
                            float position = (start + t + 1) * PositionScales[k % 280];
                            chunk[(10 + t) * 560 + k] = features[(start + t) * 560 + k] * 22.62741699796952f +
                                (float)(k < 280 ? Math.Sin(position) : Math.Cos(position));
                        }
                    Array.Copy(chunk, 10 * 560, overlap, 0, overlap.Length);
                    var packed = _encoder.Run(chunk, 1, 20, 560);
                    if (packed.Length != 20 * 513) throw new InvalidDataException("Invalid Paraformer encoder output.");
                    for (int t = 0; t < 20; ++t) Array.Copy(packed, t * 513, encoded, t * 512, 512);
                    embeddings.Clear();
                    for (int t = 5; t < 15; ++t)
                    {
                        float alpha = packed[t * 513 + 512];
                        if (alpha < 0 || alpha > 1) throw new InvalidDataException("Invalid Paraformer CIF weight.");
                        float contribution = Math.Min(alpha, 1 - integral);
                        for (int k = 0; k < 512; ++k) hidden[k] += contribution * encoded[t * 512 + k];
                        if (integral + alpha < 1) integral += alpha;
                        else
                        {
                            embeddings.AddRange(hidden);
                            integral += alpha - 1;
                            for (int k = 0; k < 512; ++k) hidden[k] = integral * encoded[t * 512 + k];
                        }
                    }
                    // FunASR's final zero hidden frame carries alpha=0.45, emitting a delayed final token.
                    if (start + 20 > frames && integral + .45f >= 1) embeddings.AddRange(hidden);
                    if (embeddings.Count == 0) continue;
                    cancellationToken.ThrowIfCancellationRequested();
                    int tokens = embeddings.Count / 512;
                    var inputs = new List<MNNGenerationGraph.Input>
                    {
                        new MNNGenerationGraph.Input("enc", encoded, 1, 20, 512),
                        new MNNGenerationGraph.Input("enc_len", new[] {20}, 1),
                        new MNNGenerationGraph.Input("acoustic_embeds", embeddings.ToArray(), 1, tokens, 512),
                        new MNNGenerationGraph.Input("acoustic_embeds_len", new[] {tokens}, 1)
                    };
                    inputs.AddRange(states);
                    var outputs = _decoder.RunOutputs(_outputs, inputs.ToArray());
                    if (!outputs[0].Shape.SequenceEqual(new[] {1, tokens, 8404}))
                        throw new InvalidDataException("Invalid Paraformer logits shape.");
                    for (int t = 0; t < tokens; ++t)
                    {
                        int offset = t * 8404, best = 0;
                        for (int k = 1; k < 8404; ++k)
                            if (outputs[0].Values[offset + k] > outputs[0].Values[offset + best]) best = k;
                        if (best > 2 && _tokens[best] != "<unk>") ids.Add(best);
                    }
                    for (int i = 0; i < 16; ++i)
                    {
                        if (!outputs[i + 1].Shape.SequenceEqual(new[] {1, 512, 10}))
                            throw new InvalidDataException("Invalid Paraformer decoder cache.");
                        states[i] = outputs[i + 1].AsInput("in_cache_" + i);
                    }
                }
                cancellationToken.ThrowIfCancellationRequested();
                return new MNNSpeechRecognition(Decode(ids.Select(id => _tokens[id])), ids.ToArray(), Array.Empty<float>());
            }
        }

        internal static string[] ReadTokens(string path)
        {
            string[] lines = File.ReadAllLines(path, new UTF8Encoding(false, true));
            if (lines.Length != 8408 || lines[3] != "8404") throw new InvalidDataException("Invalid Paraformer vocabulary.");
            try
            {
                var utf8 = new UTF8Encoding(false, true);
                var tokens = lines.Skip(4).Select(line => utf8.GetString(Convert.FromBase64String(line))).ToArray();
                if (tokens.Any(string.IsNullOrEmpty) || tokens[0] != "<blank>" || tokens[1] != "<s>" || tokens[2] != "</s>")
                    throw new InvalidDataException("Invalid Paraformer special tokens.");
                return tokens;
            }
            catch (FormatException error) { throw new InvalidDataException("Invalid Paraformer token encoding.", error); }
        }

        internal static string Decode(IEnumerable<string> tokens)
        {
            var text = new StringBuilder();
            bool merge = false, previousAscii = false;
            foreach (string token in tokens)
            {
                bool continuation = token.EndsWith("@@", StringComparison.Ordinal), ascii = token[0] < 128;
                string word = continuation ? token.Substring(0, token.Length - 2) : token;
                if (text.Length > 0 && (ascii && !merge || !ascii && previousAscii)) text.Append(' ');
                text.Append(word);
                merge = continuation && ascii;
                previousAscii = ascii;
            }
            return text.ToString().Trim();
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
                _decoder.Dispose();
                _encoder.Dispose();
            }
        }
    }
}
