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
        internal static float[] FireRedIstft(float[] features, CancellationToken token)
        {
            if (features == null || features.Length == 0 || features.Length % 1922 != 0)
                throw new InvalidDataException("Invalid FireRedTTS3 spectrum.");
            int frames = features.Length / 1922, length = checked((frames - 1) * 480 + 1920);
            var sum = new double[length];
            var env = new double[length];
            var window = Enumerable.Range(0, 1920).Select(n => .5 - .5 * Math.Cos(2 * Math.PI * n / 1919)).ToArray();
            var re = new double[1920];
            var im = new double[1920];
            for (int f = 0; f < frames; ++f)
            {
                if ((f & 15) == 0)
                    token.ThrowIfCancellationRequested();
                for (int k = 0; k < 961; ++k)
                {
                    float log = features[f * 1922 + k], phase = features[f * 1922 + 961 + k];
                    if (float.IsNaN(log) || float.IsInfinity(log) || float.IsNaN(phase) || float.IsInfinity(phase))
                        throw new InvalidDataException("Non-finite FireRedTTS3 spectrum.");
                    double mag = Math.Min(100, Math.Exp(log));
                    re[k] = mag * Math.Cos(phase);
                    im[k] = k == 0 || k == 960 ? 0 : mag * Math.Sin(phase);
                    if (k > 0 && k < 960)
                    {
                        re[1920 - k] = re[k];
                        im[1920 - k] = -im[k];
                    }
                }

                Fft(re, im, true);
                for (int n = 0; n < 1920; ++n)
                {
                    sum[f * 480 + n] += re[n] * window[n];
                    env[f * 480 + n] += window[n] * window[n];
                }
            }

            var pcm = new float[length - 1440];
            for (int i = 0; i < pcm.Length; ++i)
                pcm[i] = (float)(sum[i + 720] / env[i + 720]);
            return pcm;
        }

        // Cooley–Tukey: radix two plus the decoder's 1920 = 15 * 128 transform.
        internal static void Fft(double[] re, double[] im, bool inverse)
        {
            if (re == null || im == null || re.Length != im.Length)
                throw new ArgumentException("Mismatched FFT buffers.");
            int n = re.Length;
            if (n == 1920)
            {
                var subRe = new double[15][];
                var subIm = new double[15][];
                for (int r = 0; r < 15; ++r)
                {
                    subRe[r] = new double[128];
                    subIm[r] = new double[128];
                    for (int j = 0; j < 128; ++j)
                    {
                        subRe[r][j] = re[15 * j + r];
                        subIm[r][j] = im[15 * j + r];
                    }

                    Fft(subRe[r], subIm[r], inverse);
                }

                double sign = inverse ? 1 : -1;
                for (int k = 0; k < n; ++k)
                {
                    double real = 0, imaginary = 0;
                    for (int r = 0; r < 15; ++r)
                    {
                        double angle = sign * 2 * Math.PI * r * k / n, c = Math.Cos(angle), s = Math.Sin(angle);
                        real += subRe[r][k % 128] * c - subIm[r][k % 128] * s;
                        imaginary += subRe[r][k % 128] * s + subIm[r][k % 128] * c;
                    }

                    re[k] = real / (inverse ? 15 : 1);
                    im[k] = imaginary / (inverse ? 15 : 1);
                }

                return;
            }

            if (n == 0 || (n & (n - 1)) != 0 || im.Length != n)
                throw new ArgumentException("Unsupported FFT length.");
            for (int i = 1, j = 0; i < n; ++i)
            {
                int bit = n >> 1;
                for (; (j & bit) != 0; bit >>= 1)
                    j ^= bit;
                j ^= bit;
                if (i < j)
                {
                    double x = re[i];
                    re[i] = re[j];
                    re[j] = x;
                    x = im[i];
                    im[i] = im[j];
                    im[j] = x;
                }
            }

            for (int len = 2; len <= n; len <<= 1)
            {
                double angle = (inverse ? 2 : -2) * Math.PI / len, wr = Math.Cos(angle), wi = Math.Sin(angle);
                for (int start = 0; start < n; start += len)
                {
                    double c = 1, s = 0;
                    for (int j = 0; j < len / 2; ++j)
                    {
                        int a = start + j, b = a + len / 2;
                        double vr = re[b] * c - im[b] * s, vi = re[b] * s + im[b] * c;
                        re[b] = re[a] - vr;
                        im[b] = im[a] - vi;
                        re[a] += vr;
                        im[a] += vi;
                        double nextC = c * wr - s * wi;
                        s = c * wi + s * wr;
                        c = nextC;
                    }
                }
            }

            if (inverse)
                for (int i = 0; i < n; ++i)
                {
                    re[i] /= n;
                    im[i] /= n;
                }
        }
    }
}
