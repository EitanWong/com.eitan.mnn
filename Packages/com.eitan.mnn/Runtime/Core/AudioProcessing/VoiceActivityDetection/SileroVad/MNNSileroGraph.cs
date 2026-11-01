using System;
using System.IO;
using System.Linq;

namespace MNN.Unity
{
    // The pinned v5 graph has an If subgraph: use official Module execution.
    // Pack speech/context/state into one input, and probability/state into one output.
    internal static class MNNSileroGraph
    {
        internal const string Input = "mnn_unity_silero_input", Output = "mnn_unity_silero_output";

        internal static byte[] Prepare(byte[] source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var r = new MNNSerializedGraph.Reader(source);
            int net = r.Int(0), names = r.Follow(r.Field(net, 7)), count = r.Count(names);
            var strings = Enumerable.Range(0, count).Select(i => r.Follow(names + 4 + 4 * i)).ToArray();
            var text = strings.Select(r.Text).ToArray();
            if (text.Length != 7 || text.Distinct().Count() != 7 || text.Contains(Input))
                throw new InvalidDataException("Unexpected Silero tensor names.");
            int speech = Array.IndexOf(text, "input"), state = Array.IndexOf(text, "state"), rate = Array.IndexOf(text, "sr"),
                probability = Array.IndexOf(text, "output"), nextState = Array.IndexOf(text, "stateN");
            if (new[] {speech, state, rate, probability, nextState}.Any(i => i < 0))
                throw new InvalidDataException("Missing Silero tensors.");
            int ops = r.Follow(r.Field(net, 3));
            var original = Enumerable.Range(0, r.Count(ops)).Select(i => r.Follow(ops + 4 + 4 * i)).ToArray();
            var inputs = original.Where(o => r.Int(r.Field(o, 5)) == 34).ToArray();
            if (inputs.Length != 3 || !inputs.SelectMany(o => r.Integers(r.Field(o, 4))).OrderBy(i => i)
                .SequenceEqual(new[] {speech, state, rate}.OrderBy(i => i)))
                throw new InvalidDataException("Unexpected Silero input producers.");
            original = original.Except(inputs).ToArray();
            var b = new MNNSerializedGraph.Prefix();
            var preserved = Enumerable.Range(0, 12).Where(i => r.Field(net, i) != 0).ToArray();
            int root = b.Table(12, preserved);
            b.Int(0, root);
            foreach (int slot in preserved)
            {
                if (slot == 3 || slot == 4 || slot == 7 || slot == 8) continue;
                int from = r.Field(net, slot), to = root + 4 + slot * 4;
                if (slot == 5 || slot == 6 || slot == 9) b.Int(to, source[from]);
                else b.SourcePointer(to, r.Follow(from));
            }
            int vector = b.Vector(new int[original.Length + 8]);
            b.Pointer(root + 16, vector);
            int packed = count, speechFlat = count + 1, stateFlat = count + 2,
                probabilityFlat = count + 3, nextStateFlat = count + 4, output = count + 5;
            int op = b.Op(Input, 34, 21, Array.Empty<int>(), new[] {packed});
            b.Pointer(vector + 4, op);
            int parameter = b.Table(3, 0, 1, 2);
            b.Pointer(op + 12, parameter);
            b.Int(parameter + 8, 1); // DT_FLOAT
            b.Int(parameter + 12, 1); // NCHW
            b.Pointer(parameter + 4, b.Vector(new[] {832}));

            op = b.Op("silero_split", 83, 61, new[] {packed}, new[] {speechFlat, stateFlat});
            b.Pointer(vector + 8, op);
            parameter = b.Table(3, 0, 1); // Slice: axis 0, one split at 576.
            b.Pointer(op + 12, parameter);
            b.Pointer(parameter + 8, b.Vector(new[] {576}));
            b.Pointer(vector + 12, Reshape(b, "silero_speech", speechFlat, speech, 1, 576));
            b.Pointer(vector + 16, Reshape(b, "silero_state", stateFlat, state, 2, 1, 128));
            op = b.Op("sr", 11, 7, Array.Empty<int>(), new[] {rate});
            b.Pointer(vector + 20, op);
            parameter = b.Table(8, 0, 2, 5);
            b.Pointer(op + 12, parameter);
            b.Int(parameter + 12, 3); // DT_INT32, the converter's sample-rate type.
            b.Pointer(parameter + 4, b.Vector(Array.Empty<int>()));
            b.Pointer(parameter + 24, b.Vector(new[] {16000}));
            for (int i = 0; i < original.Length; ++i) b.SourcePointer(vector + 4 + 4 * (i + 5), original[i]);
            b.Pointer(vector + 4 + 4 * (original.Length + 5), Reshape(b, "silero_probability", probability, probabilityFlat, 1));
            b.Pointer(vector + 4 + 4 * (original.Length + 6), Reshape(b, "silero_next_state", nextState, nextStateFlat, 256));
            op = b.Op(Output, 10, 4, new[] {probabilityFlat, nextStateFlat}, new[] {output});
            b.Pointer(vector + 4 + 4 * (original.Length + 7), op);
            b.Pointer(op + 12, b.Table(1, 0)); // Concat axis 0.

            var extraNames = new[] {Input, "silero_speech_flat", "silero_state_flat", "silero_probability_flat", "silero_next_state_flat", Output};
            int nameVector = b.Vector(new int[count + extraNames.Length]);
            b.Pointer(root + 32, nameVector);
            b.Int(root + 36, count + extraNames.Length);
            for (int i = 0; i < count; ++i) b.SourcePointer(nameVector + 4 + 4 * i, strings[i]);
            for (int i = 0; i < extraNames.Length; ++i) b.Pointer(nameVector + 4 + 4 * (i + count), b.String(extraNames[i]));
            int outputNames = b.Vector(new int[1]);
            b.Pointer(root + 20, outputNames);
            b.Pointer(outputNames + 4, b.String(Output));
            return b.Finish(source);
        }

        private static int Reshape(MNNSerializedGraph.Prefix builder, string name, int input, int output, params int[] dimensions)
        {
            int op = builder.Op(name, 73, 55, new[] {input}, new[] {output});
            int parameter = builder.Table(2, 0, 1);
            builder.Pointer(op + 12, parameter);
            builder.Int(parameter + 8, 1); // NCHW
            builder.Pointer(parameter + 4, builder.Vector(dimensions));
            return op;
        }
    }
}
