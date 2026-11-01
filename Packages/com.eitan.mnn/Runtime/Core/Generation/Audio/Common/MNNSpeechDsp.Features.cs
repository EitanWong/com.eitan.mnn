// Managed frontend and synthesis math following the published IndexTTS-2.5,
// FireRedTTS3 and SeamlessM4T reference algorithms. See LICENSE.md.
using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace MNN.Unity
{
    internal static partial class MNNSpeechDsp
    {
        internal static float[] Fbank(float[] audio, bool seamless, out int frames, CancellationToken token)
        {
            frames = (audio.Length - 400) / 160 + 1;
            if (frames < 2)
                throw new ArgumentException("Reference audio is too short for feature extraction.");
            var bank = new double[80, 257];
            double low = 1127 * Math.Log(1 + 20.0 / 700), high = 1127 * Math.Log(1 + 8000.0 / 700), delta = (high - low) / 81;
            for (int m = 0; m < 80; ++m)
                for (int k = 0; k < 256; ++k)
                {
                    double mel = 1127 * Math.Log(1 + k * 16000.0 / 512 / 700);
                    bank[m, k] = Math.Max(0, Math.Min((mel - low - m * delta) / delta, (low + (m + 2) * delta - mel) / delta));
                }

            var window = Enumerable.Range(0, 400).Select(n => Math.Pow(.5 - .5 * Math.Cos(2 * Math.PI * n / 399), .85)).ToArray();
            var result = new float[frames * 80];
            var re = new double[512];
            var im = new double[512];
            for (int f = 0; f < frames; ++f)
            {
                if ((f & 31) == 0)
                    token.ThrowIfCancellationRequested();
                double mean = 0, scale = seamless ? 32768 : 1;
                for (int n = 0; n < 400; ++n)
                    mean += audio[f * 160 + n] * scale / 400;
                Array.Clear(re, 0, re.Length);
                Array.Clear(im, 0, im.Length);
                for (int n = 0; n < 400; ++n)
                {
                    double current = audio[f * 160 + n] * scale - mean;
                    double previous = audio[f * 160 + Math.Max(0, n - 1)] * scale - mean;
                    re[n] = (current - .97 * previous) * window[n];
                }

                Fft(re, im, false);
                for (int m = 0; m < 80; ++m)
                {
                    double sum = 0;
                    for (int k = 0; k < 257; ++k)
                        sum += (re[k] * re[k] + im[k] * im[k]) * bank[m, k];
                    result[f * 80 + m] = (float)Math.Log(Math.Max(1.192092955078125e-7, sum));
                }
            }

            for (int m = 0; m < 80; ++m)
            {
                double mean = 0, variance = 0;
                for (int f = 0; f < frames; ++f)
                    mean += result[f * 80 + m] / (double)frames;
                if (seamless)
                    for (int f = 0; f < frames; ++f)
                        variance += Math.Pow(result[f * 80 + m] - mean, 2) / (frames - 1);
                double divisor = seamless ? Math.Sqrt(variance + 1e-7) : 1;
                for (int f = 0; f < frames; ++f)
                    result[f * 80 + m] = (float)((result[f * 80 + m] - mean) / divisor);
            }

            if (!seamless)
                return result;
            // Pad one frame when odd, then stack adjacent frames. Last mask is zero.
            int stacked = (frames + 1) / 2;
            Array.Resize(ref result, stacked * 160);
            frames = stacked;
            return result;
        }

        internal static float[] Mel22050(float[] audio, out int frames, CancellationToken token)
        {
            frames = (audio.Length + 768 - 1024) / 256 + 1;
            var filters = new double[80, 513];
            Func<double, double> toMel = hz => hz < 1000 ? hz / (200.0 / 3) : 15 + Math.Log(hz / 1000) / (Math.Log(6.4) / 27);
            Func<double, double> toHz = mel => mel < 15 ? mel * (200.0 / 3) : 1000 * Math.Exp((mel - 15) * Math.Log(6.4) / 27);
            var points = Enumerable.Range(0, 82).Select(i => toHz(i * toMel(11025) / 81)).ToArray();
            for (int m = 0; m < 80; ++m)
                for (int k = 0; k < 513; ++k)
                {
                    double hz = k * 22050.0 / 1024;
                    filters[m, k] = Math.Max(0, Math.Min((hz - points[m]) / (points[m + 1] - points[m]), (points[m + 2] - hz) / (points[m + 2] - points[m + 1]))) * 2 / (points[m + 2] - points[m]);
                }

            var output = new float[80 * frames];
            var re = new double[1024];
            var im = new double[1024];
            for (int f = 0; f < frames; ++f)
            {
                if ((f & 31) == 0)
                    token.ThrowIfCancellationRequested();
                Array.Clear(im, 0, im.Length);
                for (int n = 0; n < 1024; ++n)
                {
                    int p = f * 256 + n - 384;
                    if (p < 0)
                        p = -p;
                    if (p >= audio.Length)
                        p = 2 * audio.Length - p - 2;
                    re[n] = audio[p] * (.5 - .5 * Math.Cos(2 * Math.PI * n / 1024));
                }

                Fft(re, im, false);
                for (int m = 0; m < 80; ++m)
                {
                    double sum = 0;
                    for (int k = 0; k < 513; ++k)
                        sum += Math.Sqrt(re[k] * re[k] + im[k] * im[k] + 1e-9) * filters[m, k];
                    output[m * frames + f] = (float)Math.Log(Math.Max(1e-5, sum));
                }
            }

            return output;
        }
    }
}
