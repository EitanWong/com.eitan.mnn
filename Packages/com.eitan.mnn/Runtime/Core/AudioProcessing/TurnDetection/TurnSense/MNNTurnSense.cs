using System;
using System.IO;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>SmolLM2-135M TurnSense transcript EOU with exact author prefix and padded256 inputs.</summary>
    public sealed class MNNTurnSense : IDisposable
    {
        public const int MaximumTokens = 256;
        private readonly object _gate = new object();
        private readonly MNNTurnSenseTokenizer _tokenizer;
        private readonly MNNGenerationGraph _graph;
        private bool _disposed;
        public MNNBackendType Backend => _graph.Backend;

        private MNNTurnSense(string directory, int threads, MNNBackendType backend)
        {
            if (!MNNTurnSenseModel.IsComplete(directory)) throw new InvalidDataException("TurnSense model package failed verification.");
            _tokenizer = new MNNTurnSenseTokenizer(Path.Combine(directory, MNNTurnSenseModel.Tokenizer));
            _graph = new MNNGenerationGraph(Path.Combine(directory, MNNTurnSenseModel.Graph), threads,
                backendType: backend, precisionMode: MNNPrecisionMode.High, gpuTuningMode: MNNGpuTuningMode.None);
        }

        public static MNNTurnSense Load(string directory, int threadCount = 4, MNNBackendType backendType = MNNBackendType.Auto)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentNullException(nameof(directory));
            if (threadCount < 1 || threadCount > 64) throw new ArgumentOutOfRangeException(nameof(threadCount));
            if (backendType != MNNBackendType.Auto && backendType != MNNBackendType.CPU && backendType != MNNBackendType.Metal)
                throw new NotSupportedException("TurnSense supports the qualified CPU and Metal paths.");
            MNNPlatformSupport.RequireSupported();
            var backend = backendType == MNNBackendType.Auto ? (MNNAcceleration.IsBackendAvailable(MNNBackendType.Metal) ? MNNBackendType.Metal : MNNBackendType.CPU) : backendType;
            return new MNNTurnSense(Path.GetFullPath(directory), threadCount, backend);
        }

        /// <summary>Preserves transcript whitespace and Unicode; rejects overlong input instead of truncating it.</summary>
        public MNNTurnSenseResult Predict(string transcript, CancellationToken cancellationToken = default)
        {
            if (transcript == null) throw new ArgumentNullException(nameof(transcript));
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(MNNTurnSense));
                cancellationToken.ThrowIfCancellationRequested();
                int[] encoded = _tokenizer.Encode(transcript);
                var ids = new int[MaximumTokens];
                var mask = new int[MaximumTokens];
                for (int i = 0; i < ids.Length; ++i)
                {
                    ids[i] = i < encoded.Length ? encoded[i] : 2;
                    mask[i] = i < encoded.Length ? 1 : 0;
                }
                cancellationToken.ThrowIfCancellationRequested();
                float[] probabilities = _graph.Run("probabilities", new MNNGenerationGraph.Input("input_ids", ids, 1, MaximumTokens),
                    new MNNGenerationGraph.Input("attention_mask", mask, 1, MaximumTokens));
                if (probabilities.Length != 2 || probabilities[0] < 0 || probabilities[0] > 1 || probabilities[1] < 0 || probabilities[1] > 1 || Math.Abs(probabilities[0] + probabilities[1] - 1) > .00001)
                    throw new InvalidDataException("TurnSense must return two normalized probabilities.");
                cancellationToken.ThrowIfCancellationRequested();
                return new MNNTurnSenseResult(probabilities[0], probabilities[1]);
            }
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
