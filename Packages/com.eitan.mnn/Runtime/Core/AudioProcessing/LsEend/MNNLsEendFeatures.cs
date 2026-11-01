using System;
using System.Threading;

namespace MNN.Unity
{
    internal sealed class MNNLsEendFeatures
    {
        private readonly double[] _window;
        private readonly float[] _filters;
        private readonly MNNFftPlan _fft = new MNNFftPlan(256, false);
        internal MNNLsEendFeatures(MNNLsEendConfig config)
        {
            _window = config.analysis_window;
            _filters = config.mel_filters;
        }

        internal float[] Extract(float[] pcm, CancellationToken token)
        {
            int frames = (pcm.Length + 79) / 80, sampled = (frames + 9) / 10;
            var mel = new float[checked(frames * 23)];
            var cumulative = new float[23];
            var real = new double[256]; var imaginary = new double[256]; var power = new float[129];
            for (int time = 0; time < frames; ++time)
            {
                if ((time & 31) == 0) token.ThrowIfCancellationRequested();
                Array.Clear(real, 0, 256); Array.Clear(imaginary, 0, 256);
                for (int i = 0; i < 200; ++i)
                {
                    int index = time * 80 + i - 100;
                    if (index >= 0 && index < pcm.Length) real[28 + i] = pcm[index] * _window[i];
                }
                _fft.Transform(real, imaginary);
                for (int k = 0; k < 129; ++k)
                {
                    float re = (float)real[k], im = (float)imaginary[k];
                    float magnitude = (float)Math.Sqrt((double)re * re + (double)im * im);
                    power[k] = magnitude * magnitude;
                }
                for (int m = 0; m < 23; ++m)
                {
                    double energy = 0;
                    for (int k = 0; k < 129; ++k) energy += (double)power[k] * _filters[m * 129 + k];
                    float value = (float)Math.Log10(Math.Max((float)energy, 1e-10f));
                    cumulative[m] += value;
                    mel[time * 23 + m] = (float)(value - (double)cumulative[m] / (time + 1));
                }
            }
            var result = new float[checked(sampled * 345)];
            for (int t = 0; t < sampled; ++t)
            for (int context = -7; context <= 7; ++context)
            {
                int source = t * 10 + context;
                if (source >= 0 && source < frames)
                    Array.Copy(mel, source * 23, result, t * 345 + (context + 7) * 23, 23);
            }
            return result;
        }
    }
}
