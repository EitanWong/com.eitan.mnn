using System;
using System.Collections.Generic;
using System.Linq;

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
            var order = Enumerable.Range(0, count).OrderByDescending(i => scores[i]).ThenBy(i => i).ToArray();
            if (temperature == 0) return order[0];
            int keep = topK > 0 ? Math.Min(topK, count) : count;
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
    }
}
