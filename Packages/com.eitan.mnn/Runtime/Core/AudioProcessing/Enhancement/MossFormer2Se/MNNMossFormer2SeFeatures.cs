using System;
using System.Threading;

namespace MNN.Unity
{
    internal sealed class MNNMossFormer2SeFeatures
    {
        private readonly MNNMossFormer2SeConfig _config;
        private readonly MNNFftPlan _fft = new MNNFftPlan(2048);
        private readonly int[] _filterStarts = new int[60], _filterEnds = new int[60];
        internal MNNMossFormer2SeFeatures(MNNMossFormer2SeConfig config)
        {
            _config = config;
            for (int mel = 0; mel < 60; ++mel)
            {
                int start = 0, end = 1024;
                while (start < end && config.mel_filters[mel * 1024 + start] == 0) ++start;
                while (end > start && config.mel_filters[mel * 1024 + end - 1] == 0) --end;
                _filterStarts[mel] = start; _filterEnds[mel] = end;
            }
        }

        internal float[] Extract(float[] pcm, out int frames, CancellationToken token)
        {
            frames = 1 + (pcm.Length - 1920) / 384;
            var fbank = new float[frames * 60];
            var random = new MNNSeededNormal((uint)_config.dither_seed);
            var row = new float[1920]; var real = new double[2048]; var imaginary = new double[2048];
            var powers = new float[1024];
            for (int frame = 0; frame < frames; ++frame)
            {
                token.ThrowIfCancellationRequested();
                random.Fill(row);
                for (int i = 0; i < row.Length; ++i) row[i] = pcm[frame * 384 + i] * 32768f + row[i];
                float mean = MNNFloatCascadeSum.Sum(row, 0, row.Length) / row.Length;
                for (int i = 0; i < row.Length; ++i) row[i] -= mean;
                Array.Clear(real, 0, real.Length); Array.Clear(imaginary, 0, imaginary.Length);
                for (int i = 0; i < row.Length; ++i)
                    real[i] = (row[i] - .97f * row[Math.Max(0, i - 1)]) * _config.analysis_window[i];
                _fft.Transform(real, imaginary);
                for (int k = 0; k < powers.Length; ++k)
                {
                    float re = (float)real[k], im = (float)imaginary[k];
                    float magnitude = (float)Math.Sqrt((double)re * re + (double)im * im);
                    powers[k] = magnitude * magnitude;
                }
                for (int mel = 0; mel < 60; ++mel)
                {
                    double sum = 0;
                    for (int k = _filterStarts[mel]; k < _filterEnds[mel]; ++k)
                        sum += (double)powers[k] * _config.mel_filters[mel * 1024 + k];
                    fbank[frame * 60 + mel] = (float)Math.Log(Math.Max(1.1920928955078125e-7, sum));
                }
            }
            var delta = Deltas(fbank, frames); var delta2 = Deltas(delta, frames);
            var features = new float[frames * 180];
            for (int frame = 0; frame < frames; ++frame)
            {
                Array.Copy(fbank, frame * 60, features, frame * 180, 60);
                Array.Copy(delta, frame * 60, features, frame * 180 + 60, 60);
                Array.Copy(delta2, frame * 60, features, frame * 180 + 120, 60);
            }
            return features;
        }

        private static float[] Deltas(float[] input, int frames)
        {
            var result = new float[input.Length];
            for (int f = 0; f < frames; ++f)
            for (int m = 0; m < 60; ++m)
            {
                float sum = 0;
                for (int offset = -2; offset <= 2; ++offset)
                    sum += input[Math.Max(0, Math.Min(frames - 1, f + offset)) * 60 + m] * offset;
                result[f * 60 + m] = sum / 10;
            }
            return result;
        }
    }
}
