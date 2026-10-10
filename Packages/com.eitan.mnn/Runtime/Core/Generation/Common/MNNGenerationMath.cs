using System;

namespace MNN.Unity
{
    internal static class MNNGenerationMath
    {
        internal static float[] Gaussian(int count, int seed)
        {
            var rng = new Random(seed);
            var result = new float[count];
            for (int i = 0; i < count; i += 2)
            {
                double r = Math.Sqrt(-2 * Math.Log(1 - rng.NextDouble())), theta = 2 * Math.PI * rng.NextDouble();
                result[i] = (float)(r * Math.Cos(theta));
                if (i + 1 < count)
                    result[i + 1] = (float)(r * Math.Sin(theta));
            }

            return result;
        }
    }
}
