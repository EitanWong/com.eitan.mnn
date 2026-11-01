using System;
using System.Collections.Generic;

namespace MNN.Unity
{
    /// <summary>Owned probabilities in the original option order. Values depend
    /// on checkpoint temperature and are not calibrated to an application's data.</summary>
    public sealed class MNNDecisionResult
    {
        public MNNDecisionKind Kind { get; }
        public IReadOnlyList<string> Labels { get; }
        public IReadOnlyList<double> Probabilities { get; }
        public int BestIndex { get; }
        public string Choice => Kind == MNNDecisionKind.Choice ? Labels[BestIndex] : null;
        public double? ExpectedScore { get; }
        public double? TrueProbability => Kind == MNNDecisionKind.Boolean ? Probabilities[1] : (double?)null;
        public double Confidence { get; }
        public double ActProbability { get; }
        public double Temperature { get; }
        public int InputTokens { get; }
        public bool StateTruncated { get; }
        public bool QuestionTruncated { get; }
        internal MNNDecisionResult(MNNDecisionQuestion question, double[] probabilities, double confidence, double actProbability, double temperature,
            int inputTokens, bool stateTruncated, bool questionTruncated)
        {
            Kind = question.Kind;
            var labels = new string[question.Options.Count];
            double expected = 0;
            for (int i = 0; i < probabilities.Length; ++i)
            {
                labels[i] = question.Options[i].Label;
                if (probabilities[i] > probabilities[BestIndex]) BestIndex = i;
                expected += i * probabilities[i];
            }
            Labels = Array.AsReadOnly(labels); Probabilities = Array.AsReadOnly((double[])probabilities.Clone());
            ExpectedScore = Kind == MNNDecisionKind.Score ? expected : (double?)null;
            Confidence = confidence; ActProbability = actProbability; Temperature = temperature;
            InputTokens = inputTokens; StateTruncated = stateTruncated; QuestionTruncated = questionTruncated;
        }
    }
}
