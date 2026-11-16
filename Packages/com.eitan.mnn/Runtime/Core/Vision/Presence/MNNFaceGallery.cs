using System;
using System.Collections.Generic;

namespace MNN.Unity
{
    /// <summary>In-memory cosine matching for MobileFaceNet's aligned 512-D embeddings.
    /// Thresholds require application calibration. This is neither liveness nor an access-control credential.</summary>
    public sealed class MNNFaceGallery
    {
        private readonly Dictionary<string, float[]> _entries = new Dictionary<string, float[]>(StringComparer.Ordinal);
        private readonly float _minimum, _margin;

        public MNNFaceGallery(float minimumSimilarity = .65f, float ambiguityMargin = .1f)
        {
            if (float.IsNaN(minimumSimilarity) || minimumSimilarity < -1 || minimumSimilarity > 1)
                throw new ArgumentOutOfRangeException(nameof(minimumSimilarity));
            if (float.IsNaN(ambiguityMargin) || ambiguityMargin < 0 || ambiguityMargin > 2)
                throw new ArgumentOutOfRangeException(nameof(ambiguityMargin));
            _minimum = minimumSimilarity;
            _margin = ambiguityMargin;
        }

        public void Enroll(string identity, float[] embedding)
        {
            if (string.IsNullOrWhiteSpace(identity)) throw new ArgumentException("Supply an identity.", nameof(identity));
            _entries[identity] = Normalize(embedding);
        }

        public bool Remove(string identity) => _entries.Remove(identity);
        public void Clear() => _entries.Clear();

        public string Recognize(float[] embedding)
        {
            var query = Normalize(embedding);
            double best = double.NegativeInfinity, second = double.NegativeInfinity;
            string match = null;
            foreach (var pair in _entries)
            {
                double score = 0;
                for (int i = 0; i < query.Length; ++i) score += (double)query[i] * pair.Value[i];
                if (score > best) { second = best; best = score; match = pair.Key; }
                else second = Math.Max(second, score);
            }
            return best >= _minimum && best - second > _margin ? match : null;
        }

        private static float[] Normalize(float[] embedding)
        {
            if (embedding == null || embedding.Length != 512) throw new ArgumentException("Expected a 512-D face embedding.", nameof(embedding));
            double norm = 0;
            foreach (float value in embedding)
            {
                if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentException("Embedding must be finite.", nameof(embedding));
                norm += (double)value * value;
            }
            if (norm < 1e-12) throw new ArgumentException("Embedding has zero norm.", nameof(embedding));
            var result = new float[embedding.Length];
            norm = Math.Sqrt(norm);
            for (int i = 0; i < result.Length; ++i) result[i] = (float)(embedding[i] / norm);
            return result;
        }
    }
}
