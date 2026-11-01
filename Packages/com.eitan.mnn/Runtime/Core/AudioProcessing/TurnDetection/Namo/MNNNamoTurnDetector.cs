using System;
using System.IO;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>Multilingual transcript end-of-turn probability from the author-published NAMO v1 classifier.</summary>
    public sealed class MNNNamoTurnDetector : IDisposable
    {
        public const int MaximumTokens = 8192;
        public const int MetalMaximumTokens = 512;
        private readonly object _gate = new object();
        private readonly MNNNamoTokenizer _tokenizer;
        private readonly string _graphPath;
        private readonly int _threads;
        private readonly MNNBackendType _requestedBackend;
        private MNNGenerationGraph _graph;
        private int _metalShape;
        private bool _disposed;
        /// <summary>The backend used by the latest call; long Metal requests run on CPU.</summary>
        public MNNBackendType Backend => _graph?.Backend ?? _requestedBackend;

        private MNNNamoTurnDetector(string directory, int threads, MNNBackendType backend)
        {
            if (!MNNNamoTurnDetectorModel.IsComplete(directory)) throw new InvalidDataException("NAMO model package failed verification.");
            _tokenizer = new MNNNamoTokenizer(Path.Combine(directory, MNNNamoTurnDetectorModel.Tokenizer));
            _graphPath = Path.Combine(directory, MNNNamoTurnDetectorModel.Graph);
            _threads = threads;
            _requestedBackend = backend;
        }

        public static MNNNamoTurnDetector Load(string directory, int threadCount = 4, MNNBackendType backendType = MNNBackendType.Auto)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentNullException(nameof(directory));
            if (threadCount < 1 || threadCount > 64) throw new ArgumentOutOfRangeException(nameof(threadCount));
            if (backendType != MNNBackendType.CPU && backendType != MNNBackendType.Metal && backendType != MNNBackendType.Auto)
                throw new NotSupportedException("NAMO currently supports the validated CPU and Metal paths.");
            MNNPlatformSupport.RequireSupported();
            return new MNNNamoTurnDetector(Path.GetFullPath(directory), threadCount,
                backendType == MNNBackendType.Auto ? MNNBackendType.CPU : backendType);
        }

        /// <summary>Returns class-1 softmax probability; truncates content on the right while preserving BOS and EOS.</summary>
        public float Predict(string transcript, CancellationToken cancellationToken = default)
        {
            if (transcript == null) throw new ArgumentNullException(nameof(transcript));
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(MNNNamoTurnDetector));
                cancellationToken.ThrowIfCancellationRequested();
                int[] tokens = _tokenizer.Encode(transcript, MaximumTokens);
                var mask = new int[tokens.Length];
                for (int i = 0; i < mask.Length; ++i) mask[i] = 1;
                cancellationToken.ThrowIfCancellationRequested();
                MNNBackendType backend = _requestedBackend == MNNBackendType.Metal && tokens.Length <= MetalMaximumTokens
                    ? MNNBackendType.Metal : MNNBackendType.CPU;
                int metalShape = tokens.Length <= 128 ? 128 : MetalMaximumTokens;
                SelectGraph(backend, metalShape);
                if (backend == MNNBackendType.Metal)
                {
                    Array.Resize(ref tokens, metalShape);
                    Array.Resize(ref mask, metalShape);
                }
                float[] logits = _graph.Run("logits", new MNNGenerationGraph.Input("input_ids", tokens, 1, tokens.Length),
                    new MNNGenerationGraph.Input("attention_mask", mask, 1, tokens.Length));
                if (logits.Length != 2) throw new InvalidDataException("NAMO must return two logits.");
                cancellationToken.ThrowIfCancellationRequested();
                return (float)(1 / (1 + Math.Exp((double)logits[0] - logits[1])));
            }
        }

        private void SelectGraph(MNNBackendType backend, int metalShape)
        {
            if (_graph != null && _graph.Backend == backend && (backend != MNNBackendType.Metal || _metalShape == metalShape)) return;
            // Release the complete graph owner when crossing the GPU context bound.
            // A fixed GPU shape avoids unsafe resize replay and per-shape sessions.
            _graph?.Dispose();
            _graph = null;
            _graph = new MNNGenerationGraph(_graphPath, _threads, backendType: backend,
                precisionMode: MNNPrecisionMode.High, gpuTuningMode: MNNGpuTuningMode.None);
            _metalShape = metalShape;
        }

        public bool IsEndOfTurn(string transcript, float threshold = .5f, CancellationToken cancellationToken = default)
        {
            if (float.IsNaN(threshold) || threshold < 0 || threshold > 1) throw new ArgumentOutOfRangeException(nameof(threshold));
            return Predict(transcript, cancellationToken) > threshold;
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                _graph?.Dispose();
            }
        }
    }
}
