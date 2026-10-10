using System;
using System.Collections.Generic;
using System.Linq;

namespace MNN.Unity
{
    internal sealed class MNNPlmsScheduler
    {
        internal readonly int[] Timesteps;
        private readonly double[] _alpha = new double[1000];
        private readonly List<float[]> _history = new List<float[]>();
        private float[] _original;
        private int _next;
        internal MNNPlmsScheduler(int steps)
        {
            if (steps < 2 || steps > 50)
                throw new ArgumentOutOfRangeException(nameof(steps));
            Timesteps = Enumerable.Range(0, steps).Select(i => 1 + (steps - 1 - i) * (1000 / steps)).ToArray();
            double cumulative = 1;
            for (int i = 0; i < 1000; ++i)
            {
                double beta = Math.Sqrt(.00085) + (Math.Sqrt(.012) - Math.Sqrt(.00085)) * i / 999;
                cumulative *= 1 - beta * beta;
                _alpha[i] = cumulative;
            }
        }

        internal float[] Step(float[] sample, float[] noise, int index)
        {
            if (sample == null || noise == null || sample.Length != noise.Length || index != _next || index >= Timesteps.Length)
                throw new ArgumentException("Invalid PLMS step order or shape.");
            ++_next;
            int t = Timesteps[index], previous = index + 1 < Timesteps.Length ? Timesteps[index + 1] : 0;
            if (index != 1)
            {
                _history.Add((float[])noise.Clone());
                if (_history.Count > 4)
                    _history.RemoveAt(0);
            }

            if (index == 0)
                _original = (float[])sample.Clone();
            else if (index == 1)
            {
                t = Timesteps[0];
                previous = Timesteps[1];
                sample = _original;
            }

            double a = _alpha[t], ap = _alpha[previous], coeff = Math.Sqrt(ap / a), denom = a * Math.Sqrt(1 - ap) + Math.Sqrt(a * (1 - a) * ap);
            var result = new float[sample.Length];
            int h = _history.Count;
            for (int i = 0; i < sample.Length; ++i)
            {
                double n = noise[i];
                if (index == 1)
                    n = (n + _history[h - 1][i]) / 2;
                else if (h == 2)
                    n = (3 * _history[1][i] - _history[0][i]) / 2;
                else if (h == 3)
                    n = (23 * _history[2][i] - 16 * _history[1][i] + 5 * _history[0][i]) / 12;
                else if (h == 4)
                    n = (55 * _history[3][i] - 59 * _history[2][i] + 37 * _history[1][i] - 9 * _history[0][i]) / 24;
                result[i] = (float)(coeff * sample[i] - (ap - a) / denom * n);
            }

            return result;
        }
    }
}
