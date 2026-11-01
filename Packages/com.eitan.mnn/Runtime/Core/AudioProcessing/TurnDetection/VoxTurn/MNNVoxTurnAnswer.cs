using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace MNN.Unity
{
    public sealed class MNNVoxTurnAnswer
    {
        public string Task
        {
            get;
        }
        public IReadOnlyList<string> Labels
        {
            get;
        }
        public IReadOnlyList<float> Logits
        {
            get;
        }
        public IReadOnlyList<float> Probabilities
        {
            get;
        }
        internal MNNVoxTurnAnswer(string task, string[] labels, float[] logits)
        {
            Task = task;
            Labels = Array.AsReadOnly((string[])labels.Clone());
            Logits = Array.AsReadOnly((float[])logits.Clone());
            double maximum = logits.Max(), sum = 0;
            var probabilities = new float[logits.Length];
            for (int i = 0; i < logits.Length; ++i)
                sum += Math.Exp(logits[i] - maximum);
            for (int i = 0; i < logits.Length; ++i)
                probabilities[i] = (float)(Math.Exp(logits[i] - maximum) / sum);
            Probabilities = Array.AsReadOnly(probabilities);
        }
        public float Probability(string label)
        {
            for (int i = 0; i < Labels.Count; ++i)
                if (Labels[i] == label)
                    return Probabilities[i];
            throw new ArgumentException("Unknown option for " + Task + ".", nameof(label));
        }
    }
}
