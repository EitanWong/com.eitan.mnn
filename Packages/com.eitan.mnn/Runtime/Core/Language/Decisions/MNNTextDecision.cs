using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    /// <summary>Pinned English Laya structured decisions. One question per forward
    /// pass; probabilities are checkpoint outputs, not application calibration.</summary>
    public sealed partial class MNNTextDecision : IDisposable
    {
        private readonly object _gate = new object();
        private readonly MNNBytePairTokenizer _tokenizer;
        private readonly MNNGenerationGraph _graph;
        private bool _disposed;
        public MNNBackendType Backend => _graph.Backend;

        private MNNTextDecision(string directory, int threads)
        {
            _tokenizer = MNNBytePairTokenizer.ReadModernBert(Path.Combine(directory, "tokenizer.json"));
            _graph = new MNNGenerationGraph(Path.Combine(directory, MNNTextDecisionModel.Files[0]), threads,
                backendType: MNNBackendType.CPU, precisionMode: MNNPrecisionMode.High);
        }

        public static MNNTextDecision Load(string directory, int threadCount = 4, MNNBackendType backendType = MNNBackendType.Auto)
        {
            MNNPlatformSupport.RequireSupported();
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentNullException(nameof(directory));
            if (threadCount < 1 || threadCount > 64) throw new ArgumentOutOfRangeException(nameof(threadCount));
            // Publisher pins CPU/High; GPU execution is not validated for this graph.
            if (backendType != MNNBackendType.Auto && backendType != MNNBackendType.CPU)
                throw new NotSupportedException("English Laya supports CPU/High only; Auto selects CPU.");
            MNNTextDecisionModel.Verify(directory);
            return new MNNTextDecision(directory, threadCount);
        }

        public MNNDecisionResult Decide(string state, MNNDecisionQuestion question, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(MNNTextDecision));
                cancellationToken.ThrowIfCancellationRequested();
                var input = BuildInput(_tokenizer, state, question, cancellationToken);
                var outputs = _graph.RunOutputs(new[] {"logits", "act_logits"}, input.GraphInputs(question.Kind));
                if (!outputs[0].Shape.SequenceEqual(new[] {1, 64}) || !outputs[1].Shape.SequenceEqual(new[] {1, 2}))
                    throw new InvalidDataException("Unexpected English Laya output shapes.");
                cancellationToken.ThrowIfCancellationRequested();
                return Decode(question, input, outputs[0].Values, outputs[1].Values);
            }
        }

        public Task<MNNDecisionResult> DecideAsync(string state, MNNDecisionQuestion question, CancellationToken cancellationToken = default) =>
            Task.Run(() => Decide(state, question, cancellationToken), cancellationToken);

        internal static MNNDecisionResult Decode(MNNDecisionQuestion question, Input input, float[] logits, float[] action)
        {
            int count = question.Options.Count;
            if (logits == null || logits.Length != 64 || action == null || action.Length != 2)
                throw new InvalidDataException("Unexpected English Laya output lengths.");
            double temperature = question.Kind == MNNDecisionKind.Boolean ? 1.983399510383606 :
                question.Kind == MNNDecisionKind.Score ? 1.2514300346374512 : count == 2 ? 1.9063563346862793 :
                count <= 5 ? 1.7601518630981445 : count <= 10 ? 1.0000158548355103 : .5;
            double[] probabilities = Softmax(logits.Take(count).ToArray(), temperature);
            double entropy = 0;
            foreach (double p in probabilities) if (p > 0) entropy -= p * Math.Log(p);
            double confidence = question.Kind == MNNDecisionKind.Boolean ? probabilities.Max() : Math.Max(0, Math.Min(1, 1 - entropy / Math.Log(count)));
            return new MNNDecisionResult(question, probabilities, confidence, Softmax(action, 1)[0], temperature,
                input.TokenCount, input.StateTruncated, input.QuestionTruncated);
        }

        private static double[] Softmax(float[] values, double temperature)
        {
            if (values.Any(v => float.IsNaN(v) || float.IsInfinity(v))) throw new InvalidDataException("Nonfinite English Laya output.");
            double maximum = values.Max();
            double[] result = values.Select(v => Math.Exp(((double)v - maximum) / temperature)).ToArray();
            double sum = result.Sum();
            for (int i = 0; i < result.Length; ++i) result[i] /= sum;
            return result;
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true; _graph.Dispose();
            }
        }
    }
}
