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
    public sealed partial class MNNParaformerRecognizer : IDisposable
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
                using (var stream = CreateStream())
                {
                    for (int offset = 0; offset < pcm.Length; offset += 480000)
                        stream.AcceptWaveform(pcm, offset, Math.Min(480000, pcm.Length - offset), cancellationToken);
                    return stream.InputFinished(cancellationToken);
                }
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
