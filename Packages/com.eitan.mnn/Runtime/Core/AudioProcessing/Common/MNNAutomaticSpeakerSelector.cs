using System;

namespace MNN.Unity
{
    /// <summary>Worker-owned source selection. Contribution is relative source energy, not a speech probability.
    /// Slot numbers never represent identity; supply similarity to the previously selected speaker and monotonic seconds.</summary>
    public sealed class MNNAutomaticSpeakerSelector
    {
        private readonly float _minimum, _margin;
        private double _lastSelection = double.NegativeInfinity;
        public MNNAutomaticSpeakerSelector(float minimum, float margin)
        {
            if (float.IsNaN(minimum) || minimum < 0 || minimum > 1) throw new ArgumentOutOfRangeException(nameof(minimum));
            if (float.IsNaN(margin) || margin < 0 || margin > 1) throw new ArgumentOutOfRangeException(nameof(margin));
            _minimum = minimum;
            _margin = margin;
        }
        public int Select(float[] contribution, float[] similarity, double seconds)
        {
            if (contribution == null || contribution.Length == 0 || similarity != null && similarity.Length != contribution.Length)
                throw new ArgumentException("Supply matching candidate contribution and identity evidence.");
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            int best = -1;
            float first = float.NegativeInfinity, second = float.NegativeInfinity;
            for (int i = 0; i < contribution.Length; ++i)
            {
                if (float.IsNaN(contribution[i]) || contribution[i] < _minimum || contribution[i] > 1) continue;
                float continuity = similarity != null && seconds >= _lastSelection && seconds - _lastSelection < 10 && similarity[i] >= .6f && similarity[i] <= 1 ? .12f : 0;
                float score = contribution[i] + continuity;
                if (score > first) { second = first; first = score; best = i; }
                else second = Math.Max(second, score);
            }
            if (best < 0 || first - second < _margin) return -1;
            _lastSelection = seconds;
            return best;
        }
        public void Reset() => _lastSelection = double.NegativeInfinity;
    }
}
