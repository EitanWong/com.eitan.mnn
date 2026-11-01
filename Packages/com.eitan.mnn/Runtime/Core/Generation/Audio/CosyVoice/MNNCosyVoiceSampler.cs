// Adapted from Nian27/CosyVoice3-MNN (Copyright 2026 Nian27), Apache-2.0.
// Modified for managed RAS sampling and validation. See LICENSE.md.
using System;
using System.Collections.Generic;
using System.IO;

namespace MNN.Unity
{
    // Upstream RAS: top-k=25, top-p=.8, temperature=1; on repetition
    // resample the full speech range with the repeated token excluded.
    internal static class MNNCosyVoiceSampler
    {
        internal const int Offset = 151924, Count = 6561, Eos = 158486, Vocabulary = 158720;
        internal const int CompactSize = Eos - Offset + 1;

        internal sealed class Workspace
        {
            internal readonly int[] Top = new int[25];
        }

        internal static int Sample(float[] logits, IList<int> history, int minimum, Random random, Workspace workspace = null)
        {
            if (logits == null || (logits.Length != CompactSize && (logits.Length == 0 || logits.Length % Vocabulary != 0)))
                throw new InvalidDataException("Unexpected CosyVoice logits shape.");
            int start = logits.Length == CompactSize ? -Offset : logits.Length - Vocabulary;
            int allowed = Count + (history.Count >= minimum ? 1 : 0);
            var top = (workspace ?? new Workspace()).Top;
            int size = 0;
            // Keep only the best 25, preserving ascending ID order for ties,
            // exactly as the stable full sort did. Reuse storage across tokens.
            for (int i = 0; i < allowed; ++i)
            {
                int id = Id(i);
                float score = logits[start + id];
                if (float.IsNaN(score) || float.IsInfinity(score))
                    throw new InvalidDataException("Non-finite CosyVoice logits.");
                int position = size;
                while (position > 0 && score > logits[start + top[position - 1]])
                    --position;
                if (position >= top.Length)
                    continue;
                if (size < top.Length)
                    ++size;
                for (int j = size - 1; j > position; --j)
                    top[j] = top[j - 1];
                top[position] = id;
            }

            double max = logits[start + top[0]], sum = 0;
            for (int i = 0; i < size; ++i)
                sum += Math.Exp(logits[start + top[i]] - max);
            double cumulative = 0;
            int keep = 0;
            do
            {
                cumulative += Math.Exp(logits[start + top[keep++]] - max) / sum;
            }
            while (keep < size && cumulative < .8);
            double total = 0;
            for (int i = 0; i < keep; ++i)
                total += Math.Exp(logits[start + top[i]] - max);
            double threshold = random.NextDouble() * total;
            int selected = top[keep - 1];
            for (int i = 0; i < keep; ++i)
            {
                threshold -= Math.Exp(logits[start + top[i]] - max);
                if (threshold <= 0)
                {
                    selected = top[i];
                    break;
                }
            }

            for (int i = Math.Max(0, history.Count - 10); i < history.Count; ++i)
                if (history[i] == selected)
                    return Resample(logits, start, allowed, selected, random);
            return selected;
        }

        // Offset+Count is a reserved gap, not a speech token or EOS.
        private static int Id(int index) => index == Count ? Eos : Offset + index;

        private static int Resample(float[] logits, int start, int count, int excluded, Random random)
        {
            double max = double.NegativeInfinity;
            for (int i = 0; i < count; ++i)
                if (Id(i) != excluded)
                    max = Math.Max(max, logits[start + Id(i)]);
            double total = 0;
            for (int i = 0; i < count; ++i)
                if (Id(i) != excluded)
                    total += Math.Exp(logits[start + Id(i)] - max);
            double threshold = random.NextDouble() * total;
            int last = -1;
            for (int i = 0; i < count; ++i)
            {
                int id = Id(i);
                if (id == excluded)
                    continue;
                last = id;
                threshold -= Math.Exp(logits[start + id] - max);
                if (threshold <= 0)
                    return id;
            }

            return last;
        }
    }
}
