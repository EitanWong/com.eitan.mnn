using System;
using System.IO;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>Offline FullSubNet complex ratio mask enhancement. Each recording uses utterance-level normalization.</summary>
    public sealed partial class MNNFullSubNet : IDisposable
    {
        private readonly object _gate = new object();
        private readonly MNNGenerationGraph _graph;
        private readonly MNNGenerationGraph _fullbandGraph, _subbandGraph;
        private readonly bool _plus;
        private bool _disposed;
        public string ModelName => _plus ? "FullSubNet+" : "FullSubNet";
        public MNNBackendType Backend => _graph?.Backend ?? _fullbandGraph.Backend;
        private MNNFullSubNet(string directory, int threads, MNNBackendType backend)
        {
            if (!MNNFullSubNetModel.IsComplete(directory)) throw new InvalidDataException("FullSubNet profile or graph verification failed.");
            var config = MNNFullSubNetConfig.Read(directory);
            _plus = MNNFullSubNetPlusModel.Matches(config);
            var selected = MNNAcceleration.Resolve(backend == MNNBackendType.Auto
                ? (!_plus && MNNAcceleration.IsBackendAvailable(MNNBackendType.Metal) ? MNNBackendType.Metal : MNNBackendType.CPU) : backend);
            if (selected == MNNBackendType.CPU)
            {
                _graph = new MNNGenerationGraph(Path.Combine(directory, config.graph), threads,
                    backendType: selected, precisionMode: MNNPrecisionMode.High);
                return;
            }
            try
            {
                _fullbandGraph = new MNNGenerationGraph(Path.Combine(directory, config.fullband_graph), threads,
                    backendType: selected, precisionMode: MNNPrecisionMode.High,
                    externalWeightsPath: _plus ? null : Path.Combine(directory, config.fullband_graph + ".weight"),
                    gpuTuningMode: _plus ? MNNGpuTuningMode.None : MNNGpuTuningMode.Wide,
                    recreateGpuSessionOnShapeChange: _plus);
                _subbandGraph = new MNNGenerationGraph(Path.Combine(directory, config.subband_graph), threads,
                    backendType: selected, precisionMode: MNNPrecisionMode.High,
                    externalWeightsPath: Path.Combine(directory, config.subband_graph + ".weight"),
                    gpuTuningMode: _plus ? MNNGpuTuningMode.None : MNNGpuTuningMode.Wide);
                if (_fullbandGraph.Backend != _subbandGraph.Backend) throw new NotSupportedException("FullSubNet stages resolved to different backends.");
            }
            catch
            {
                _subbandGraph?.Dispose();
                _fullbandGraph?.Dispose();
                throw;
            }
        }
        public static MNNFullSubNet Load(string directory, int threadCount = 1, MNNBackendType backendType = MNNBackendType.Auto)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentNullException(nameof(directory));
            if (threadCount < 1 || threadCount > 64) throw new ArgumentOutOfRangeException(nameof(threadCount));
            MNNAcceleration.Validate(backendType);
            if (backendType == MNNBackendType.NN) throw new NotSupportedException("FullSubNet NNAPI graph/device compatibility has not been verified.");
            MNNPlatformSupport.RequireSupported();
            return new MNNFullSubNet(Path.GetFullPath(directory), threadCount, backendType);
        }
        /// <summary>Frequency-major magnitude [257,frames] to complex mask [2,257,frames].</summary>
        public float[] PredictMask(float[] magnitude, int frames, CancellationToken cancellationToken = default)
        {
            if (frames < 2 || frames > 626 || magnitude == null || magnitude.Length != checked(frames * 257))
                throw new ArgumentException("Supply 2–626 frames of 257 frequency-major magnitudes.", nameof(magnitude));
            for (int i = 0; i < magnitude.Length; ++i)
                if (!(magnitude[i] >= 0 && magnitude[i] <= float.MaxValue)) throw new ArgumentException("Magnitude must be finite and nonnegative.", nameof(magnitude));
            lock (_gate)
            {
                ThrowIfDisposed();
                cancellationToken.ThrowIfCancellationRequested();
                var result = PredictMaskCore(magnitude, frames, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                return result;
            }
        }
        private float[] PredictMaskCore(float[] magnitude, int frames, CancellationToken token, float[] spectrum = null)
        {
            if (_plus) return PredictMaskPlus(magnitude, spectrum, frames, token);
            if (_graph == null) return PredictMaskGpu(magnitude, frames, token);
            var result = _graph.RunOutputs(new[]{"crm_mask"}, new MNNGenerationGraph.Input("magnitude", magnitude, 1, 1, 257, frames))[0];
            if (result.Values.Length != 2 * 257 * frames) throw new InvalidDataException("Unexpected FullSubNet mask shape.");
            return result.Values;
        }
        private void ThrowIfDisposed() { if (_disposed) throw new ObjectDisposedException(nameof(MNNFullSubNet)); }
        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                _graph?.Dispose();
                _fullbandGraph?.Dispose();
                _subbandGraph?.Dispose();
            }
        }
    }
}
