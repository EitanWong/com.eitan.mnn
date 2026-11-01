// Adapted from Nian27/CosyVoice3-MNN (Copyright 2026 Nian27), Apache-2.0.
// Modified for managed signal processing and cancellation. See LICENSE.md.
using System;
using System.IO;
using System.Threading;

namespace MNN.Unity
{
    // Periodic Hann, centered 16-point FFT and hop 4, shared by HiFT models.
    internal static class MNNSpeechSpectral16
    {
        private static readonly double[] Cos = new double[8], Sin = new double[8];
        private static readonly int[] Reverse = new int[16];
        private static readonly float[] Window = new float[16];
        static MNNSpeechSpectral16()
        {
            for (int n = 0; n < 16; ++n)
            {
                Window[n] = (float)(.5 - .5 * Math.Cos(2 * Math.PI * n / 16));
                Reverse[n] = ((n & 1) << 3) | ((n & 2) << 1) | ((n & 4) >> 1) | ((n & 8) >> 3);
                if (n < 8)
                {
                    Cos[n] = Math.Cos(2 * Math.PI * n / 16);
                    Sin[n] = Math.Sin(2 * Math.PI * n / 16);
                }
            }
        }

        internal static float[] Stft(float[] source, CancellationToken token)
        {
            if (source == null || source.Length < 16 || source.Length % 4 != 0)
                throw new InvalidDataException("Invalid HiFT source length.");
            int frames = source.Length / 4 + 1;
            var result = new float[checked(18 * frames)];
            var re = new double[16];
            var im = new double[16];
            for (int f = 0; f < frames; ++f)
            {
                if ((f & 255) == 0)
                    token.ThrowIfCancellationRequested();
                for (int n = 0; n < 16; ++n)
                {
                    int index = f * 4 - 8 + n;
                    if (index < 0)
                        index = -index;
                    if (index >= source.Length)
                        index = 2 * source.Length - 2 - index;
                    re[Reverse[n]] = source[index] * Window[n];
                    im[Reverse[n]] = 0;
                }

                Transform(re, im, false);
                for (int b = 0; b < 9; ++b)
                {
                    result[b * frames + f] = (float)re[b];
                    result[(b + 9) * frames + f] = (float)im[b];
                }
            }

            return result;
        }

        // Fixed 16-point radix-2 FFT. Inputs are written in bit-reversed
        // order; scratch arrays and twiddles are reused for every frame.
        private static void Transform(double[] re, double[] im, bool inverse)
        {
            for (int length = 2; length <= 16; length *= 2)
            {
                int half = length / 2, stride = 16 / length;
                for (int block = 0; block < 16; block += length)
                for (int j = 0; j < half; ++j)
                {
                    int a = block + j, b = a + half, twiddle = j * stride;
                    double cosine = Cos[twiddle], sine = inverse ? Sin[twiddle] : -Sin[twiddle];
                    double real = re[b] * cosine - im[b] * sine;
                    double imaginary = re[b] * sine + im[b] * cosine;
                    re[b] = re[a] - real;
                    im[b] = im[a] - imaginary;
                    re[a] += real;
                    im[a] += imaginary;
                }
            }
        }

        internal static float[] Istft(float[] spectral, int frames, CancellationToken token)
        {
            if (frames < 2 || frames > 240001 || spectral == null || spectral.Length != checked(18 * frames))
                throw new InvalidDataException("Invalid HiFT spectral shape.");
            var accumulated = new double[(frames - 1) * 4 + 16];
            var envelope = new double[accumulated.Length];
            var re = new double[16];
            var im = new double[16];
            for (int f = 0; f < frames; ++f)
            {
                if ((f & 255) == 0)
                    token.ThrowIfCancellationRequested();
                for (int b = 0; b < 9; ++b)
                {
                    float log = spectral[b * frames + f], raw = spectral[(b + 9) * frames + f];
                    if (float.IsNaN(log) || float.IsInfinity(log) || float.IsNaN(raw) || float.IsInfinity(raw))
                        throw new InvalidDataException("Non-finite HiFT spectrum.");
                    double magnitude = Math.Min(100, Math.Exp(log)), phase = Math.Sin(raw);
                    re[Reverse[b]] = magnitude * Math.Cos(phase);
                    im[Reverse[b]] = b == 0 || b == 8 ? 0 : magnitude * Math.Sin(phase);
                    if (b > 0 && b < 8)
                    {
                        re[Reverse[16 - b]] = re[Reverse[b]];
                        im[Reverse[16 - b]] = -im[Reverse[b]];
                    }
                }

                Transform(re, im, true);
                for (int n = 0; n < 16; ++n)
                {
                    int index = f * 4 + n;
                    accumulated[index] += re[n] / 16 * Window[n];
                    envelope[index] += (double)Window[n] * Window[n];
                }
            }

            var pcm = new float[(frames - 1) * 4];
            for (int i = 0; i < pcm.Length; ++i)
            {
                int j = i + 8;
                pcm[i] = (float)Math.Max(-.99, Math.Min(.99, envelope[j] > 1e-11 ? accumulated[j] / envelope[j] : accumulated[j]));
            }

            return pcm;
        }
    }
}
