using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MNN.Unity
{
    // Adapt only the pinned Pocket export using the public FlatBuffer schema.
    // Empty text/sequence concatenation is an identity. The encoder lost two
    // ONNX Shape(start=2,end=3) attributes; restore their one-element slices.
    // Source graph files and native MNN code remain unchanged.
    internal static class MNNPocketGraph
    {
        internal static byte[] Encoder(byte[] source) => SliceShapeOutputs(source, new[]{"val_0", "val_100"});

        private static byte[] SliceShapeOutputs(byte[] source, string[] targets)
        {
            var d = new MNNSerializedGraph.Reader(source); var b = new MNNSerializedGraph.Prefix();
            int root = d.Int(0), names = d.Follow(d.Field(root, 7)), count = d.Count(names), ops = d.Follow(d.Field(root, 3));
            var newNames = new List<string>();
            int[] fields = Enumerable.Range(0, 12).Where(slot => slot == 8 || d.Field(root, slot) != 0).ToArray();
            int net = b.Table(12, fields); b.Pointer(0, net);
            foreach (int slot in fields)
            {
                if (slot == 3 || slot == 7 || slot == 8) continue;
                int original = d.Field(root, slot), field = net + 4 + 4 * slot;
                if (slot == 5 || slot == 6 || slot == 9) b.Int(field, source[original]);
                else b.SourcePointer(field, d.Follow(original));
            }
            int newOps = b.Vector(new int[d.Count(ops) + targets.Length * 3]); b.Pointer(net + 16, newOps); int at = 0;
            int NewTensor(string name) { newNames.Add(name); return count + newNames.Count - 1; }
            void Constant(string name, int value, out int tensor)
            {
                tensor = NewTensor(name); int op = b.Op(name, 11, 7, Array.Empty<int>(), new[]{tensor});
                b.Pointer(newOps + 4 + 4 * at++, op);
                int blob = b.Table(8, 0, 1, 2, 5); b.Pointer(op + 12, blob);
                b.Pointer(blob + 4, b.Vector(new[]{1})); b.Int(blob + 8, 0); b.Int(blob + 12, 3);
                b.Pointer(blob + 24, b.Vector(new[]{value}));
            }
            int matched = 0;
            for (int i = 0; i < d.Count(ops); ++i)
            {
                int op = d.Follow(ops + 4 + 4 * i), nf = d.Field(op, 3);
                string name = nf == 0 ? "" : d.Text(d.Follow(nf));
                if (!targets.Contains(name)) { b.SourcePointer(newOps + 4 + 4 * at++, op); continue; }
                if (d.Int(d.Field(op, 5)) != 80) throw new InvalidDataException("Expected Pocket encoder Shape op: " + name);
                ++matched;
                int outputField = d.Follow(d.Field(op, 4)) + 4, output = d.Int(outputField), temporary = NewTensor(name + "/full_shape");
                Buffer.BlockCopy(BitConverter.GetBytes(temporary), 0, source, outputField, 4);
                b.SourcePointer(newOps + 4 + 4 * at++, op);
                Constant(name + "/shape_start", 2, out int begin);
                Constant(name + "/shape_size", 1, out int size);
                int slice = b.Op(name, 84, 62, new[]{temporary, begin, size}, new[]{output});
                b.Pointer(newOps + 4 + 4 * at++, slice);
                int parameter = b.Table(1, 0); b.Pointer(slice + 12, parameter); b.Int(parameter + 4, 1);
            }
            if (targets.Length != matched) throw new InvalidDataException("Missing Pocket encoder Shape ops.");
            int tensorNames = b.Vector(new int[count + newNames.Count]); b.Pointer(net + 32, tensorNames);
            for (int i = 0; i < count; ++i) b.SourcePointer(tensorNames + 4 + 4 * i, d.Follow(names + 4 + 4 * i));
            for (int i = 0; i < newNames.Count; ++i) b.Pointer(tensorNames + 4 + 4 * (count + i), b.String(newNames[i]));
            b.Int(net + 36, count + newNames.Count);
            return b.Finish(source);
        }

        internal static byte[] Main(byte[] source, string[] outputs, bool priming)
        {
            var d = new MNNSerializedGraph.Reader(source);
            int root = d.Int(0), names = d.Follow(d.Field(root, 7)), ops = d.Follow(d.Field(root, 3));
            var tensorNames = Enumerable.Range(0, d.Count(names)).Select(i => d.Text(d.Follow(names + 4 + 4 * i))).ToArray();
            int text = Array.IndexOf(tensorNames, priming ? "text_embeddings" : "/input_linear/MatMul_output_0"),
                concat = Array.IndexOf(tensorNames, "/Concat_output_0");
            if (text < 0 || concat < 0) throw new InvalidDataException("Missing Pocket main concatenation.");
            var all = Enumerable.Range(0, d.Count(ops)).Select(i => d.Follow(ops + 4 + 4 * i)).ToArray();
            foreach (int op in all)
            {
                int field = d.Field(op, 0);
                if (field == 0) continue;
                int vector = d.Follow(field);
                for (int i = 0; i < d.Count(vector); ++i)
                    if (d.Int(vector + 4 + 4 * i) == concat)
                        Buffer.BlockCopy(BitConverter.GetBytes(text), 0, source, vector + 4 + 4 * i, 4);
            }
            var needed = new HashSet<int>(outputs.Select(name => Array.IndexOf(tensorNames, name)));
            var retained = new List<int>();
            foreach (int op in all.Reverse())
            {
                if (!d.Integers(d.Field(op, 4)).Any(needed.Contains)) continue;
                retained.Add(op);
                foreach (int input in d.Integers(d.Field(op, 0))) needed.Add(input);
            }
            retained.Reverse();
            var b = new MNNSerializedGraph.Prefix();
            int[] fields = Enumerable.Range(0, 12).Where(slot => slot == 4 || d.Field(root, slot) != 0).ToArray();
            int net = b.Table(12, fields); b.Pointer(0, net);
            foreach (int slot in fields)
            {
                if (slot == 3 || slot == 4) continue;
                int field = net + 4 + 4 * slot, original = d.Field(root, slot);
                if (slot == 5 || slot == 6 || slot == 9) b.Int(field, source[original]);
                else if (slot == 8) b.Int(field, d.Int(original));
                else b.SourcePointer(field, d.Follow(original));
            }
            int newOps = b.Vector(new int[retained.Count]); b.Pointer(net + 16, newOps);
            for (int i = 0; i < retained.Count; ++i) b.SourcePointer(newOps + 4 + 4 * i, retained[i]);
            int newOutputs = b.Vector(new int[outputs.Length]); b.Pointer(net + 20, newOutputs);
            for (int i = 0; i < outputs.Length; ++i) b.Pointer(newOutputs + 4 + 4 * i, b.String(outputs[i]));
            return b.Finish(source);
        }

    }
}
