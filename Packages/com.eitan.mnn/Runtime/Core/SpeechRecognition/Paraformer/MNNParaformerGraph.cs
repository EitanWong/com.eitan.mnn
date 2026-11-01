using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MNN.Unity
{
    // Prepare the pinned Paraformer export through the public MNN FlatBuffer schema.
    // Add its omitted length producer and pack enc/alphas into one Module output.
    // Original graph bytes and native MNN source are never modified.
    internal static class MNNParaformerGraph
    {
        internal const string EncoderOutput = "mnn_unity_paraformer_encoder";

        internal static byte[] PrepareEncoder(byte[] source, int frames)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (frames != 20) throw new ArgumentOutOfRangeException(nameof(frames));
            var r = new MNNSerializedGraph.Reader(source);
            int net = r.Int(0), names = r.Follow(r.Field(net, 7));
            int count = r.Count(names), lengthId = -1, encodedId = -1, alphaId = -1;
            var strings = new int[count];
            for (int i = 0; i < count; ++i)
            {
                strings[i] = r.Follow(names + 4 + i * 4);
                string name = r.Text(strings[i]);
                if (name == "enc_len") lengthId = i;
                if (name == "enc") encodedId = i;
                if (name == "alphas") alphaId = i;
                if (name == EncoderOutput) throw new InvalidDataException("Paraformer graph is already prepared.");
            }
            if (lengthId != 0 || encodedId < 0 || alphaId < 0)
                throw new InvalidDataException("Unexpected Paraformer encoder names.");
            int ops = r.Follow(r.Field(net, 3)), opCount = r.Count(ops);
            var operations = new int[opCount];
            var produced = new HashSet<int>();
            var consumed = new HashSet<int>();
            for (int i = 0; i < opCount; ++i)
            {
                int op = r.Follow(ops + 4 + i * 4);
                operations[i] = op;
                foreach (int id in r.Integers(r.Field(op, 4)))
                    if (id < 0 || id >= count || !produced.Add(id))
                        throw new InvalidDataException("Invalid Paraformer producer.");
                foreach (int id in r.Integers(r.Field(op, 0)))
                {
                    if (id < 0 || id >= count) throw new InvalidDataException("Invalid Paraformer input index.");
                    consumed.Add(id);
                }
            }
            if (produced.Contains(lengthId) || !produced.Contains(encodedId) || !produced.Contains(alphaId) ||
                !consumed.Except(produced).SequenceEqual(new[] {lengthId}))
                throw new InvalidDataException("Unexpected Paraformer external inputs.");

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
            int opVector = b.Vector(new int[opCount + 3]);
            b.Pointer(root + 16, opVector);
            for (int i = 0; i < opCount; ++i)
                b.SourcePointer(opVector + 8 + i * 4, operations[i]);

            // A Const for tensor zero precedes the original graph's consumers.
            int length = b.Op("enc_len", 11, 7, Array.Empty<int>(), new[] {lengthId});
            b.Pointer(opVector + 4, length);
            int blob = b.Table(8, 0, 2, 5);
            b.Pointer(length + 12, blob);
            b.Int(blob + 12, 3); // DataType::DT_INT32
            b.Pointer(blob + 4, b.Vector(new[] {1}));
            b.Pointer(blob + 24, b.Vector(new[] {frames}));

            int reshape = b.Op("mnn_unity_paraformer_alpha", 73, 55, new[] {alphaId}, new[] {count});
            b.Pointer(opVector + 4 + (opCount + 1) * 4, reshape);
            int dimensions = b.Table(2, 0);
            b.Pointer(reshape + 12, dimensions);
            b.Pointer(dimensions + 4, b.Vector(new[] {1, frames, 1}));
            int concat = b.Op(EncoderOutput, 10, 4, new[] {encodedId, count}, new[] {count + 1});
            b.Pointer(opVector + 4 + (opCount + 2) * 4, concat);
            int axis = b.Table(1, 0);
            b.Pointer(concat + 12, axis);
            b.Int(axis + 4, 2);

            int tensorNames = b.Vector(new int[count + 2]);
            b.Pointer(root + 32, tensorNames);
            for (int i = 0; i < count; ++i) b.SourcePointer(tensorNames + 4 + i * 4, strings[i]);
            b.Pointer(tensorNames + 4 + count * 4, b.String("mnn_unity_paraformer_alpha"));
            int output = b.String(EncoderOutput);
            b.Pointer(tensorNames + 4 + (count + 1) * 4, output);
            // FlatBuffer offsets point forward, so create the output string after its vector.
            int outputNames = b.Vector(new int[1]);
            b.Pointer(root + 20, outputNames);
            b.Pointer(outputNames + 4, b.String(EncoderOutput));
            b.Int(root + 36, count + 2);
            return b.Finish(source);
        }

    }
}
