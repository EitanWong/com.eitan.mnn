using System;
using System.IO;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>FS-EEND complete-recording diarization with causal attention, original lookahead and five posterior columns.</summary>
    public sealed class MNNFsEendDiarization : IDisposable
    {
        public const int SampleRate = 8000, DefaultThreadCount = 8;
        private readonly object _gate = new object();
        private readonly MNNGenerationGraph _graph;
        private readonly MNNEendFeatures _features;
        private bool _disposed;
        public MNNBackendType Backend => _graph.Backend;
        public string ModelName { get; }

        private MNNFsEendDiarization(string directory, int threads, MNNBackendType backend)
        {
            var config = MNNFsEendModel.ReadVerified(directory) ?? throw new InvalidDataException("FS-EEND profile or runtime file verification failed.");
            ModelName = MNNFsEendProfile.Find(config.checkpoint_sha256).Name;
            _features = new MNNEendFeatures(config.analysis_window, config.mel_filters, false);
            if (backend == MNNBackendType.Auto) backend = MNNBackendType.CPU;
            _graph = new MNNGenerationGraph(Path.Combine(directory, MNNFsEendModel.Graph), threads, backendType: backend,
                precisionMode: MNNPrecisionMode.High, gpuTuningMode: MNNGpuTuningMode.None);
        }
        public static MNNFsEendDiarization Load(string directory, int threadCount = DefaultThreadCount, MNNBackendType backendType = MNNBackendType.Auto)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentNullException(nameof(directory));
            if (threadCount < 1 || threadCount > 64) throw new ArgumentOutOfRangeException(nameof(threadCount));
            MNNAcceleration.Validate(backendType);
            if (backendType != MNNBackendType.Auto && backendType != MNNBackendType.CPU && backendType != MNNBackendType.Metal)
                throw new NotSupportedException("FS-EEND execution is unverified on this backend.");
            MNNPlatformSupport.RequireSupported();
            return new MNNFsEendDiarization(Path.GetFullPath(directory), threadCount, backendType);
        }
        /// <summary>Analyze 1–480000 finite mono PCM samples at exactly 8 kHz. Each complete recording has independent attention context.</summary>
        public MNNFsEendActivity Analyze(float[] samples, int sampleRate = SampleRate, CancellationToken cancellationToken = default)
        {
            Validate(samples, sampleRate, cancellationToken);
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(MNNFsEendDiarization));
                var features = _features.Extract(samples, cancellationToken); int frames = features.Length / 345;
                var mask = new float[checked(frames * frames)];
                for (int t = 0; t < frames; ++t)
                    for (int k = t + 1; k < frames; ++k) mask[t * frames + k] = -1e9f;
                cancellationToken.ThrowIfCancellationRequested();
                var result = _graph.RunOutputs(new[] { "posterior" }, new MNNGenerationGraph.Input("features", features, 1, frames, 345),
                    new MNNGenerationGraph.Input("causal_mask", mask, frames, frames))[0];
                if (result.Shape.Length != 2 || result.Shape[0] != frames || result.Shape[1] != 5)
                    throw new InvalidDataException("Unexpected FS-EEND posterior shape.");
                cancellationToken.ThrowIfCancellationRequested();
                return new MNNFsEendActivity(result.Values, (double)samples.Length / SampleRate);
            }
        }
        public MNNFsEendActivity AnalyzeWaveFile(string path, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested(); var samples = MNNWaveAudio.Read(path, out int rate);
            return Analyze(samples, rate, cancellationToken);
        }
        private static void Validate(float[] samples, int rate, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (rate != SampleRate) throw new ArgumentOutOfRangeException(nameof(rate), "FS-EEND requires 8 kHz PCM.");
            if (samples == null || samples.Length < 1 || samples.Length > 480000) throw new ArgumentException("Supply 1–480000 mono samples.", nameof(samples));
            for (int i = 0; i < samples.Length; ++i)
            {
                if ((i & 16383) == 0) token.ThrowIfCancellationRequested();
                if (float.IsNaN(samples[i]) || float.IsInfinity(samples[i])) throw new ArgumentException("Supply finite floating-point PCM.", nameof(samples));
            }
        }
        public void Dispose() { lock (_gate) { if (_disposed) return; _disposed = true; _graph.Dispose(); } }
    }
}
