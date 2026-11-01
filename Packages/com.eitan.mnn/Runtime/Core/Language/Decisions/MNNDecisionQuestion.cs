using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace MNN.Unity
{
    public enum MNNDecisionKind { Choice, Score, Boolean }

    /// <summary>One structured question, evaluated in one forward pass. Option
    /// order defines model indices; score levels start at zero.</summary>
    public sealed class MNNDecisionQuestion
    {
        public MNNDecisionKind Kind { get; }
        public string Instructions { get; }
        public IReadOnlyList<MNNDecisionOption> Options { get; }
        private MNNDecisionQuestion(MNNDecisionKind kind, string instructions, MNNDecisionOption[] options)
        {
            if (string.IsNullOrWhiteSpace(instructions) || instructions.Length > 8000) throw new ArgumentException("Provide question instructions of at most 8,000 characters.", nameof(instructions));
            if (options == null || options.Length < 2 || options.Length > 64 || options.Any(o => o == null)) throw new ArgumentException("A question requires 2–64 nonnull options.", nameof(options));
            if (options.Select(o => o.Label).Distinct(StringComparer.Ordinal).Count() != options.Length) throw new ArgumentException("Option labels must be unique.", nameof(options));
            Kind = kind; Instructions = instructions; Options = Array.AsReadOnly((MNNDecisionOption[])options.Clone());
        }
        public static MNNDecisionQuestion Choice(string instructions, params MNNDecisionOption[] options) => new MNNDecisionQuestion(MNNDecisionKind.Choice, instructions, options);
        public static MNNDecisionQuestion Score(string instructions, params string[] levels)
        {
            if (levels == null || levels.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("Provide a description for each score level.", nameof(levels));
            return new MNNDecisionQuestion(MNNDecisionKind.Score, instructions, levels.Select((s, i) => new MNNDecisionOption(i.ToString(CultureInfo.InvariantCulture), s)).ToArray());
        }
        public static MNNDecisionQuestion Boolean(string instructions, string falseCriterion = null, string trueCriterion = null) =>
            new MNNDecisionQuestion(MNNDecisionKind.Boolean, instructions, new[] {new MNNDecisionOption("false", falseCriterion), new MNNDecisionOption("true", trueCriterion)});
    }
}
