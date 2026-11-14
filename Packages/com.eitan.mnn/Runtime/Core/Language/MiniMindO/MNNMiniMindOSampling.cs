using System;
using System.Collections.Generic;

namespace MNN.Unity
{
    internal static class MNNMiniMindOSampling
    {
        internal static int Sample(float[] logits, int offset, int count, float temperature, float topP,
            int topK, Random random, IEnumerable<int> recent = null)
        {
            var scores = new double[count];
            for (int i = 0; i < count; ++i) scores[i] = logits[offset + i] / (temperature > 0 ? temperature : 1);
            if (recent != null)
                foreach (int id in recent) scores[id] = scores[id] > 0 ? scores[id] / 1.05 : scores[id] * 1.05;
            if (temperature == 0)
            {
                int best = 0;
                for (int i = 1; i < count; ++i)
                    if (Compare(scores, i, best) < 0) best = i;
                return best;
            }
            int keep = topK > 0 ? Math.Min(topK, count) : count;
            var order = SelectTop(scores, keep);
            double total = 0, maximum = scores[order[0]];
            for (int i = 0; i < keep; ++i) total += scores[order[i]] = Math.Exp(scores[order[i]] - maximum);
            // Preserve the first token beyond the nucleus boundary, as in the released sampler.
            if (topP < 1)
            {
                double cumulative = 0;
                for (int i = 0; i < keep; ++i)
                {
                    cumulative += scores[order[i]];
                    if (cumulative / total > topP) { keep = i + 1; total = cumulative; break; }
                }
            }
            double draw = random.NextDouble() * total;
            for (int i = 0; i < keep; ++i)
                if ((draw -= scores[order[i]]) <= 0) return order[i];
            return order[keep - 1];
        }

        // Keep the worst retained token at the root. Audio needs only 50 of 2112
        // tokens; sorting the entire vocabulary eight times per step is unnecessary.
        private static int[] SelectTop(double[] scores, int keep)
        {
            var order = new int[keep];
            for (int i = 0; i < keep; ++i) order[i] = i;
            if (keep < scores.Length)
            {
                for (int i = keep / 2 - 1; i >= 0; --i) SiftWorst(scores, order, i);
                for (int i = keep; i < scores.Length; ++i)
                {
                    if (Compare(scores, i, order[0]) >= 0) continue;
                    order[0] = i;
                    SiftWorst(scores, order, 0);
                }
            }
            Array.Sort(order, (left, right) => Compare(scores, left, right));
            return order;
        }

        private static void SiftWorst(double[] scores, int[] order, int root)
        {
            while (root * 2 + 1 < order.Length)
            {
                int child = root * 2 + 1;
                if (child + 1 < order.Length && Compare(scores, order[child + 1], order[child]) > 0) ++child;
                if (Compare(scores, order[root], order[child]) >= 0) return;
                int swap = order[root]; order[root] = order[child]; order[child] = swap;
                root = child;
            }
        }

        // Match the released descending-score, ascending-token tie order exactly.
        private static int Compare(double[] scores, int left, int right)
        {
            int rank = scores[right].CompareTo(scores[left]);
            return rank != 0 ? rank : left.CompareTo(right);
        }

    }
}
