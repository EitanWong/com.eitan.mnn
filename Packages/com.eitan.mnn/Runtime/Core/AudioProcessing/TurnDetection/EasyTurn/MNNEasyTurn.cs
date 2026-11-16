using System;
using System.IO;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>ASLP Easy-Turn acoustic transcription and four-way turn classification.</summary>
    public sealed partial class MNNEasyTurn : IDisposable
    {
        public const string ConfigFileName = "mnn_easy_turn.json";
        private readonly object _gate = new object();
        private readonly MNNGenerationGraph _audio, _decoder;
        private readonly MNNLiveKitTokenizer _tokenizer;
        private readonly FileStream _embeddings;
        private readonly float[] _boundaries, _prompt;
        private bool _disposed;
        public MNNBackendType Backend => _audio.Backend;
        private MNNEasyTurn(string directory, int threads)
        {
            MNNEasyTurnProfile.Verify(directory);
            _tokenizer = new MNNLiveKitTokenizer(directory, qwen: true);
            _embeddings = File.OpenRead(Path.Combine(directory, "embeddings_bf16.bin"));
            try
            {
                var config = MNNEasyTurnProfile.Read(directory);
                _prompt = Embed(_tokenizer.Encode(config.prompt));
                byte[] boundaries = File.ReadAllBytes(Path.Combine(directory, "speech-boundary.f32"));
                _boundaries = new float[1792]; Buffer.BlockCopy(boundaries, 0, _boundaries, 0, boundaries.Length);
                _audio = new MNNGenerationGraph(Path.Combine(directory, "audio.mnn"), threads,
                    backendType: MNNBackendType.CPU, precisionMode: MNNPrecisionMode.High);
                _decoder = new MNNGenerationGraph(Path.Combine(directory, "decoder.mnn"), threads,
                    backendType: MNNBackendType.CPU, precisionMode: MNNPrecisionMode.High);
            }
            catch { _audio?.Dispose(); _decoder?.Dispose(); _embeddings.Dispose(); throw; }
        }
        public static MNNEasyTurn Load(string directory, int threadCount = 2, MNNBackendType backendType = MNNBackendType.CPU)
        {
            MNNPlatformSupport.RequireSupported();
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentNullException(nameof(directory));
            if (threadCount < 1 || threadCount > 64) throw new ArgumentOutOfRangeException(nameof(threadCount));
            if (backendType != MNNBackendType.CPU && backendType != MNNBackendType.Auto)
                throw new NotSupportedException("Easy-Turn currently qualifies CPU inference only.");
            return new MNNEasyTurn(Path.GetFullPath(directory), threadCount);
        }
        public MNNEasyTurnResult Predict(float[] samples, int sampleRate, CancellationToken cancellationToken = default)
        {
            if (samples == null || samples.Length == 0 || sampleRate < 8000 || sampleRate > 96000 || samples.Length > (long)sampleRate * 600)
                throw new ArgumentException("Supply normalized mono PCM at 8–96 kHz, up to ten minutes.");
            foreach (float sample in samples)
                if (float.IsNaN(sample) || float.IsInfinity(sample) || Math.Abs(sample) > 1) throw new ArgumentException("Expected normalized finite PCM.", nameof(samples));
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(MNNEasyTurn));
                cancellationToken.ThrowIfCancellationRequested();
                var pcm = MNNSpeechDsp.Resample(samples, sampleRate, 16000, cancellationToken, tailSamples: 128000);
                var features = Features(pcm, out int frames, cancellationToken);
                var output = _audio.RunOutputs(new[] { "embeddings", "speech_length" },
                    new MNNGenerationGraph.Input("features", features, 1, 800, 80),
                    new MNNGenerationGraph.Input("lengths", new[] { frames }, 1));
                int length = checked((int)output[1].Values[0]);
                if (length < 1 || length > 100) throw new InvalidDataException("Invalid Easy-Turn acoustic length.");
                var initial = new float[_prompt.Length + (length + 2) * 896];
                Array.Copy(_prompt, initial, _prompt.Length);
                Array.Copy(_boundaries, 0, initial, _prompt.Length, 896);
                Array.Copy(output[0].Values, 0, initial, _prompt.Length + 896, length * 896);
                Array.Copy(_boundaries, 896, initial, initial.Length - 896, 896);
                var tokens = Generate(initial, cancellationToken);
                return new MNNEasyTurnResult(_tokenizer.Decode(tokens), tokens);
            }
        }
        internal static float[] Features(float[] pcm, out int frames, CancellationToken token)
        {
            var mel = MNNWhisperFeatures.ExtractUnpaddedCentered(pcm, out frames, token);
            var output = new float[800 * 80];
            for (int t = 0; t < frames; ++t)
                for (int m = 0; m < 80; ++m) output[t * 80 + m] = mel[m * frames + t];
            return output;
        }
        private float[] Embed(int[] tokens)
        {
            var values = new float[checked(tokens.Length * 896)];
            var bytes = new byte[896 * 2]; var bits = new int[896];
            for (int i = 0; i < tokens.Length; ++i)
            {
                if (tokens[i] < 0 || tokens[i] >= 151936) throw new InvalidDataException("Invalid Easy-Turn token ID.");
                _embeddings.Position = (long)tokens[i] * bytes.Length;
                int offset = 0;
                while (offset < bytes.Length)
                {
                    int count = _embeddings.Read(bytes, offset, bytes.Length - offset);
                    if (count == 0) throw new EndOfStreamException("Incomplete Easy-Turn embeddings.");
                    offset += count;
                }
                for (int j = 0; j < 896; ++j) bits[j] = (bytes[j * 2] | bytes[j * 2 + 1] << 8) << 16;
                Buffer.BlockCopy(bits, 0, values, i * 896 * 4, 896 * 4);
            }
            return values;
        }
        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return; _disposed = true;
                _audio.Dispose(); _decoder.Dispose(); _embeddings.Dispose();
            }
        }
    }
}
