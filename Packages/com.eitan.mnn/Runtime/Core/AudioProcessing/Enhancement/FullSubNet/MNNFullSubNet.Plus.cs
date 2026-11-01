using System;
using System.IO;
using System.Threading;

namespace MNN.Unity
{
    public sealed partial class MNNFullSubNet
    {
        private float[] PredictMaskPlus(float[] magnitude, float[] spectrum, int frames, CancellationToken token)
        {
            int plane = FrequencyBins * frames;
            if (spectrum == null || spectrum.Length != 2 * plane)
                throw new NotSupportedException("FullSubNet+ requires magnitude, real and imaginary spectra; use Enhance with normalized PCM.");
            var real = new float[plane]; var imaginary = new float[plane];
            Array.Copy(spectrum, real, plane); Array.Copy(spectrum, plane, imaginary, 0, plane);
            var inputs = new[]
            {
                new MNNGenerationGraph.Input("magnitude", magnitude, 1, 1, FrequencyBins, frames),
                new MNNGenerationGraph.Input("real", real, 1, 1, FrequencyBins, frames),
                new MNNGenerationGraph.Input("imag", imaginary, 1, 1, FrequencyBins, frames)
            };
            if (_graph != null)
            {
                var mask = _graph.RunOutputs(new[] { "crm_mask" }, inputs)[0].Values;
                if (mask.Length != 2 * plane) throw new InvalidDataException("Unexpected FullSubNet+ mask shape.");
                return mask;
            }
            var features = _fullbandGraph.RunOutputs(new[] { "sb_features" }, inputs)[0].Values;
            token.ThrowIfCancellationRequested();
            return RunPlusSubband(features, frames, token);
        }

        private float[] RunPlusSubband(float[] features, int frames, CancellationToken token)
        {
            const int channels = 34;
            int paddedFrames = frames + 2;
            if (features.Length != FrequencyBins * channels * paddedFrames)
                throw new InvalidDataException("Unexpected FullSubNet+ attention/TCN frontend shape.");
            var input = new float[FrequencyBins * BlockFrames * channels];
            var hidden = new float[2 * FrequencyBins * 384]; var cell = new float[hidden.Length];
            var output = new float[2 * FrequencyBins * frames];
            for (int start = 0; start < paddedFrames; start += BlockFrames)
            {
                token.ThrowIfCancellationRequested(); Array.Clear(input, 0, input.Length);
                int count = Math.Min(BlockFrames, paddedFrames - start);
                for (int bin = 0; bin < FrequencyBins; ++bin)
                for (int frame = 0; frame < count; ++frame)
                for (int channel = 0; channel < channels; ++channel)
                    input[(bin * BlockFrames + frame) * channels + channel] = features[(bin * channels + channel) * paddedFrames + start + frame];
                var result = RunBlock(_subbandGraph, input, hidden, cell, FrequencyBins, channels, 384);
                hidden = result[1].Values; cell = result[2].Values; var projected = result[0].Values;
                for (int bin = 0; bin < FrequencyBins; ++bin)
                for (int frame = 0; frame < count; ++frame)
                {
                    int time = start + frame - 2;
                    if (time < 0) continue;
                    int index = (bin * BlockFrames + frame) * 2;
                    output[bin * frames + time] = projected[index];
                    output[(FrequencyBins + bin) * frames + time] = projected[index + 1];
                }
            }
            return output;
        }
    }
}
