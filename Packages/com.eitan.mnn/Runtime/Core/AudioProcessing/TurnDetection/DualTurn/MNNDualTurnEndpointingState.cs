using System;
using System.IO;
using System.Linq;

namespace MNN.Unity
{
    internal static class MNNDualTurnEndpointingState
    {
        internal static readonly string[] OutputNames =
        {
            "eot", "vad", "fvad", "audio_ctx2", "pk2", "pv2", "tf_hist2", "h2", "c2", "pos2"
        };
        private static readonly string[] Names = { "audio_ctx", "pk", "pv", "tf_hist", "h", "c", "pos" };

        internal static MNNGenerationGraph.Input[] Initial()
        {
            return new[]
            {
                new MNNGenerationGraph.Input("audio_ctx", Array.Empty<float>(), 2, 0),
                new MNNGenerationGraph.Input("pk", Array.Empty<float>(), 8, 2, 8, 0, 64),
                new MNNGenerationGraph.Input("pv", Array.Empty<float>(), 8, 2, 8, 0, 64),
                new MNNGenerationGraph.Input("tf_hist", new float[2 * 260 * 512], 2, 260, 512),
                new MNNGenerationGraph.Input("h", new float[2 * 256], 2, 1, 256),
                new MNNGenerationGraph.Input("c", new float[2 * 256], 2, 1, 256),
                new MNNGenerationGraph.Input("pos", new int[1], 1)
            };
        }

        internal static MNNGenerationGraph.Input[] Next(MNNGenerationGraph.Output[] outputs, long tick)
        {
            if (outputs.Length != OutputNames.Length)
                throw new InvalidDataException("DualTurn must return three predictions and seven states.");
            int context = (int)Math.Min(tick, 4) * MNNDualTurnEndpointing.TickSamples;
            int frames = (int)Math.Min(tick * 2, 250);
            RequireFloat(outputs[3], "audio_ctx2", 2, context);
            RequireFloat(outputs[4], "pk2", 8, 2, 8, frames, 64);
            RequireFloat(outputs[5], "pv2", 8, 2, 8, frames, 64);
            RequireFloat(outputs[6], "tf_hist2", 2, 260, 512);
            RequireFloat(outputs[7], "h2", 2, 1, 256);
            RequireFloat(outputs[8], "c2", 2, 1, 256);
            if (!outputs[9].Shape.SequenceEqual(new[] { 1 }) || Position(outputs[9].Data) != tick * 2)
                throw new InvalidDataException("DualTurn position state must advance by exactly two.");
            var result = new MNNGenerationGraph.Input[Names.Length];
            for (int i = 0; i < result.Length; ++i)
                result[i] = outputs[i + 3].AsInput(Names[i]);
            return result;
        }

        internal static float[] RequireFloat(MNNGenerationGraph.Output output, string name, params int[] shape)
        {
            if (!(output.Data is float[] values) || !output.Shape.SequenceEqual(shape))
                throw new InvalidDataException("Invalid DualTurn tensor: " + name);
            return values;
        }

        private static long Position(Array values)
        {
            if (values is int[] integers && integers.Length == 1)
                return integers[0];
            if (values is long[] longs && longs.Length == 1)
                return longs[0];
            throw new InvalidDataException("DualTurn position must retain an integer tensor type.");
        }
    }
}
