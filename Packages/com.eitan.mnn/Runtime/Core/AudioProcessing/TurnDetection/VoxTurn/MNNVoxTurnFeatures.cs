using System;
using System.Threading;
namespace MNN.Unity
{
    internal sealed class MNNVoxTurnFeatures
    {
        private readonly float[] _window, _filters;
        private readonly MNNSourceRealFft _fft = new MNNSourceRealFft(400);
        private readonly float[] _frame = new float[400], _real = new float[201], _imaginary = new float[201], _power = new float[201];
        internal MNNVoxTurnFeatures(float[] window, float[] filters)
        {
            if (window.Length != 400 || filters.Length != 128 * 201)
                throw new ArgumentException("Invalid VoxTurn mel constants.");
            _window = window;
            _filters = filters;
        }
        internal float[] Extract(float[] samples, CancellationToken token)
        {
            var pcm = new float[128000];
            int count = samples == null ? 0 : Math.Min(samples.Length, pcm.Length);
            if (count > 0)
                Array.Copy(samples, samples.Length - count, pcm, pcm.Length - count, count);
            var mel = new float[128 * 800];
            float maximum = -10;
            for (int t = 0; t < 800; ++t)
            {
                if ((t & 31) == 0)
                    token.ThrowIfCancellationRequested();
                Frame(pcm, t);
                _fft.Forward(_frame, _real, _imaginary);
                for (int k = 0; k < 201; ++k)
                {
                    float magnitude = (float)Math.Sqrt((double)_real[k] * _real[k] + (double)_imaginary[k] * _imaginary[k]);
                    _power[k] = magnitude * magnitude;
                }
                for (int m = 0; m < 128; ++m)
                {
                    double sum = 0;
                    for (int k = 0; k < 201; ++k)
                        sum += (double)_filters[m * 201 + k] * _power[k];
                    float value = (float)Math.Log10(Math.Max(1e-10f, (float)sum));
                    mel[m * 800 + t] = value;
                    maximum = Math.Max(maximum, value);
                }
            }
            float floor = maximum - 8;
            for (int i = 0; i < mel.Length; ++i)
                mel[i] = (Math.Max(mel[i], floor) + 4) / 4;
            return mel;
        }
        private void Frame(float[] pcm, int time)
        {
            for (int i = 0; i < 400; ++i)
            {
                int at = time * 160 + i - 200;
                if (at < 0)
                    at = -at;
                if (at >= pcm.Length)
                    at = 2 * pcm.Length - at - 2;
                _frame[i] = pcm[at] * _window[i];
            }
        }
    }
}
