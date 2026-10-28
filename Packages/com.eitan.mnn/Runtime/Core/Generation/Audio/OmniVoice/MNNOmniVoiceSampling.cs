using System;

namespace MNN.Unity
{
    internal static class MNNOmniVoiceSampling
    {
        internal const int Codebooks = 8;
        internal const int Vocabulary = 1025;
        internal const int Mask = 1024;

        internal static int[] Schedule(int total, int steps)
        {
            if (total < 1 || steps < 1 || steps > 128) throw new ArgumentOutOfRangeException(nameof(steps));
            var schedule = new int[steps];
            int remaining = total;
            for (int step = 0; step < steps; ++step)
            {
                double start = Shift((double)step / steps);
                double end = Shift((double)(step + 1) / steps);
                int count = step == steps - 1 ? remaining : Math.Min((int)Math.Ceiling(total * (end - start)), remaining);
                schedule[step] = count;
                remaining -= count;
            }
            return schedule;
        }

        private static double Shift(double time) => 0.1 * time / (1 - 0.9 * time);

        // The two inner log-softmax constants cancel in CFG's outer log-softmax.
        // Upstream normalizes all classes, then excludes mask from selection.
        internal static int Predict(float[] conditional, float[] unconditional, float guidance, out float confidence)
            => Predict(conditional, 0, unconditional, 0, guidance, out confidence);

        internal static int Predict(float[] conditional, int cOffset, float[] unconditional, int uOffset,
            float guidance, out float confidence)
        {
            if (conditional == null || unconditional == null || cOffset < 0 || uOffset < 0 ||
                conditional.Length - cOffset < Vocabulary || unconditional.Length - uOffset < Vocabulary)
                throw new ArgumentException("OmniVoice logits must contain 1025 classes.");
            int best = 0;
            double bestValue = double.NegativeInfinity;
            for (int i = 0; i < Mask; ++i)
            {
                double value = (1.0 + guidance) * conditional[cOffset + i] - guidance * unconditional[uOffset + i];
                if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentException("Non-finite OmniVoice logits.");
                if (value > bestValue) { bestValue = value; best = i; }
            }
            double maskValue = (1.0 + guidance) * conditional[cOffset + Mask] - guidance * unconditional[uOffset + Mask];
            if (double.IsNaN(maskValue) || double.IsInfinity(maskValue)) throw new ArgumentException("Non-finite OmniVoice logits.");
            double maximum = Math.Max(bestValue, maskValue);
            double sum = 0;
            for (int i = 0; i < Vocabulary; ++i)
                sum += Math.Exp((1.0 + guidance) * conditional[cOffset + i] - guidance * unconditional[uOffset + i] - maximum);
            confidence = (float)(bestValue - maximum - Math.Log(sum));
            return best;
        }

        internal static double Gumbel(Random random) => -Math.Log(-Math.Log(Math.Max(1e-10, random.NextDouble())));
    }
}
