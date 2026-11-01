using System;
using System.Threading;

namespace MNN.Unity
{
    internal sealed class MNNResembleEnhancerDsp
    {
        private readonly MNNSourceRealFft _melAnalysis = new MNNSourceRealFft(2048);
        private readonly MNNSourceRealFft _denoiserAnalysis = new MNNSourceRealFft(1680);
        private readonly MNNResembleEnhancerConfig _config;
        private readonly MNNResembleDenoiserSpectral _synthesis;

        internal MNNResembleEnhancerDsp(MNNResembleEnhancerConfig config)
        {
            _config = config;
            _synthesis = new MNNResembleDenoiserSpectral(config.analysis_window);
        }

        internal float[] Prepare(float[] pcm, out float scale)
        {
            float peak = 0;
            foreach (float value in pcm) peak = Math.Max(peak, Math.Abs(value));
            scale = Math.Max(peak, 1e-7f);
            var padded = new float[pcm.Length + 441];
            for (int i = 0; i < pcm.Length; ++i) padded[i] = pcm[i] / scale;
            Normalize(padded);
            return padded;
        }

        private static void Normalize(float[] pcm)
        {
            float peak = 0;
            foreach (float value in pcm) peak = Math.Max(peak, Math.Abs(value));
            float denominator = peak + 1e-7f;
            for (int i = 0; i < pcm.Length; ++i) pcm[i] /= denominator;
        }

        internal float[] Mel(float[] pcm, CancellationToken token)
        {
            int frames = pcm.Length / 420;
            var emphasized = new float[pcm.Length];
            for (int i = 0; i < pcm.Length; ++i)
                emphasized[i] = pcm[i] - .97f * (i == 0 ? 0f : pcm[i - 1]);
            var work = new float[2048];
            var real = new float[1025];
            var imaginary = new float[1025];
            var magnitudes = new float[1025];
            var output = new float[128 * frames];
            for (int frame = 0; frame < frames; ++frame)
            {
                token.ThrowIfCancellationRequested();
                for (int i = 0; i < 2048; ++i)
                {
                    int index = frame * 420 + i - 1024;
                    work[i] = index < 0 || index >= pcm.Length ? 0 : emphasized[index] * _config.mel_window[i];
                }
                _melAnalysis.Forward(work, real, imaginary);
                for (int bin = 0; bin < 1025; ++bin)
                    magnitudes[bin] = (float)Math.Sqrt((double)real[bin] * real[bin] + (double)imaginary[bin] * imaginary[bin]);
                for (int mel = 0; mel < 128; ++mel)
                {
                    double sum = 0;
                    for (int bin = 0; bin < 1025; ++bin)
                        sum += (double)magnitudes[bin] * _config.mel_filterbank[bin * 128 + mel];
                    float value = ((float)Math.Log10(Math.Max(.0001f, (float)sum)) * 20f + 80f) / 95f;
                    output[mel * frames + frame] = (value - _config.running_mean) / _config.running_std;
                }
            }
            return output;
        }

        internal float[] DenoiserFeatures(float[] padded, out int frames, CancellationToken token)
        {
            var input = (float[])padded.Clone();
            Normalize(input);
            frames = input.Length / 420;
            int plane = 841 * frames;
            var output = new float[3 * plane];
            var windowed = new float[1680];
            var real = new float[841];
            var imaginary = new float[841];
            for (int frame = 0; frame < frames; ++frame)
            {
                token.ThrowIfCancellationRequested();
                for (int i = 0; i < 1680; ++i)
                {
                    int index = frame * 420 + i - 840;
                    if (index < 0) index = -index;
                    if (index >= input.Length) index = 2 * input.Length - 2 - index;
                    windowed[i] = input[index] * _config.analysis_window[i];
                }
                _denoiserAnalysis.Forward(windowed, real, imaginary);
                for (int bin = 0; bin < 841; ++bin)
                {
                    int index = bin * frames + frame;
                    float phase = MathF.Atan2(imaginary[bin], real[bin]);
                    output[index] = (float)Math.Sqrt((double)real[bin] * real[bin] + (double)imaginary[bin] * imaginary[bin]);
                    output[plane + index] = MNNResembleEnhancerPhase.Cos(phase);
                    output[2 * plane + index] = MNNResembleEnhancerPhase.Sin(phase);
                }
            }
            return output;
        }

        internal float[] DenoiserWave(float[] spectrum, int frames, int length, CancellationToken token)
        {
            var signal = _synthesis.Synthesize(spectrum, frames, frames * 420, 1f, token);
            var output = new float[length];
            Array.Copy(signal, output, Math.Min(length, signal.Length));
            return output;
        }

        internal static void NormalizeCondition(float[] input, int frames)
        {
            for (int channel = 0; channel < 128; ++channel)
            {
                int start = channel * frames;
                double sum = 0;
                for (int i = 0; i < frames; ++i) sum += input[start + i];
                float mean = (float)(sum / frames);
                double squares = 0;
                for (int i = 0; i < frames; ++i)
                {
                    float centered = input[start + i] - mean;
                    squares += centered * centered;
                }
                float variance = (float)squares / frames;
                float alpha = (float)(1.0 / Math.Sqrt(variance + .00001));
                float beta = -mean * alpha;
                for (int i = 0; i < frames; ++i) input[start + i] = input[start + i] * alpha + beta;
            }
        }
    }
}
