using System;
using System.Collections.Generic;
using System.IO;

namespace MNN.Unity
{
    // Official two-draw RAS policy. The managed RNG is deterministic per runtime;
    // it intentionally does not claim NumPy PCG64 seed/PCM equivalence.
    internal sealed class MNNCosyVoice300MSampler
    {
        private readonly Random _random;
        private readonly int _seed;
        private int _draws;
        private readonly double[] _probabilities = new double[4097];
        private readonly int[] _order = new int[25];
        private bool _spare;
        private float _normal;
        internal MNNCosyVoice300MSampler(int seed) { _seed = seed; _random = new Random(seed); }
        internal double Uniform() { ++_draws; return _random.NextDouble(); }
        internal MNNCosyVoice300MSampler Fork()
        {
            var copy = new MNNCosyVoice300MSampler(_seed);
            for (int i = 0; i < _draws; ++i) copy.Uniform();
            copy._spare = _spare; copy._normal = _normal;
            return copy;
        }
        internal float Normal()
        {
            if (_spare) { _spare = false; return _normal; }
            double radius = Math.Sqrt(-2 * Math.Log(1 - Uniform())), angle = 2 * Math.PI * Uniform();
            _normal = (float)(radius * Math.Sin(angle)); _spare = true;
            return (float)(radius * Math.Cos(angle));
        }
        internal int Sample(float[] logits, List<int> history, int minimum)
        {
            if (logits.Length != 4097) throw new InvalidDataException("Invalid CosyVoice-300M logits.");
            double max = double.NegativeInfinity;
            for (int i = 0; i < 4097; ++i)
            {
                if (float.IsNaN(logits[i]) || float.IsInfinity(logits[i])) throw new InvalidDataException("Non-finite speech logits.");
                if (i != 4096 || history.Count >= minimum) max = Math.Max(max, logits[i]);
            }
            double total = 0;
            for (int i = 0; i < 4097; ++i)
            {
                _probabilities[i] = i == 4096 && history.Count < minimum ? 0 : Math.Exp(logits[i] - max);
                total += _probabilities[i];
            }
            for (int i = 0; i < 4097; ++i) _probabilities[i] /= total;
            // RAS only consumes the first 25 sorted probabilities. Keep that exact
            // prefix, including ascending token-ID ties and underflowed zeros.
            int filled = 0;
            for (int token = 0; token < 4097; ++token)
            {
                double probability = _probabilities[token];
                if (filled == 25 && probability <= _probabilities[_order[24]]) continue;
                int at = Math.Min(filled, 24);
                while (at > 0 && probability > _probabilities[_order[at - 1]])
                {
                    _order[at] = _order[at - 1];
                    --at;
                }
                _order[at] = token;
                if (filled < 25) ++filled;
            }
            double mass = 0; int count = 0;
            do { mass += _probabilities[_order[count++]]; } while (count < 25 && mass < .8);
            double pick = Uniform() * mass, cumulative = 0;
            int selected = _order[count - 1];
            for (int i = 0; i < count; ++i)
            {
                cumulative += _probabilities[_order[i]];
                if (pick < cumulative) { selected = _order[i]; break; }
            }
            for (int i = Math.Max(0, history.Count - 10); i < history.Count; ++i)
                if (history[i] == selected)
                {
                    pick = Uniform(); cumulative = 0;
                    for (int j = 0; j < 4097; ++j)
                    {
                        cumulative += _probabilities[j];
                        if (pick < cumulative) return j;
                    }
                    for (int j = 4096; j >= 0; --j)
                        if (_probabilities[j] > 0) return j;
                    throw new InvalidDataException("Empty speech probability distribution.");
                }
            return selected;
        }
    }
}
