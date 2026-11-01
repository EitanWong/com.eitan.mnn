using System;

namespace MNN.Unity
{
    /// <summary>Cosine similarity for finite nonzero vectors in the same embedding space.</summary>
    public static class MNNVectorSimilarity
    {
        public static float Cosine(float[] left, float[] right)
        {
            if (left == null || right == null || left.Length != right.Length || left.Length == 0) throw new ArgumentException("Embeddings must have the same nonzero dimension.");
            double dot = 0, a = 0, b = 0;
            for (int i = 0; i < left.Length; ++i)
            {
                if (float.IsNaN(left[i]) || float.IsInfinity(left[i]) || float.IsNaN(right[i]) || float.IsInfinity(right[i])) throw new ArgumentException("Embeddings must be finite.");
                dot += left[i] * right[i]; a += left[i] * left[i]; b += right[i] * right[i];
            }
            if (a <= 1e-24 || b <= 1e-24) throw new ArgumentException("Embeddings must have nonzero norms.");
            return (float)Math.Max(-1, Math.Min(1, dot / Math.Sqrt(a * b)));
        }
    }
}
