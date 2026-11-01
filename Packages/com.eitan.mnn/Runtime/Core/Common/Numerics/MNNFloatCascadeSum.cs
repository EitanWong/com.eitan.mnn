using System;

namespace MNN.Unity
{
    // PyTorch ARM float32 contiguous-row reduction (four float lanes).
    // The operation order matters for NeMo's near-constant feature variance.
    // Derived from PyTorch SumKernel.cpp; notice: Documentation~/PyTorchNumericsLicense.md.
    internal static class MNNFloatCascadeSum
    {
        internal static float Sum(float[] values, int offset, int count)
        {
            if (values == null || offset < 0 || count < 4 || offset > values.Length - count)
                throw new ArgumentException("Supply a contiguous float row of at least four values.");
            int vectors = count / 4, groups = vectors / 4;
            int logarithm = 0;
            for (int n = Math.Max(1, groups) - 1; n > 0; n >>= 1) ++logarithm;
            int power = Math.Max(4, logarithm / 4), step = 1 << power, mask = step - 1;
            var accumulators = new float[64];
            int group = 0;
            while (group + step <= groups)
            {
                for (int j = 0; j < step; ++j, ++group) AddGroup(values, offset + group * 16, accumulators);
                Carry(accumulators, group, power, mask);
            }
            for (; group < groups; ++group) AddGroup(values, offset + group * 16, accumulators);
            for (int level = 1; level < 4; ++level)
            for (int i = 0; i < 16; ++i) accumulators[i] += accumulators[level * 16 + i];
            for (int vector = groups * 4; vector < vectors; ++vector)
            for (int lane = 0; lane < 4; ++lane) accumulators[lane] += values[offset + vector * 4 + lane];
            for (int row = 1; row < 4; ++row)
            for (int lane = 0; lane < 4; ++lane) accumulators[lane] += accumulators[row * 4 + lane];
            float result = 0;
            for (int i = vectors * 4; i < count; ++i) result += values[offset + i];
            for (int lane = 0; lane < 4; ++lane) result += accumulators[lane];
            return result;
        }

        private static void AddGroup(float[] values, int offset, float[] accumulators)
        {
            for (int i = 0; i < 16; ++i) accumulators[i] += values[offset + i];
        }

        private static void Carry(float[] accumulators, int group, int power, int mask)
        {
            for (int level = 1; level < 4; ++level)
            {
                for (int i = 0; i < 16; ++i)
                {
                    accumulators[level * 16 + i] += accumulators[(level - 1) * 16 + i];
                    accumulators[(level - 1) * 16 + i] = 0;
                }
                if ((group & (mask << (level * power))) != 0) break;
            }
        }
    }
}
