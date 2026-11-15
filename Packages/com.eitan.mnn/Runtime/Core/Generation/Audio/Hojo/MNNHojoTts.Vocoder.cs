using System;
using System.IO;
using System.Threading;

namespace MNN.Unity
{
    public sealed partial class MNNHojoTts
    {
        private static float[] InvertSpectrum(MNNGenerationGraph.Output magnitude, MNNGenerationGraph.Output phase, CancellationToken token)
        {
            if (magnitude.Shape.Length != 2 || magnitude.Shape[0] != 961 || phase.Values.Length != magnitude.Values.Length)
                throw new InvalidDataException("Expected Hojo [961, frames] magnitude and phase.");
            int frames = magnitude.Shape[1], size = checked((frames - 1) * 480 + 1920);
            if (frames < 1) throw new InvalidDataException("Empty Hojo spectrum.");
            var sum = new double[size]; var envelope = new double[size]; var window = new double[1920];
            for (int i = 0; i < 1920; ++i) window[i] = .5 - .5 * Math.Cos(2 * Math.PI * i / 1920);
            var fft = new MNNFftPlan(1920); var real = new double[1920]; var imaginary = new double[1920];
            for (int frame = 0; frame < frames; ++frame)
            {
                token.ThrowIfCancellationRequested();
                for (int k = 0; k <= 960; ++k)
                {
                    int index = k * frames + frame;
                    double mag = Math.Min(100, Math.Max(1e-12, magnitude.Values[index])), angle = phase.Values[index];
                    real[k] = mag * Math.Cos(angle); imaginary[k] = k == 0 || k == 960 ? 0 : mag * Math.Sin(angle);
                    if (k > 0 && k < 960) { real[1920 - k] = real[k]; imaginary[1920 - k] = -imaginary[k]; }
                }
                fft.Transform(real, imaginary, true);
                for (int n = 0; n < 1920; ++n)
                {
                    int index = frame * 480 + n;
                    sum[index] += real[n] * window[n]; envelope[index] += window[n] * window[n];
                }
            }
            var pcm = new float[frames * 480];
            for (int i = 0; i < pcm.Length; ++i)
            {
                int index = i + 720;
                if (envelope[index] <= 1e-11) throw new InvalidDataException("Invalid Hojo window envelope.");
                pcm[i] = (float)(sum[index] / envelope[index]);
            }
            return pcm;
        }
    }
}
