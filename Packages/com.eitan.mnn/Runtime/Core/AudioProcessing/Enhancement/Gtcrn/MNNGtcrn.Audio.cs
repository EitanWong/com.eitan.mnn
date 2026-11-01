using System;
using System.Threading;

namespace MNN.Unity
{
    public sealed partial class MNNGtcrn
    {
        private readonly MNNFftPlan _fft = new MNNFftPlan(FrameLength);
        private readonly float[] _window = CreateWindow(), _spectrum = new float[FrequencyBins * 2], _enhanced = new float[FrequencyBins * 2];
        private readonly double[] _real = new double[FrameLength], _imaginary = new double[FrameLength];

        /// <summary>Enhance an entire recording with the official centered sqrt-Hann STFT convention.
        /// Resets recurrent caches before/after the recording and restores the original rate/count.</summary>
        public float[] Enhance(float[] samples, int sampleRate = SampleRate, CancellationToken cancellationToken = default)
        {
            if (samples == null || samples.Length == 0) throw new ArgumentException("Supply nonempty mono PCM.", nameof(samples));
            if (sampleRate < 8000 || sampleRate > 96000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (samples.Length > (long)sampleRate * 300) throw new ArgumentException("Supply at most five minutes of mono PCM.", nameof(samples));
            for (int i = 0; i < samples.Length; ++i)
            {
                if ((i & 16383) == 0) cancellationToken.ThrowIfCancellationRequested();
                if (float.IsNaN(samples[i]) || float.IsInfinity(samples[i]) || Math.Abs(samples[i]) > 1)
                    throw new ArgumentException("Audio must contain finite PCM in [-1,1].", nameof(samples));
            }
            lock (_gate)
            {
                ThrowIfDisposed();
                ResetCore();
                try
                {
                    var pcm = MNNSpeechDsp.Resample(samples, sampleRate, SampleRate, cancellationToken);
                    int count = pcm.Length;
                    if (pcm.Length <= FrameLength / 2) Array.Resize(ref pcm, FrameLength / 2 + 1);
                    var output = EnhanceCentered(pcm, count, cancellationToken);
                    if (sampleRate == SampleRate) return output;
                    output = MNNSpeechDsp.Resample(output, SampleRate, sampleRate, cancellationToken);
                    if (output.Length != samples.Length) Array.Resize(ref output, samples.Length);
                    return output;
                }
                finally { ResetCore(); }
            }
        }

        public float[] EnhanceWaveFile(string path, CancellationToken cancellationToken = default)
        {
            var pcm = MNNWaveAudio.Read(path, out int rate);
            return Enhance(pcm, rate, cancellationToken);
        }

        private float[] EnhanceCentered(float[] pcm, int count, CancellationToken token)
        {
            int frames = pcm.Length / HopLength + 1;
            var accumulation = new double[checked((frames - 1) * HopLength + FrameLength)];
            var normalization = new double[accumulation.Length];
            for (int frame = 0; frame < frames; ++frame)
            {
                token.ThrowIfCancellationRequested();
                int start = frame * HopLength;
                for (int i = 0; i < FrameLength; ++i)
                {
                    _real[i] = pcm[Reflect(start + i - FrameLength / 2, pcm.Length)] * _window[i];
                    _imaginary[i] = 0;
                }
                _fft.Transform(_real, _imaginary);
                for (int bin = 0; bin < FrequencyBins; ++bin)
                {
                    _spectrum[bin * 2] = (float)_real[bin];
                    _spectrum[bin * 2 + 1] = (float)_imaginary[bin];
                }
                RunSpectrum(_spectrum, _enhanced);
                Synthesize(start, accumulation, normalization);
            }
            var output = new float[count];
            for (int i = 0; i < count; ++i)
            {
                int index = i + FrameLength / 2;
                output[i] = (float)(accumulation[index] / normalization[index]);
            }
            return output;
        }

        private void Synthesize(int start, double[] accumulation, double[] normalization)
        {
            for (int bin = 0; bin < FrequencyBins; ++bin)
            {
                _real[bin] = _enhanced[bin * 2];
                _imaginary[bin] = _enhanced[bin * 2 + 1];
                if (bin == 0 || bin == FrameLength / 2) continue;
                _real[FrameLength - bin] = _real[bin];
                _imaginary[FrameLength - bin] = -_imaginary[bin];
            }
            _imaginary[0] = _imaginary[FrameLength / 2] = 0;
            _fft.Transform(_real, _imaginary, true);
            for (int i = 0; i < FrameLength; ++i)
            {
                accumulation[start + i] += _real[i] * _window[i];
                normalization[start + i] += (double)_window[i] * _window[i];
            }
        }

        private static int Reflect(int index, int length) => index < 0 ? -index : index >= length ? 2 * length - 2 - index : index;
        private static float[] CreateWindow()
        {
            var window = new float[FrameLength];
            for (int i = 0; i < FrameLength; ++i) window[i] = (float)Math.Sqrt(.5 - .5 * Math.Cos(2 * Math.PI * i / FrameLength));
            return window;
        }
    }
}
