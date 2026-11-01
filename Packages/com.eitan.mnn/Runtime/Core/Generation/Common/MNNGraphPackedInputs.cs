using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MNN.Unity
{
    // Public serialized graph adapter for Module::forward(VARP). Packed float
    // inputs are sliced/reshaped by MNN ops; no native vtable dispatch is used.
    internal static class MNNGraphPackedInputs
    {
        internal const string InputName = "mnn_unity_packed_inputs";

        internal sealed class Slice
        {
            internal readonly string Name;
            internal readonly int[] Begin, Size, Shape;
            internal Slice(string name, int[] begin, int[] size, params int[] shape)
            { Name = name; Begin = begin; Size = size; Shape = shape; }
        }

        internal static byte[] Pack(byte[] source, int[] packedShape, params Slice[] inputs)
        {
            if (packedShape.Length == 0 || packedShape.Any(n => n < 1) || inputs.Length == 0 ||
                inputs.Select(input => input.Name).Distinct().Count() != inputs.Length || inputs.Any(input =>
                input.Begin.Length != packedShape.Length || input.Size.Length != packedShape.Length || input.Begin.Any(n => n < 0) ||
                input.Size.Any(n => n == 0 || n < -1) || input.Shape.Any(n => n == 0 || n < -1) || input.Shape.Count(n => n == -1) > 1))
                throw new ArgumentException("Invalid packed Module slice shapes.");
            var d = new MNNSerializedGraph.Reader(source);
            var b = new MNNSerializedGraph.Prefix();
            int root = d.Int(0), originalNames = d.Follow(d.Field(root, 7)), count = d.Count(originalNames);
            var names = new List<string>{InputName};
            int originalOps = d.Follow(d.Field(root, 3));
            var targets = new Dictionary<string, int>();
            var retained = new List<int>();
            for (int i = 0; i < d.Count(originalOps); ++i)
            {
                int op = d.Follow(originalOps + 4 + 4 * i), nameField = d.Field(op, 3);
                string name = nameField == 0 ? "" : d.Text(d.Follow(nameField));
                if (!inputs.Any(input => input.Name == name)) { retained.Add(op); continue; }
                int[] outputs = d.Integers(d.Field(op, 4));
                if (d.Int(d.Field(op, 5)) != 34 || outputs.Length != 1 || targets.ContainsKey(name))
                    throw new InvalidDataException("Expected unique packed graph input: " + name);
                int parameter = d.Follow(d.Field(op, 2)), type = d.Field(parameter, 1);
                if (type != 0 && d.Int(type) != 1) throw new InvalidDataException("Packed Module slices require float32 inputs.");
                targets.Add(name, outputs[0]);
            }
            if (targets.Count != inputs.Length) throw new InvalidDataException("Missing packed graph input.");
            int[] fields = Enumerable.Range(0, 12).Where(slot => slot == 8 || d.Field(root, slot) != 0).ToArray();
            int net = b.Table(12, fields); b.Pointer(0, net);
            foreach (int slot in fields)
            {
                if (slot == 3 || slot == 7 || slot == 8) continue;
                int field = net + 4 + 4 * slot, original = d.Field(root, slot);
                if (slot == 5 || slot == 6 || slot == 9) b.Int(field, source[original]);
                else b.SourcePointer(field, d.Follow(original));
            }
            int ops = b.Vector(new int[retained.Count + 1 + inputs.Length * 4]);
            b.Pointer(net + 16, ops); int index = 0;
            void Add(int op) { b.Pointer(ops + 4 + 4 * index++, op); }
            int NewTensor(string name) { int id = count + names.Count; names.Add(name); return id; }
            int packed = b.Op(InputName, 34, 21, Array.Empty<int>(), new[]{count}); Add(packed);
            int param = b.Table(3, 0, 1, 2); b.Pointer(packed + 12, param);
            b.Pointer(param + 4, b.Vector(packedShape));
            b.Int(param + 8, 1); b.Int(param + 12, 0);

            int Constant(string name, int[] values)
            {
                int tensor = NewTensor(name), op = b.Op(name, 11, 7, Array.Empty<int>(), new[]{tensor}); Add(op);
                int blob = b.Table(8, 0, 1, 2, 5); b.Pointer(op + 12, blob);
                b.Pointer(blob + 4, b.Vector(new[]{values.Length})); b.Int(blob + 8, 0); b.Int(blob + 12, 3);
                b.Pointer(blob + 24, b.Vector(values)); return tensor;
            }
            foreach (var input in inputs)
            {
                int begin = Constant(input.Name + "/packed_begin", input.Begin);
                int size = Constant(input.Name + "/packed_size", input.Size);
                int sliced = NewTensor(input.Name + "/packed_slice");
                int slice = b.Op(input.Name + "/packed_slice", 84, 62, new[]{count, begin, size}, new[]{sliced}); Add(slice);
                int sliceParam = b.Table(1, 0); b.Pointer(slice + 12, sliceParam); b.Int(sliceParam + 4, 1);
                int reshape = b.Op(input.Name, 73, 55, new[]{sliced}, new[]{targets[input.Name]}); Add(reshape);
                int shape = b.Table(2, 0, 1); b.Pointer(reshape + 12, shape);
                b.Pointer(shape + 4, b.Vector(input.Shape)); b.Int(shape + 8, 0);
            }
            foreach (int op in retained) b.SourcePointer(ops + 4 + 4 * index++, op);
            int tensorNames = b.Vector(new int[count + names.Count]); b.Pointer(net + 32, tensorNames);
            for (int i = 0; i < count; ++i) b.SourcePointer(tensorNames + 4 + 4 * i, d.Follow(originalNames + 4 + 4 * i));
            for (int i = 0; i < names.Count; ++i) b.Pointer(tensorNames + 4 + 4 * (count + i), b.String(names[i]));
            b.Int(net + 36, count + names.Count);
            return b.Finish(source);
        }
    }
}
