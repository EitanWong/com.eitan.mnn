using System;
using System.IO;
using System.Threading;

namespace MNN.Unity
{
    public sealed partial class MNNFullSubNet
    {
        private const int BlockFrames = 32;
        private static readonly string[] BlockOutputs = {"projected", "hidden_out", "cell_out"};

        // The primitive recurrent graphs have fixed shapes. Normalization still
        // covers the complete utterance, including the original two lookahead zeros.
        private float[] PredictMaskGpu(float[] magnitude, int frames, CancellationToken token)
        {
            int paddedFrames = frames + 2;
            var frequencySums = new double[FrequencyBins];
            double sum = 0;
            for (int bin = 0; bin < FrequencyBins; ++bin)
            {
                for (int frame = 0; frame < frames; ++frame) frequencySums[bin] += magnitude[bin * frames + frame];
                sum += frequencySums[bin];
            }
            float fullbandScale = (float)(sum / (FrequencyBins * paddedFrames)) + 1e-5f;
            var fullband = RunFullband(magnitude, frames, paddedFrames, fullbandScale, token);
            double subbandSum = 0;
            for (int bin = 0; bin < FrequencyBins; ++bin)
            {
                for (int neighbor = -15; neighbor <= 15; ++neighbor) subbandSum += frequencySums[ReflectFrequency(bin + neighbor)];
                for (int frame = 0; frame < paddedFrames; ++frame) subbandSum += fullband[bin * paddedFrames + frame];
            }
            float subbandScale = (float)(subbandSum / (FrequencyBins * 32 * paddedFrames)) + 1e-5f;
            return RunSubband(magnitude, fullband, frames, paddedFrames, subbandScale, token);
        }

        private float[] RunFullband(float[] magnitude, int frames, int paddedFrames, float scale, CancellationToken token)
        {
            var input = new float[BlockFrames * FrequencyBins];
            var hidden = new float[2 * 512];
            var cell = new float[hidden.Length];
            var output = new float[FrequencyBins * paddedFrames];
            for (int start = 0; start < paddedFrames; start += BlockFrames)
            {
                token.ThrowIfCancellationRequested();
                Array.Clear(input, 0, input.Length);
                int count = Math.Min(BlockFrames, paddedFrames - start);
                for (int frame = 0; frame < count && start + frame < frames; ++frame)
                for (int bin = 0; bin < FrequencyBins; ++bin)
                    input[frame * FrequencyBins + bin] = magnitude[bin * frames + start + frame] / scale;
                var result = RunBlock(_fullbandGraph, input, hidden, cell, 1, FrequencyBins, 512);
                hidden = result[1].Values;
                cell = result[2].Values;
                var projected = result[0].Values;
                for (int frame = 0; frame < count; ++frame)
                for (int bin = 0; bin < FrequencyBins; ++bin)
                    output[bin * paddedFrames + start + frame] = projected[frame * FrequencyBins + bin];
            }
            return output;
        }

        private float[] RunSubband(float[] magnitude, float[] fullband, int frames, int paddedFrames, float scale, CancellationToken token)
        {
            var input = new float[FrequencyBins * BlockFrames * 32];
            var hidden = new float[2 * FrequencyBins * 384];
            var cell = new float[hidden.Length];
            var output = new float[2 * FrequencyBins * frames];
            for (int start = 0; start < paddedFrames; start += BlockFrames)
            {
                token.ThrowIfCancellationRequested();
                Array.Clear(input, 0, input.Length);
                int count = Math.Min(BlockFrames, paddedFrames - start);
                for (int bin = 0; bin < FrequencyBins; ++bin)
                for (int frame = 0; frame < count; ++frame)
                {
                    int time = start + frame, offset = (bin * BlockFrames + frame) * 32;
                    if (time < frames)
                        for (int neighbor = -15; neighbor <= 15; ++neighbor)
                            input[offset + neighbor + 15] = magnitude[ReflectFrequency(bin + neighbor) * frames + time] / scale;
                    input[offset + 31] = fullband[bin * paddedFrames + time] / scale;
                }
                var result = RunBlock(_subbandGraph, input, hidden, cell, FrequencyBins, 32, 384);
                hidden = result[1].Values;
                cell = result[2].Values;
                var projected = result[0].Values;
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

        private static MNNGenerationGraph.Output[] RunBlock(MNNGenerationGraph graph, float[] input, float[] hidden, float[] cell, int batch, int features, int hiddenSize)
        {
            var result = graph.RunOutputs(BlockOutputs,
                new MNNGenerationGraph.Input("features", input, batch, BlockFrames, features),
                new MNNGenerationGraph.Input("hidden", hidden, 2, batch, hiddenSize),
                new MNNGenerationGraph.Input("cell", cell, 2, batch, hiddenSize));
            if (result[0].Values.Length != batch * BlockFrames * (batch == 1 ? FrequencyBins : 2) ||
                result[1].Values.Length != hidden.Length || result[2].Values.Length != cell.Length)
                throw new InvalidDataException("Unexpected FullSubNet recurrent block output.");
            return result;
        }

        private static int ReflectFrequency(int bin) => bin < 0 ? -bin : bin >= FrequencyBins ? 2 * FrequencyBins - 2 - bin : bin;
    }
}
