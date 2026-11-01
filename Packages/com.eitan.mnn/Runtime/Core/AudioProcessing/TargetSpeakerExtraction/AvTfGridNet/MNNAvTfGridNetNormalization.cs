using System;
using System.Threading;

namespace MNN.Unity
{
    // Deterministic four-lane cascaded Welford arithmetic from the pinned
    // PyTorch CPU source oracle. This is managed numerical code, not an ABI layout.
    internal sealed class MNNAvTfGridNetNormalization
    {
        private const int Channels = 48, Lanes = 4, ChunkSize = 16;
        private readonly float[] _gamma, _beta;

        internal MNNAvTfGridNetNormalization(float[] gamma, float[] beta)
        {
            _gamma = CopyCoefficients(gamma, nameof(gamma));
            _beta = CopyCoefficients(beta, nameof(beta));
        }

        private static float[] CopyCoefficients(float[] values, string name)
        {
            if (values == null || values.Length != Channels) throw new ArgumentException("Supply 48 source coefficients.", name);
            foreach (float value in values)
                if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentException("Supply finite source coefficients.", name);
            return (float[])values.Clone();
        }

        internal void NormalizeInPlace(float[] values, CancellationToken token)
        {
            if (values == null || values.Length == 0 || values.Length % (Channels * MNNAvTfGridNetSpectral.Bins) != 0)
                throw new ArgumentException("Supply channel-major AV-TFGridNet convolution features.", nameof(values));
            token.ThrowIfCancellationRequested();
            Moments(values, token, out float mean, out float variance);
            float rstd = (float)(1.0 / Math.Sqrt(Math.Max(variance, 0f) + 1e-5));
            int spatial = values.Length / Channels;
            for (int channel = 0; channel < Channels; ++channel)
            {
                float scale = rstd * _gamma[channel];
                float bias = Fma(-scale, mean, _beta[channel]);
                Apply(values, channel * spatial, spatial, scale, bias, token);
            }
        }

        private static void Apply(float[] values, int offset, int count, float scale, float bias, CancellationToken token)
        {
            for (int i = 0; i < count; ++i)
            {
                if ((i & 16383) == 0) token.ThrowIfCancellationRequested();
                values[offset + i] = Fma(scale, values[offset + i], bias);
            }
        }

        private sealed class Cascade
        {
            internal readonly int[] Counts;
            internal readonly float[] Means, Variances;
            internal Cascade(int depth)
            {
                Counts = new int[depth]; Means = new float[depth * Lanes]; Variances = new float[depth * Lanes];
            }
        }

        private static void Moments(float[] values, CancellationToken token, out float mean, out float variance)
        {
            int vectors = values.Length / Lanes, chunks = (vectors + ChunkSize - 1) / ChunkSize;
            int depth = 1;
            while ((1 << depth) < chunks) ++depth;
            var stack = new Cascade(depth);
            var chunkMean = new float[Lanes]; var chunkVariance = new float[Lanes];
            for (int chunk = 0; chunk < chunks; ++chunk)
            {
                if ((chunk & 255) == 0) token.ThrowIfCancellationRequested();
                int count = Math.Min(ChunkSize, vectors - chunk * ChunkSize);
                UpdateChunk(values, chunk * ChunkSize * Lanes, count, chunkMean, chunkVariance);
                AddVector(stack, 0, count, chunkMean, chunkVariance, 0);
                Carry(stack, chunk + 1);
            }
            for (int level = 1; level < depth; ++level)
                AddVector(stack, 0, stack.Counts[level], stack.Means, stack.Variances, level * Lanes);
            Finish(stack, vectors, values.Length, out mean, out variance);
        }

        private static void UpdateChunk(float[] values, int offset, int count, float[] means, float[] variances)
        {
            Array.Clear(means, 0, Lanes); Array.Clear(variances, 0, Lanes);
            for (int j = 0; j < count; ++j)
            {
                float reciprocal = 1f / (j + 1);
                for (int lane = 0; lane < Lanes; ++lane)
                {
                    float x = values[offset + j * Lanes + lane], delta = x - means[lane];
                    means[lane] = Fma(reciprocal, delta, means[lane]);
                    variances[lane] = Fma(delta, x - means[lane], variances[lane]);
                }
            }
        }

        private static void Carry(Cascade stack, int mask)
        {
            for (int level = 1; level < stack.Counts.Length && (mask & 1) == 0; ++level)
            {
                int offset = (level - 1) * Lanes;
                AddVector(stack, level, stack.Counts[level - 1], stack.Means, stack.Variances, offset);
                stack.Counts[level - 1] = 0;
                Array.Clear(stack.Means, offset, Lanes); Array.Clear(stack.Variances, offset, Lanes);
                mask >>= 1;
            }
        }

        private static void AddVector(Cascade stack, int level, int count, float[] means, float[] variances, int offset)
        {
            int previous = stack.Counts[level], total = previous + count, target = level * Lanes;
            float c = total == 0 ? 0f : (float)count / total;
            for (int lane = 0; lane < Lanes; ++lane)
            {
                float delta = means[offset + lane] - stack.Means[target + lane];
                float cDelta = c * delta, countDelta = delta * previous;
                stack.Means[target + lane] += cDelta;
                stack.Variances[target + lane] = Fma(countDelta, cDelta, stack.Variances[target + lane] + variances[offset + lane]);
            }
            stack.Counts[level] = total;
        }

        private static void Finish(Cascade stack, int vectors, int count, out float mean, out float variance)
        {
            mean = 0; variance = 0;
            int accumulated = 0;
            for (int lane = 0; lane < Lanes; ++lane)
            {
                int total = accumulated + vectors;
                float c = (float)vectors / total, delta = stack.Means[lane] - mean;
                mean = Fma(c, delta, mean);
                float product = (delta * delta) * c;
                variance += Fma(product, accumulated, stack.Variances[lane]);
                accumulated = total;
            }
            variance /= count;
        }

        private static float Fma(float a, float b, float c) => (float)((double)a * b + c);
    }
}
