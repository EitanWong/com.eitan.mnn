using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    /// <summary>Author VBx ResNet101 direct utterance entry, returning the original raw 256-dimensional x-vector.</summary>
    public sealed class MNNVbxEmbedding : IDisposable
    {
        public const int SampleRate = 16000, DefaultThreadCount = 8;
        private readonly object _gate = new object();
        private readonly MNNGenerationGraph _graph;
        private readonly MNNVbxFeatures _features;
        private bool _disposed;
        public MNNBackendType Backend => _graph.Backend;
        public string ModelName => MNNVbxModel.Name;

        private MNNVbxEmbedding(string directory, int threads, MNNBackendType backend)
        {
            var config = MNNVbxModel.ReadVerified(directory) ?? throw new InvalidDataException("VBx profile or runtime file verification failed.");
            _features = new MNNVbxFeatures(config);
            if (backend == MNNBackendType.Auto) backend = MNNAcceleration.IsBackendAvailable(MNNBackendType.Metal) ? MNNBackendType.Metal : MNNBackendType.CPU;
            _graph = new MNNGenerationGraph(Path.Combine(directory, MNNVbxModel.Graph), threads, backendType: backend,
                precisionMode: MNNPrecisionMode.High, gpuTuningMode: MNNGpuTuningMode.None);
        }
        public static MNNVbxEmbedding Load(string directory, int threadCount = DefaultThreadCount, MNNBackendType backendType = MNNBackendType.Auto)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentNullException(nameof(directory));
            if (threadCount < 1 || threadCount > 64) throw new ArgumentOutOfRangeException(nameof(threadCount));
            MNNAcceleration.Validate(backendType);
            if (backendType != MNNBackendType.Auto && backendType != MNNBackendType.CPU && backendType != MNNBackendType.Metal)
                throw new NotSupportedException("VBx execution is unverified on this backend.");
            MNNPlatformSupport.RequireSupported();
            return new MNNVbxEmbedding(Path.GetFullPath(directory), threadCount, backendType);
        }
        /// <summary>Encode 1520–160000 finite mono samples at exactly 16 kHz; retain the original vector scale.</summary>
        public float[] Encode(float[] samples, int sampleRate = SampleRate, CancellationToken cancellationToken = default)
        {
            Validate(samples, sampleRate, cancellationToken);
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(MNNVbxEmbedding));
                var features = _features.Extract(samples, cancellationToken); int frames = features.Length / 64;
                var input = new float[features.Length];
                for (int t = 0; t < frames; ++t)
                    for (int m = 0; m < 64; ++m) input[m * frames + t] = features[t * 64 + m];
                cancellationToken.ThrowIfCancellationRequested();
                var output = _graph.RunOutputs(new[] { "982" }, new MNNGenerationGraph.Input("x", input, 1, 64, frames))[0];
                if (output.Shape.Length != 2 || output.Shape[0] != 1 || output.Shape[1] != 256)
                    throw new InvalidDataException("Unexpected VBx embedding shape.");
                cancellationToken.ThrowIfCancellationRequested(); return output.Values;
            }
        }
        public float[] EncodeWaveFile(string path, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested(); var samples = MNNWaveAudio.Read(path, out int rate);
            return Encode(samples, rate, cancellationToken);
        }
        public Task<float[]> EncodeAsync(float[] samples, int sampleRate = SampleRate, CancellationToken cancellationToken = default)
        {
            var copy = samples == null ? null : (float[])samples.Clone();
            return Task.Run(() => Encode(copy, sampleRate, cancellationToken), cancellationToken);
        }
        public static float Similarity(float[] left, float[] right) => MNNVectorSimilarity.Cosine(left, right);
        private static void Validate(float[] samples, int rate, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (rate != SampleRate) throw new ArgumentOutOfRangeException(nameof(rate), "VBx requires 16 kHz PCM.");
            if (samples == null || samples.Length < 1520 || samples.Length > 160000)
                throw new ArgumentException("Supply 1520–160000 mono samples.", nameof(samples));
            for (int i = 0; i < samples.Length; ++i)
            {
                if ((i & 16383) == 0) token.ThrowIfCancellationRequested(); double scaled = samples[i] * 32768.0;
                if (double.IsNaN(scaled) || scaled < long.MinValue || scaled >= 9223372036854775808.0)
                    throw new ArgumentException("PCM must be finite and representable as signed 64-bit integer PCM after scaling.", nameof(samples));
            }
        }
        public void Dispose() { lock (_gate) { if (_disposed) return; _disposed = true; _graph.Dispose(); } }
    }
}
