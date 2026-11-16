using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MNN.Unity
{
    public sealed class MNNBairongTurnSenseResult
    {
        private static readonly string[] Labels = { "complete", "incomplete", "invalid" };
        public IReadOnlyList<float> Probabilities { get; }
        public int ClassIndex { get; }
        public string Label => Labels[ClassIndex];
        public bool IsComplete => ClassIndex == 0;
        internal MNNBairongTurnSenseResult(float[] logits)
        {
            if (logits.Length != 3 || logits.Any(v => float.IsNaN(v) || float.IsInfinity(v)))
                throw new InvalidDataException("Expected three finite TurnSense scores.");
            var values = (float[])logits.Clone();
            if (values.Any(v => v < 0 || v > 1) || Math.Abs(values.Sum() - 1) >= .0001)
            {
                float maximum = values.Max(); double sum = 0;
                for (int i = 0; i < values.Length; ++i) { values[i] = (float)Math.Exp(values[i] - maximum); sum += values[i]; }
                for (int i = 0; i < values.Length; ++i) values[i] = (float)(values[i] / sum);
            }
            ClassIndex = Array.IndexOf(values, values.Max()); Probabilities = Array.AsReadOnly(values);
        }
    }
}
