using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace MNN.Unity
{
    public sealed partial class MNNTextDecision
    {
        internal sealed class Input
        {
            internal int[] Ids, Attention, MarkerPositions, MarkerMask;
            internal int TokenCount;
            internal bool StateTruncated, QuestionTruncated;
            internal MNNGenerationGraph.Input[] GraphInputs(MNNDecisionKind kind) => new[]
            {
                new MNNGenerationGraph.Input("input_ids", Ids, 1, 512),
                new MNNGenerationGraph.Input("attention_mask", Attention, 1, 512),
                new MNNGenerationGraph.Input("marker_pos", MarkerPositions, 1, 64),
                new MNNGenerationGraph.Input("marker_mask", MarkerMask, 1, 64),
                new MNNGenerationGraph.Input("qtype", new[] {(int)kind}, 1)
            };
        }

        internal static string TypeName(MNNDecisionKind kind) => kind == MNNDecisionKind.Choice ? "choice" : kind == MNNDecisionKind.Score ? "score" : "noul";
        internal static Input BuildInput(MNNBytePairTokenizer tokenizer, string state, MNNDecisionQuestion question, CancellationToken token = default)
        {
            if (question == null) throw new ArgumentNullException(nameof(question));
            ValidateText(state, 16000); ValidateText(question.Instructions, 8000);
            token.ThrowIfCancellationRequested();
            int[] head = tokenizer.Encode(TypeName(question.Kind) + " question: " + question.Instructions.Replace("[MASK]", " "));
            var options = new List<int[]>(); bool truncated = false;
            for (int i = 0; i < question.Options.Count; ++i)
            {
                token.ThrowIfCancellationRequested();
                var option = question.Options[i];
                ValidateText(option.Label, 2048);
                if (!string.IsNullOrEmpty(option.Description)) ValidateText(option.Description, 4096);
                string description = option.Description;
                if (question.Kind == MNNDecisionKind.Boolean && string.IsNullOrEmpty(description))
                    description = i == 0 ? "no, the statement does not hold" : "yes, the statement holds";
                string text = question.Kind == MNNDecisionKind.Score ? "level " + option.Label + ": " + description :
                    string.IsNullOrEmpty(description) ? option.Label : option.Label + ": " + description;
                int[] content = tokenizer.Encode(" " + text.Replace("[MASK]", " "));
                truncated |= content.Length > 48;
                options.Add(new[] {50284}.Concat(content.Take(48)).ToArray());
            }
            int budget = 192 - options.Sum(o => o.Length);
            if (budget < 16)
            {
                int per = Math.Max(4, (192 - 16) / options.Count);
                truncated |= options.Any(o => o.Length > per);
                options = options.Select(o => o.Take(per).ToArray()).ToList();
                budget = 192 - options.Sum(o => o.Length);
            }
            truncated |= head.Length > Math.Max(8, budget);
            var ids = new List<int> {50281}; ids.AddRange(head.Take(Math.Max(8, budget))); ids.Add(50282);
            var positions = new int[64]; var masks = new int[64];
            for (int i = 0; i < options.Count; ++i) { positions[i] = ids.Count; masks[i] = 1; ids.AddRange(options[i]); }
            ids.Add(50282);
            int room = Math.Max(0, 512 - ids.Count - 1);
            int[] stateIds = tokenizer.Encode(state.Replace("[MASK]", " "));
            ids.AddRange(stateIds.Take(room)); ids.Add(50282);
            if (ids.Count > 512 || positions.Take(options.Count).Any(p => p >= 512)) throw new InvalidDataException("Question markers do not fit the fixed Laya sequence.");
            var attention = new int[512]; for (int i = 0; i < ids.Count; ++i) attention[i] = 1;
            return new Input {Ids = ids.Concat(Enumerable.Repeat(50283, 512 - ids.Count)).ToArray(), Attention = attention,
                MarkerPositions = positions, MarkerMask = masks, TokenCount = ids.Count, StateTruncated = stateIds.Length > room, QuestionTruncated = truncated};
        }

        internal static void ValidateText(string text, int maximum)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length > maximum) throw new ArgumentException("Provide nonempty text of at most " + maximum + " characters.", nameof(text));
            for (int i = 0; i < text.Length; ++i)
            {
                if (text[i] == 0) throw new ArgumentException("Text cannot contain NUL.", nameof(text));
                if (!char.IsSurrogate(text[i])) continue;
                if (!char.IsSurrogatePair(text, i)) throw new ArgumentException("Text contains an unpaired UTF-16 surrogate.", nameof(text));
                ++i;
            }
        }
    }
}
