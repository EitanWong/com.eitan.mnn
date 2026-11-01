using System;
using System.Runtime.InteropServices;

namespace MNN.Unity
{
    /// <summary>Reusable in-place complex FFT. Radix-2 is used for power-of-two lengths;
    /// lengths composed of factors 2, 3, 5 and 7 use an allocation-free Bluestein convolution.
    /// An optional Burst adapter accelerates the radix-2 kernel.</summary>
    public sealed class MNNFftPlan
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal unsafe delegate void Kernel(double* real, double* imaginary, int* reversed, double* cosine, double* sine, int length, int inverse);
        internal static Kernel BurstKernel;
        private readonly int[] _reversed;
        private readonly double[] _cosine, _sine;
        private readonly bool _bluestein;
        private readonly int _convolutionLength;
        private readonly int[] _convolutionReversed;
        private readonly double[] _convolutionCosine, _convolutionSine;
        private readonly double[] _chirpForwardReal, _chirpForwardImaginary;
        private readonly double[] _chirpInverseReal, _chirpInverseImaginary;
        private readonly double[] _workReal, _workImaginary;
        private readonly double[] _chirpCosine, _chirpSine;
        public int Length { get; }
        public bool PreferBurst { get; set; }
        public static bool IsBurstAvailable => BurstKernel != null;
        public bool UsesBurst => PreferBurst && IsBurstAvailable;
        public MNNFftPlan(int length, bool preferBurst = true)
        {
            if (length < 2 || length > 65536 || !IsSmoothLength(length))
                throw new ArgumentOutOfRangeException(nameof(length), "FFT length must be composed of factors 2, 3, 5 and 7 and be between 2 and 65536.");
            Length = length; PreferBurst = preferBurst;
            _bluestein = (length & (length - 1)) != 0;
            _reversed = _bluestein ? Array.Empty<int>() : new int[length];
            _cosine = _bluestein ? Array.Empty<double>() : new double[length / 2];
            _sine = _bluestein ? Array.Empty<double>() : new double[length / 2];
            if (!_bluestein)
            {
                for (int i = 1; i < length; ++i) _reversed[i] = (_reversed[i >> 1] >> 1) | ((i & 1) != 0 ? length >> 1 : 0);
                for (int i = 0; i < length / 2; ++i)
                {
                    double angle = -2 * Math.PI * i / length;
                    _cosine[i] = Math.Cos(angle); _sine[i] = Math.Sin(angle);
                }
                _convolutionLength = 0;
                _convolutionReversed = null; _convolutionCosine = null; _convolutionSine = null;
                _chirpForwardReal = null; _chirpForwardImaginary = null;
                _chirpInverseReal = null; _chirpInverseImaginary = null;
                _workReal = null; _workImaginary = null;
                return;
            }
            _convolutionLength = NextPowerOfTwo(checked(2 * length - 1));
            _convolutionReversed = new int[_convolutionLength];
            _convolutionCosine = new double[_convolutionLength / 2];
            _convolutionSine = new double[_convolutionLength / 2];
            for (int i = 1; i < _convolutionLength; ++i)
                _convolutionReversed[i] = (_convolutionReversed[i >> 1] >> 1) | ((i & 1) != 0 ? _convolutionLength >> 1 : 0);
            for (int i = 0; i < _convolutionLength / 2; ++i)
            {
                double angle = -2 * Math.PI * i / _convolutionLength;
                _convolutionCosine[i] = Math.Cos(angle); _convolutionSine[i] = Math.Sin(angle);
            }
            _chirpCosine = new double[length]; _chirpSine = new double[length];
            for (int i = 0; i < length; ++i)
            {
                double phase = Math.PI * i * (double)i / length;
                _chirpCosine[i] = Math.Cos(phase); _chirpSine[i] = Math.Sin(phase);
            }
            _chirpForwardReal = BuildChirpSpectrum(-1, out _chirpForwardImaginary);
            _chirpInverseReal = BuildChirpSpectrum(1, out _chirpInverseImaginary);
            _workReal = new double[_convolutionLength]; _workImaginary = new double[_convolutionLength];
        }
        /// <summary>Forward transform is unscaled; inverse divides by Length. Arrays must be distinct.
        /// A plan may be shared between threads using separate input arrays.</summary>
        public unsafe void Transform(double[] real, double[] imaginary, bool inverse = false)
        {
            if (real == null || imaginary == null || real.Length != Length || imaginary.Length != Length || ReferenceEquals(real, imaginary))
                throw new ArgumentException("Supply distinct real/imaginary buffers matching the FFT plan length.");
            if (_bluestein)
            {
                lock (_workReal) TransformBluestein(real, imaginary, inverse);
                return;
            }
            fixed (double* r = real, im = imaginary, cosine = _cosine, sine = _sine)
            fixed (int* reversed = _reversed)
            {
                var burst = PreferBurst ? BurstKernel : null;
                if (burst != null) burst(r, im, reversed, cosine, sine, Length, inverse ? 1 : 0);
                else TransformKernel(r, im, reversed, cosine, sine, Length, inverse ? 1 : 0);
            }
        }

        private static bool IsSmoothLength(int length)
        {
            int value = length;
            while ((value & 1) == 0) value >>= 1;
            while (value % 3 == 0) value /= 3;
            while (value % 5 == 0) value /= 5;
            while (value % 7 == 0) value /= 7;
            return value == 1;
        }

        private static int NextPowerOfTwo(int value)
        {
            int result = 1;
            while (result < value) result <<= 1;
            return result;
        }

        private double[] BuildChirpSpectrum(int sign, out double[] imaginary)
        {
            var real = new double[_convolutionLength];
            imaginary = new double[_convolutionLength];
            for (int m = -(Length - 1); m <= Length - 1; ++m)
            {
                int index = m >= 0 ? m : _convolutionLength + m;
                double phase = -sign * Math.PI * m * (double)m / Length;
                real[index] = Math.Cos(phase);
                imaginary[index] = Math.Sin(phase);
            }
            unsafe
            {
                fixed (double* realPointer = real, imaginaryPointer = imaginary, cosine = _convolutionCosine, sine = _convolutionSine)
                fixed (int* reversed = _convolutionReversed)
                    TransformKernel(realPointer, imaginaryPointer, reversed, cosine, sine, _convolutionLength, 0);
            }
            return real;
        }

        private unsafe void TransformBluestein(double[] real, double[] imaginary, bool inverse)
        {
            int sign = inverse ? 1 : -1;
            double[] spectrumReal = inverse ? _chirpInverseReal : _chirpForwardReal;
            double[] spectrumImaginary = inverse ? _chirpInverseImaginary : _chirpForwardImaginary;
            Array.Clear(_workReal, 0, _workReal.Length);
            Array.Clear(_workImaginary, 0, _workImaginary.Length);
            for (int n = 0; n < Length; ++n)
            {
                double c = _chirpCosine[n], s = sign * _chirpSine[n];
                _workReal[n] = real[n] * c - imaginary[n] * s;
                _workImaginary[n] = real[n] * s + imaginary[n] * c;
            }
            fixed (double* workReal = _workReal, workImaginary = _workImaginary,
                cosine = _convolutionCosine, sine = _convolutionSine)
            fixed (int* reversed = _convolutionReversed)
            {
                var kernel = UsesBurst ? BurstKernel : TransformKernel;
                kernel(workReal, workImaginary, reversed, cosine, sine, _convolutionLength, 0);
                for (int i = 0; i < _convolutionLength; ++i)
                {
                    double r = workReal[i] * spectrumReal[i] - workImaginary[i] * spectrumImaginary[i];
                    double im = workReal[i] * spectrumImaginary[i] + workImaginary[i] * spectrumReal[i];
                    workReal[i] = r; workImaginary[i] = im;
                }
                kernel(workReal, workImaginary, reversed, cosine, sine, _convolutionLength, 1);
            }
            double scale = inverse ? 1.0 / Length : 1.0;
            for (int k = 0; k < Length; ++k)
            {
                double c = _chirpCosine[k], s = sign * _chirpSine[k];
                real[k] = (_workReal[k] * c - _workImaginary[k] * s) * scale;
                imaginary[k] = (_workReal[k] * s + _workImaginary[k] * c) * scale;
            }
        }
        // Shared source for Mono/IL2CPP and the optional Burst compiler. No Unity.Collections,
        // Jobs scheduling, trigonometry, managed objects or allocation in this kernel.
        internal static unsafe void TransformKernel(double* real, double* imaginary, int* reversed, double* cosine, double* sine, int length, int inverse)
        {
            for (int i = 0; i < length; ++i)
            {
                int j = reversed[i];
                if (j <= i) continue;
                double r = real[i], im = imaginary[i];
                real[i] = real[j]; imaginary[i] = imaginary[j]; real[j] = r; imaginary[j] = im;
            }
            for (int width = 2; width <= length; width <<= 1)
            {
                int half = width >> 1, stride = length / width;
                for (int start = 0; start < length; start += width)
                    for (int j = 0; j < half; ++j)
                    {
                        int a = start + j, b = a + half, twiddle = j * stride;
                        double c = cosine[twiddle], s = inverse == 0 ? sine[twiddle] : -sine[twiddle];
                        double r = real[b] * c - imaginary[b] * s, im = real[b] * s + imaginary[b] * c;
                        real[b] = real[a] - r; imaginary[b] = imaginary[a] - im;
                        real[a] += r; imaginary[a] += im;
                    }
            }
            if (inverse != 0)
            {
                double scale = 1.0 / length;
                for (int i = 0; i < length; ++i) { real[i] *= scale; imaginary[i] *= scale; }
            }
        }
    }
}
