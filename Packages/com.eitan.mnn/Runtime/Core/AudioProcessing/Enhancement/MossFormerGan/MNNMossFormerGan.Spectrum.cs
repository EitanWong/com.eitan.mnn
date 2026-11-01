using System;
using System.Threading;

namespace MNN.Unity
{
    public sealed partial class MNNMossFormerGan
    {
        private float[] Analyze(float[] pcm, int paddedCount, int frames, float gain, CancellationToken token)
        {
            int plane = frames * FrequencyBins;
            var features = new float[2 * plane];
            var input = new float[FftSize];
            var real = new float[FrequencyBins]; var imaginary = new float[FrequencyBins];
            for (int frame = 0; frame < frames; ++frame)
            {
                token.ThrowIfCancellationRequested();
                for (int n = 0; n < FftSize; ++n)
                {
                    int index = frame * HopSize + n - FftSize / 2;
                    if (index < 0) index = -index;
                    if (index >= paddedCount) index = 2 * paddedCount - 2 - index;
                    float value = pcm[index < pcm.Length ? index : index - pcm.Length] * gain;
                    input[n] = value * _window[n];
                }
                _analysisFft.Forward(input, real, imaginary);
                for (int bin = 0; bin < FrequencyBins; ++bin)
                {
                    float re = real[bin], im = imaginary[bin];
                    float magnitude = (float)Math.Pow((float)Math.Sqrt((double)re * re + (double)im * im), .3f);
                    float phase = (float)Math.Atan2(im, re);
                    int index = frame * FrequencyBins + bin;
                    features[index] = magnitude * (float)Math.Cos(phase);
                    features[plane + index] = magnitude * (float)Math.Sin(phase);
                }
            }
            return features;
        }

        private float[] Synthesize(float[] features, float[] mask, float[] residual, int frames, int count, double gain, CancellationToken token)
        {
            int plane = frames * FrequencyBins;
            var accumulation = new double[(frames - 1) * HopSize + FftSize];
            var normalization = new double[accumulation.Length];
            var real = new double[FftSize]; var imaginary = new double[FftSize];
            for (int frame = 0; frame < frames; ++frame)
            {
                token.ThrowIfCancellationRequested();
                for (int bin = 0; bin < FrequencyBins; ++bin)
                {
                    int index = frame * FrequencyBins + bin;
                    float re = features[index], im = features[plane + index];
                    float magnitude = (float)Math.Sqrt(re * re + im * im);
                    float phase = (float)Math.Atan2(im, re);
                    float estimated = mask[index] * magnitude;
                    float outReal = estimated * (float)Math.Cos(phase) + residual[index];
                    float outImaginary = estimated * (float)Math.Sin(phase) + residual[plane + index];
                    double power = Math.Pow(Math.Sqrt((double)outReal * outReal + (double)outImaginary * outImaginary), 1 / .3);
                    double angle = Math.Atan2(outImaginary, outReal);
                    real[bin] = power * Math.Cos(angle); imaginary[bin] = power * Math.Sin(angle);
                    if (bin == 0 || bin == FrequencyBins - 1) continue;
                    real[FftSize - bin] = real[bin]; imaginary[FftSize - bin] = -imaginary[bin];
                }
                imaginary[0] = imaginary[FftSize / 2] = 0;
                _fft.Transform(real, imaginary, true);
                for (int n = 0; n < FftSize; ++n)
                {
                    int index = frame * HopSize + n;
                    accumulation[index] += real[n] * _window[n];
                    normalization[index] += (double)_window[n] * _window[n];
                }
            }
            var output = new float[count];
            for (int i = 0; i < count; ++i)
            {
                int index = i + FftSize / 2;
                output[i] = (float)(accumulation[index] / normalization[index] / gain);
            }
            return output;
        }
    }
}
