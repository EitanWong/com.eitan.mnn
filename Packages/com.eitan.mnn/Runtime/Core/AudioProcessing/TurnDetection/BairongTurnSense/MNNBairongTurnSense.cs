using System;
using System.IO;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>Bairong TurnSense 1.0/1.1 acoustic complete/incomplete/invalid decisions, separate from SmolLM2 TurnSense.</summary>
    public sealed class MNNBairongTurnSense : IDisposable
    {
        private readonly object _gate = new object();
        private readonly MNNBairongTurnSenseFeatures _features;
        private readonly MNNGenerationGraph _graph;
        private bool _disposed;
        public MNNBackendType Backend => _graph.Backend;
        private MNNBairongTurnSense(string directory, int threads, MNNBackendType backend)
        {
            MNNBairongTurnSenseConfig.Verify(directory);
            _features = new MNNBairongTurnSenseFeatures(Path.Combine(directory, "am.mvn"));
            _graph = new MNNGenerationGraph(Path.Combine(directory, "model.mnn"), threads, backendType: backend,
                precisionMode: MNNPrecisionMode.High, gpuTuningMode: MNNGpuTuningMode.None);
        }
        public static MNNBairongTurnSense Load(string directory, int threadCount = 2, MNNBackendType backendType = MNNBackendType.CPU)
        {
            MNNPlatformSupport.RequireSupported();
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentNullException(nameof(directory));
            if (threadCount < 1 || threadCount > 64) throw new ArgumentOutOfRangeException(nameof(threadCount));
            if (backendType != MNNBackendType.CPU) throw new NotSupportedException("Bairong TurnSense currently qualifies CPU inference only.");
            return new MNNBairongTurnSense(Path.GetFullPath(directory), threadCount, backendType);
        }
        public MNNBairongTurnSenseResult Predict(float[] samples, int sampleRate, CancellationToken cancellationToken = default)
        {
            if (samples == null || samples.Length == 0 || sampleRate < 8000 || sampleRate > 96000 || samples.Length > (long)sampleRate * 600)
                throw new ArgumentException("Supply nonempty 8–96 kHz mono PCM up to ten minutes.");
            foreach (float sample in samples)
                if (float.IsNaN(sample) || float.IsInfinity(sample) || Math.Abs(sample) > 1) throw new ArgumentException("Expected normalized finite PCM.", nameof(samples));
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(MNNBairongTurnSense));
                cancellationToken.ThrowIfCancellationRequested();
                var pcm = MNNSpeechDsp.Resample(samples, sampleRate, 16000, cancellationToken, tailSamples: 128000);
                var features = _features.Extract(pcm, out int frames, cancellationToken);
                var padded = new float[133 * 560]; Array.Copy(features, padded, features.Length);
                var scores = _graph.Run("logits", new MNNGenerationGraph.Input("feats", padded, 1, 133, 560),
                    new MNNGenerationGraph.Input("feat_lengths", new[] { frames }, 1));
                cancellationToken.ThrowIfCancellationRequested();
                return new MNNBairongTurnSenseResult(scores);
            }
        }
        public void Dispose()
        {
            lock (_gate) { if (_disposed) return; _disposed = true; _graph.Dispose(); }
        }
    }
}
