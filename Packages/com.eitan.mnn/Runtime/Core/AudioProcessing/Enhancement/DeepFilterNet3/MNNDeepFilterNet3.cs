using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    /// <summary>Offline 48 kHz DeepFilterNet3 enhancement. Each call resets the full feature/GRU sequence.
    /// Managed DSP follows libDF; the three exported graphs process the complete utterance.</summary>
    public sealed class MNNDeepFilterNet3 : IDisposable
    {
        public const int SampleRate = 48000;
        private readonly object _gate = new object();
        private readonly MNNGenerationGraph _encoder, _erbDecoder, _dfDecoder;
        private readonly MNNDeepFilterNet3Dsp _dsp = new MNNDeepFilterNet3Dsp();
        private bool _disposed;
        public MNNBackendType EncoderBackend => _encoder.Backend;
        public MNNBackendType ErbDecoderBackend => _erbDecoder.Backend;
        public MNNBackendType DfDecoderBackend => _dfDecoder.Backend;

        private MNNDeepFilterNet3(string directory, int threads, MNNBackendType backend)
        {
            if (!MNNDeepFilterNet3Model.IsComplete(directory)) throw new InvalidDataException("DeepFilterNet3 graph or profile verification failed.");
            bool cpu = MNNAcceleration.Resolve(backend) == MNNBackendType.CPU;
            try
            {
                _encoder = LoadGraph(directory, cpu ? MNNDeepFilterNet3Model.EncoderFp32 : MNNDeepFilterNet3Model.Encoder, threads, backend);
                _erbDecoder = LoadGraph(directory, cpu ? MNNDeepFilterNet3Model.ErbDecoderFp32 : MNNDeepFilterNet3Model.ErbDecoder, threads, backend);
                _dfDecoder = LoadGraph(directory, cpu ? MNNDeepFilterNet3Model.DfDecoderFp32 : MNNDeepFilterNet3Model.DfDecoder, threads, backend);
            }
            catch
            {
                _encoder?.Dispose(); _erbDecoder?.Dispose(); _dfDecoder?.Dispose();
                throw;
            }
        }
        private static MNNGenerationGraph LoadGraph(string directory, string graph, int threads, MNNBackendType backend) =>
            new MNNGenerationGraph(Path.Combine(directory, graph), threads, backendType: backend, precisionMode: MNNAcceleration.Resolve(backend) == MNNBackendType.CPU ? MNNPrecisionMode.High : MNNPrecisionMode.Normal);

        public static MNNDeepFilterNet3 Load(string directory, int threadCount = 4, MNNBackendType backendType = MNNBackendType.CPU)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentNullException(nameof(directory));
            if (threadCount < 1 || threadCount > 64) throw new ArgumentOutOfRangeException(nameof(threadCount));
            MNNPlatformSupport.RequireSupported();
            return new MNNDeepFilterNet3(Path.GetFullPath(directory), threadCount, backendType);
        }

        public float[] Enhance(float[] samples, int sampleRate = SampleRate, CancellationToken cancellationToken = default)
        {
            Validate(samples, sampleRate, cancellationToken);
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(MNNDeepFilterNet3));
                var pcm = MNNSpeechDsp.Resample(samples, sampleRate, SampleRate, cancellationToken);
                var features = _dsp.Analyze(pcm, cancellationToken);
                int frames = features.Frames;
                var names = new[] {"e0", "e1", "e2", "e3", "emb", "c0"};
                var encoded = _encoder.RunOutputs(names,
                    new MNNGenerationGraph.Input("feat_erb", features.Erb, 1, 1, frames, 32),
                    new MNNGenerationGraph.Input("feat_spec", features.Complex, 1, 2, frames, 96));
                cancellationToken.ThrowIfCancellationRequested();
                var mask = _erbDecoder.Run("m", encoded[4].AsInput("emb"), encoded[3].AsInput("e3"),
                    encoded[2].AsInput("e2"), encoded[1].AsInput("e1"), encoded[0].AsInput("e0"));
                cancellationToken.ThrowIfCancellationRequested();
                var coefficients = _dfDecoder.Run("coefs", encoded[4].AsInput("emb"), encoded[5].AsInput("c0"));
                var spectrum = _dsp.Filter(features, mask, coefficients, cancellationToken);
                var enhanced = _dsp.Synthesize(spectrum, frames, pcm.Length, cancellationToken);
                var output = sampleRate == SampleRate ? enhanced : MNNSpeechDsp.Resample(enhanced, SampleRate, sampleRate, cancellationToken);
                if (output.Length != samples.Length) Array.Resize(ref output, samples.Length);
                return output;
            }
        }

        public float[] EnhanceWaveFile(string path, out int sampleRate, CancellationToken cancellationToken = default)
        {
            var samples = MNNWaveAudio.Read(path, out sampleRate);
            return Enhance(samples, sampleRate, cancellationToken);
        }
        public Task<float[]> EnhanceAsync(float[] samples, int sampleRate = SampleRate, CancellationToken cancellationToken = default)
        {
            var copy = samples == null ? null : (float[])samples.Clone();
            return Task.Run(() => Enhance(copy, sampleRate, cancellationToken), cancellationToken);
        }
        private static void Validate(float[] samples, int sampleRate, CancellationToken token)
        {
            if (sampleRate < 8000 || sampleRate > 96000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (samples == null || samples.Length == 0 || samples.Length > (long)sampleRate * 30)
                throw new ArgumentException("Supply between one sample and 30 seconds of mono PCM.", nameof(samples));
            for (int i = 0; i < samples.Length; ++i)
            {
                if ((i & 16383) == 0) token.ThrowIfCancellationRequested();
                if (float.IsNaN(samples[i]) || float.IsInfinity(samples[i]) || Math.Abs(samples[i]) > 1)
                    throw new ArgumentException("Audio must contain finite normalized PCM in [-1,1].", nameof(samples));
            }
        }
        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                _encoder.Dispose(); _erbDecoder.Dispose(); _dfDecoder.Dispose();
            }
        }
    }
}
