using System;

namespace MNN.Unity
{
    public sealed class MNNDecisionOption
    {
        public string Label { get; }
        public string Description { get; }
        public MNNDecisionOption(string label, string description = null)
        {
            if (string.IsNullOrWhiteSpace(label) || label.Length > 2048) throw new ArgumentException("An option needs a nonempty label of at most 2,048 characters.", nameof(label));
            if (description?.Length > 4096) throw new ArgumentException("Option descriptions support at most 4,096 characters.", nameof(description));
            Label = label; Description = description;
        }
    }
}
