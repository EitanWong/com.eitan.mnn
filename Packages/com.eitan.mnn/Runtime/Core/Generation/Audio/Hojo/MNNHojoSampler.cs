using System;
using System.Collections.Generic;

namespace MNN.Unity
{
    // One synthesis owns its sampling state. A max heap visits only the sorted
    // nucleus prefix; reusable buffers avoid full-vocabulary sorting per token.
    internal sealed class MNNHojoSampler
    {
        private readonly float[] _scores;
        private readonly double[] _probabilities;
        private readonly int[] _heap, _nucleus;
        private readonly Random _random;
        internal MNNHojoSampler(int vocabularySize, int seed)
        {
            if (vocabularySize < 1) throw new ArgumentOutOfRangeException(nameof(vocabularySize));
            _scores = new float[vocabularySize];
            _probabilities = new double[vocabularySize];
            _heap = new int[vocabularySize];
            _nucleus = new int[vocabularySize];
            _random = new Random(seed);
        }

        internal int Sample(float[] logits, float temperature, HashSet<int> history)
        {
            if (logits.Length != _scores.Length) throw new ArgumentException("Unexpected Hojo vocabulary size.", nameof(logits));
            Array.Copy(logits, _scores, logits.Length);
            foreach (int id in history) _scores[id] = _scores[id] > 0 ? _scores[id] / 1.1f : _scores[id] * 1.1f;
            int best = 0;
            for (int i = 1; i < _scores.Length; ++i) if (_scores[i] > _scores[best]) best = i;
            if (temperature == 0) return best;
            double total = PrepareHeap(temperature, best), retained = 0;
            int remaining = _heap.Length, count = 0;
            do
            {
                int next = _heap[0];
                _nucleus[count++] = next;
                retained += _probabilities[next];
                _heap[0] = _heap[--remaining];
                SiftDown(0, remaining);
            } while (remaining > 0 && retained <= total * .95);
            double draw = _random.NextDouble() * retained;
            for (int i = 0; i < count; ++i)
                if ((draw -= _probabilities[_nucleus[i]]) <= 0) return _nucleus[i];
            return _nucleus[count - 1];
        }

        private double PrepareHeap(float temperature, int best)
        {
            double total = 0;
            for (int i = 0; i < _scores.Length; ++i)
            {
                _heap[i] = i;
                total += _probabilities[i] = Math.Exp((_scores[i] - _scores[best]) / temperature);
            }
            for (int i = _heap.Length / 2 - 1; i >= 0; --i) SiftDown(i, _heap.Length);
            return total;
        }

        private bool Before(int left, int right)
            => _scores[left] > _scores[right] || _scores[left] == _scores[right] && left < right;

        private void SiftDown(int at, int count)
        {
            int value = _heap[at];
            while (at < count / 2)
            {
                int child = at * 2 + 1;
                if (child + 1 < count && Before(_heap[child + 1], _heap[child])) ++child;
                if (!Before(_heap[child], value)) break;
                _heap[at] = _heap[child]; at = child;
            }
            _heap[at] = value;
        }
    }
}
