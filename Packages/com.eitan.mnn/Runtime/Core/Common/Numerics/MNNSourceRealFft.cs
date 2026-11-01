using System;

namespace MNN.Unity
{
    // Managed source-ordered real FFT for the verified 400/640/1680/2048-point audio frontends.
    // Single precision and operation order preserve the upstream cancellation residuals.
    // Copyright (C) 2010-2024 Max-Planck-Society; 2019-2020 Peter Bell.
    // BSD-3-Clause notice: Documentation~/PocketFftLicense.md.
    internal sealed class MNNSourceRealFft
    {
        private readonly int _length;
        private readonly float[][] _twiddles;
        private readonly float[] _scratch;
        private readonly float[] _seventhRoots;

        internal MNNSourceRealFft(int length)
        {
            if (length != 400 && length != 640 && length != 1680 && length != 2048) throw new ArgumentOutOfRangeException(nameof(length));
            _length = length; _scratch = new float[length];
            _twiddles = length == 400
                ? new[] { Twiddles(4, 100, 1), Twiddles(4, 25, 4), Twiddles(5, 5, 16) }
                : length == 640
                    ? new[] { Twiddles(2, 320, 1), Twiddles(4, 80, 2), Twiddles(4, 20, 8), Twiddles(4, 5, 32) }
                    : length == 1680
                        ? new[] { Twiddles(4, 420, 1), Twiddles(4, 105, 4), Twiddles(3, 35, 16), Twiddles(5, 7, 48) }
                        : new[] { Twiddles(2, 1024, 1), Twiddles(4, 256, 2), Twiddles(4, 64, 8), Twiddles(4, 16, 32), Twiddles(4, 4, 128) };
            if (length == 1680)
            {
                _seventhRoots = new float[14]; _seventhRoots[0] = 1;
                for (int i = 1; i <= 3; ++i)
                {
                    Root(i * 240, out float real, out float imaginary);
                    _seventhRoots[2 * i] = _seventhRoots[2 * (7 - i)] = real;
                    _seventhRoots[2 * i + 1] = imaginary; _seventhRoots[2 * (7 - i) + 1] = -imaginary;
                }
            }
        }

        // Input is a caller-owned work buffer and is overwritten. A plan belongs to one call owner.
        internal void Forward(float[] input, float[] real, float[] imaginary)
        {
            int bins = _length / 2 + 1;
            if (input.Length != _length || real.Length != bins || imaginary.Length != bins)
                throw new ArgumentException("Supply real input and one-sided output buffers matching the plan.");
            if (_length == 400)
            {
                Radix5(1, 80, input, _scratch, Array.Empty<float>());
                Radix5(5, 16, _scratch, input, _twiddles[2]);
                Radix4(25, 4, input, _scratch, _twiddles[1]);
                Radix4(100, 1, _scratch, input, _twiddles[0]);
            }
            else if (_length == 640)
            {
                Radix5(1, 128, input, _scratch, Array.Empty<float>());
                Radix4(5, 32, _scratch, input, _twiddles[3]);
                Radix4(20, 8, input, _scratch, _twiddles[2]);
                Radix4(80, 2, _scratch, input, _twiddles[1]);
                Radix2(320, 1, input, _scratch, _twiddles[0]);
                Array.Copy(_scratch, input, _length);
            }
            else if (_length == 1680)
            {
                Radix7First(input, _scratch);
                Radix5(7, 48, input, _scratch, _twiddles[3]);
                Radix3(35, 16, _scratch, input, _twiddles[2]);
                Radix4(105, 4, input, _scratch, _twiddles[1]);
                Radix4(420, 1, _scratch, input, _twiddles[0]);
            }
            else
            {
                Radix4(1, 512, input, _scratch, Array.Empty<float>());
                Radix4(4, 128, _scratch, input, _twiddles[4]);
                Radix4(16, 32, input, _scratch, _twiddles[3]);
                Radix4(64, 8, _scratch, input, _twiddles[2]);
                Radix4(256, 2, input, _scratch, _twiddles[1]);
                Radix2(1024, 1, _scratch, input, _twiddles[0]);
            }
            real[0] = input[0]; imaginary[0] = 0;
            for (int i = 1; i < bins - 1; ++i) { real[i] = input[2 * i - 1]; imaginary[i] = input[2 * i]; }
            real[bins - 1] = input[_length - 1]; imaginary[bins - 1] = 0;
        }

        private float[] Twiddles(int radix, int width, int stride)
        {
            var result = new float[(radix - 1) * (width - 1)];
            for (int j = 1; j < radix; ++j)
            for (int i = 1; i <= (width - 1) / 2; ++i)
            {
                int index = (j - 1) * (width - 1) + 2 * i - 2;
                Root(j * stride * i, out result[index], out result[index + 1]);
            }
            return result;
        }

        private void Root(int phase, out float real, out float imaginary)
        {
            int quadrant = phase / (_length / 4), offset = phase % (_length / 4);
            double angle = 2 * Math.PI * offset / _length;
            float c = (float)Math.Cos(angle), s = (float)Math.Sin(angle);
            real = quadrant == 0 ? c : quadrant == 1 ? -s : quadrant == 2 ? -c : s;
            imaginary = quadrant == 0 ? s : quadrant == 1 ? c : quadrant == 2 ? -s : -c;
        }

        // Specialization of radfg for the final factor 7 (width 1). Its output
        // remains in input, matching pocketfft's extra buffer swap for general factors.
        private void Radix7First(float[] input, float[] temporary)
        {
            const int groups = 240;
            for (int j = 1; j < 4; ++j)
            for (int k = 0; k < groups; ++k)
            {
                int left = k + groups * (7 - j), right = k + groups * j;
                float original = input[left]; input[left] -= input[right]; input[right] += original;
            }
            for (int j = 1; j < 4; ++j)
            {
                int phase = 3 * j % 7;
                for (int k = 0; k < groups; ++k)
                {
                    float real = Fma(_seventhRoots[4 * j], input[k + groups * 2], Fma(_seventhRoots[2 * j], input[k + groups], input[k]));
                    float imaginary = Fma(_seventhRoots[2 * j + 1], input[k + groups * 6], _seventhRoots[4 * j + 1] * input[k + groups * 5]);
                    temporary[k + groups * j] = Fma(_seventhRoots[2 * phase], input[k + groups * 3], real);
                    temporary[k + groups * (7 - j)] = Fma(_seventhRoots[2 * phase + 1], input[k + groups * 4], imaginary);
                }
            }
            for (int k = 0; k < groups; ++k)
                temporary[k] = ((input[k] + input[k + groups]) + input[k + groups * 2]) + input[k + groups * 3];
            for (int k = 0; k < groups; ++k)
            {
                input[7 * k] = temporary[k];
                for (int j = 1; j < 4; ++j)
                {
                    input[7 * k + 2 * j - 1] = temporary[k + groups * j];
                    input[7 * k + 2 * j] = temporary[k + groups * (7 - j)];
                }
            }
        }

        private static void Radix3(int width, int groups, float[] input, float[] output, float[] w)
        {
            const float tau = .8660254037844386468f;
            float C(int a, int b, int c) => input[a + width * (b + groups * c)];
            void H(int a, int b, int c, float value) => output[a + width * (b + 3 * c)] = value;
            for (int k = 0; k < groups; ++k)
            {
                float sum = C(0, k, 1) + C(0, k, 2);
                H(0, 0, k, C(0, k, 0) + sum); H(0, 2, k, tau * (C(0, k, 2) - C(0, k, 1)));
                H(width - 1, 1, k, Fma(-.5f, sum, C(0, k, 0)));
            }
            for (int k = 0; k < groups; ++k)
            for (int i = 2; i < width; i += 2)
            {
                int opposite = width - i;
                Multiply(w, i - 2, C(i - 1, k, 1), C(i, k, 1), out float dr2, out float di2);
                Multiply(w, width - 1 + i - 2, C(i - 1, k, 2), C(i, k, 2), out float dr3, out float di3);
                Rearrange(ref dr2, ref di2, ref dr3, ref di3);
                H(i - 1, 0, k, C(i - 1, k, 0) + dr2); H(i, 0, k, C(i, k, 0) + di2);
                float tr2 = Fma(-.5f, dr2, C(i - 1, k, 0)), ti2 = Fma(-.5f, di2, C(i, k, 0));
                float tr3 = tau * dr3, ti3 = tau * di3;
                H(i - 1, 2, k, tr2 + tr3); H(opposite - 1, 1, k, tr2 - tr3);
                H(i, 2, k, ti3 + ti2); H(opposite, 1, k, ti3 - ti2);
            }
        }

        // A double holds the exact product of two floats. Round once after addition,
        // preserving the float fused multiply-add used by the upstream ARM FFT.
        private static float Fma(float a, float b, float c) => (float)((double)a * b + c);

        private static void Multiply(float[] w, int index, float re, float im, out float r, out float i)
        {
            r = Fma(w[index], re, w[index + 1] * im);
            i = Fma(w[index], im, -(w[index + 1] * re));
        }

        private static void Rearrange(ref float rx, ref float ix, ref float ry, ref float iy)
        {
            float a = rx + ry, b = ry - rx, c = ix + iy, d = ix - iy;
            rx = a; ix = c; ry = d; iy = b;
        }

        private static void Radix2(int width, int groups, float[] input, float[] output, float[] w)
        {
            float C(int a, int b, int c) => input[a + width * (b + groups * c)];
            void H(int a, int b, int c, float value) => output[a + width * (b + 2 * c)] = value;
            for (int k = 0; k < groups; ++k)
            {
                H(0, 0, k, C(0, k, 0) + C(0, k, 1));
                H(width - 1, 1, k, C(0, k, 0) - C(0, k, 1));
            }
            if ((width & 1) == 0)
            for (int k = 0; k < groups; ++k)
            {
                H(0, 1, k, -C(width - 1, k, 1));
                H(width - 1, 0, k, C(width - 1, k, 0));
            }
            for (int k = 0; k < groups; ++k)
            for (int i = 2; i < width; i += 2)
            {
                int opposite = width - i;
                Multiply(w, i - 2, C(i - 1, k, 1), C(i, k, 1), out float real, out float imaginary);
                H(i - 1, 0, k, C(i - 1, k, 0) + real);
                H(opposite - 1, 1, k, C(i - 1, k, 0) - real);
                H(i, 0, k, imaginary + C(i, k, 0));
                H(opposite, 1, k, imaginary - C(i, k, 0));
            }
        }

        private static void Radix4(int width, int groups, float[] input, float[] output, float[] w)
        {
            float C(int a, int b, int c) => input[a + width * (b + groups * c)];
            void H(int a, int b, int c, float value) => output[a + width * (b + 4 * c)] = value;
            for (int k = 0; k < groups; ++k)
            {
                float tr1 = C(0, k, 3) + C(0, k, 1), tr2 = C(0, k, 0) + C(0, k, 2);
                H(0, 2, k, C(0, k, 3) - C(0, k, 1)); H(width - 1, 1, k, C(0, k, 0) - C(0, k, 2));
                H(0, 0, k, tr2 + tr1); H(width - 1, 3, k, tr2 - tr1);
            }
            if ((width & 1) == 0)
            for (int k = 0; k < groups; ++k)
            {
                float ti1 = -.7071067811865475244f * (C(width - 1, k, 1) + C(width - 1, k, 3));
                float tr1 = .7071067811865475244f * (C(width - 1, k, 1) - C(width - 1, k, 3));
                H(width - 1, 0, k, C(width - 1, k, 0) + tr1); H(width - 1, 2, k, C(width - 1, k, 0) - tr1);
                H(0, 3, k, ti1 + C(width - 1, k, 2)); H(0, 1, k, ti1 - C(width - 1, k, 2));
            }
            for (int k = 0; k < groups; ++k)
            for (int i = 2; i < width; i += 2)
            {
                int opposite = width - i;
                Multiply(w, i - 2, C(i - 1, k, 1), C(i, k, 1), out float cr2, out float ci2);
                Multiply(w, width - 1 + i - 2, C(i - 1, k, 2), C(i, k, 2), out float cr3, out float ci3);
                Multiply(w, 2 * (width - 1) + i - 2, C(i - 1, k, 3), C(i, k, 3), out float cr4, out float ci4);
                float tr1 = cr4 + cr2, tr4 = cr4 - cr2, ti1 = ci2 + ci4, ti4 = ci2 - ci4;
                float tr2 = C(i - 1, k, 0) + cr3, tr3 = C(i - 1, k, 0) - cr3;
                float ti2 = C(i, k, 0) + ci3, ti3 = C(i, k, 0) - ci3;
                H(i - 1, 0, k, tr2 + tr1); H(opposite - 1, 3, k, tr2 - tr1);
                H(i, 0, k, ti1 + ti2); H(opposite, 3, k, ti1 - ti2);
                H(i - 1, 2, k, tr3 + ti4); H(opposite - 1, 1, k, tr3 - ti4);
                H(i, 2, k, tr4 + ti3); H(opposite, 1, k, tr4 - ti3);
            }
        }

        private static void Radix5(int width, int groups, float[] input, float[] output, float[] w)
        {
            const float tr11 = .3090169943749474241f, ti11 = .9510565162951535721f;
            const float tr12 = -.8090169943749474241f, ti12 = .5877852522924731292f;
            float C(int a, int b, int c) => input[a + width * (b + groups * c)];
            void H(int a, int b, int c, float value) => output[a + width * (b + 5 * c)] = value;
            for (int k = 0; k < groups; ++k)
            {
                float cr2 = C(0, k, 4) + C(0, k, 1), ci5 = C(0, k, 4) - C(0, k, 1);
                float cr3 = C(0, k, 3) + C(0, k, 2), ci4 = C(0, k, 3) - C(0, k, 2);
                H(0, 0, k, C(0, k, 0) + cr2 + cr3);
                H(width - 1, 1, k, Fma(tr12, cr3, Fma(tr11, cr2, C(0, k, 0))));
                H(0, 2, k, Fma(ti11, ci5, ti12 * ci4));
                H(width - 1, 3, k, Fma(tr11, cr3, Fma(tr12, cr2, C(0, k, 0))));
                H(0, 4, k, Fma(ti12, ci5, -(ti11 * ci4)));
            }
            for (int k = 0; k < groups; ++k)
            for (int i = 2; i < width; i += 2)
            {
                int opposite = width - i;
                Multiply(w, i - 2, C(i - 1, k, 1), C(i, k, 1), out float dr2, out float di2);
                Multiply(w, width - 1 + i - 2, C(i - 1, k, 2), C(i, k, 2), out float dr3, out float di3);
                Multiply(w, 2 * (width - 1) + i - 2, C(i - 1, k, 3), C(i, k, 3), out float dr4, out float di4);
                Multiply(w, 3 * (width - 1) + i - 2, C(i - 1, k, 4), C(i, k, 4), out float dr5, out float di5);
                Rearrange(ref dr2, ref di2, ref dr5, ref di5); Rearrange(ref dr3, ref di3, ref dr4, ref di4);
                H(i - 1, 0, k, C(i - 1, k, 0) + dr2 + dr3); H(i, 0, k, C(i, k, 0) + di2 + di3);
                float tr2 = Fma(tr12, dr3, Fma(tr11, dr2, C(i - 1, k, 0))), ti2 = Fma(tr12, di3, Fma(tr11, di2, C(i, k, 0)));
                float tr3 = Fma(tr11, dr3, Fma(tr12, dr2, C(i - 1, k, 0))), ti3 = Fma(tr11, di3, Fma(tr12, di2, C(i, k, 0)));
                float tr5 = Fma(ti11, dr5, ti12 * dr4), ti5 = Fma(ti11, di5, ti12 * di4);
                float tr4 = Fma(ti12, dr5, -(ti11 * dr4)), ti4 = Fma(ti12, di5, -(ti11 * di4));
                H(i - 1, 2, k, tr2 + tr5); H(opposite - 1, 1, k, tr2 - tr5);
                H(i, 2, k, ti5 + ti2); H(opposite, 1, k, ti5 - ti2);
                H(i - 1, 4, k, tr3 + tr4); H(opposite - 1, 3, k, tr3 - tr4);
                H(i, 4, k, ti4 + ti3); H(opposite, 3, k, ti4 - ti3);
            }
        }
    }
}
