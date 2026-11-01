using System;
using System.IO;
using System.Linq;

namespace MNN.Unity
{
    // Append a fixed linear projection using the public FlatBuffer MatMul/Add
    // operators, so Module executes it with its optimized native kernels.
    internal static class MNNGraphLinearHead
    {
        internal const string OutputName = "mnn_unity_linear_head";

        // A standalone public Input op provides the seed for the same native
        // MatMul/Add kernels used when appending a projection to a model.
        internal static byte[] Create(float[] weights, float[] bias, int channels)
        {
            var b = new MNNSerializedGraph.Prefix();
            int net = b.Table(12, 3, 4, 5, 6, 7, 8); b.Pointer(0, net);
            int ops = b.Vector(new int[1]); b.Pointer(net + 16, ops);
            int input = b.Op("input", 34, 21, Array.Empty<int>(), new[]{0}); b.Pointer(ops + 4, input);
            int param = b.Table(3, 0, 1, 2); b.Pointer(input + 12, param);
            b.Pointer(param + 4, b.Vector(new[]{1, channels})); b.Int(param + 8, 1); b.Int(param + 12, 0);
            int outputs = b.Vector(new int[1]); b.Pointer(net + 20, outputs); b.Pointer(outputs + 4, b.String("input"));
            b.Int(net + 24, 0); b.Int(net + 28, 0);
            int names = b.Vector(new int[1]); b.Pointer(net + 32, names); b.Pointer(names + 4, b.String("input"));
            b.Int(net + 36, 1);
            return Append(b.Finish(Array.Empty<byte>()), "input", weights, bias, channels);
        }

        internal static byte[] Append(byte[] source, string inputName, float[] weights, float[] bias, int channels)
        {
            if (channels < 1 || bias == null || bias.Length == 0 || weights == null || weights.Length != checked(channels * bias.Length) ||
                weights.Concat(bias).Any(v => float.IsNaN(v) || float.IsInfinity(v)))
                throw new ArgumentException("Invalid linear projection.");
            var d = new MNNSerializedGraph.Reader(source); var b = new MNNSerializedGraph.Prefix();
            int root = d.Int(0), originalNames = d.Follow(d.Field(root, 7)), count = d.Count(originalNames), input = -1;
            for (int i = 0; i < count; ++i)
                if (d.Text(d.Follow(originalNames + 4 + i * 4)) == inputName) input = i;
            if (input < 0) throw new InvalidDataException("Missing linear head input.");
            int[] fields = Enumerable.Range(0, 12).Where(slot => slot == 4 || slot == 8 || d.Field(root, slot) != 0).ToArray();
            int net = b.Table(12, fields); b.Pointer(0, net);
            foreach (int slot in fields)
            {
                if (slot == 3 || slot == 4 || slot == 7 || slot == 8) continue;
                int from = d.Field(root, slot), to = net + 4 + 4 * slot;
                if (slot == 5 || slot == 6 || slot == 9) b.Int(to, source[from]);
                else b.SourcePointer(to, d.Follow(from));
            }
            int originalOps = d.Follow(d.Field(root, 3)), opCount = d.Count(originalOps);
            int ops = b.Vector(new int[opCount + 4]); b.Pointer(net + 16, ops);
            for (int i = 0; i < opCount; ++i) b.SourcePointer(ops + 4 + 4 * i, d.Follow(originalOps + 4 + 4 * i));
            void Constant(string name, int index, int tensor, float[] values, params int[] shape)
            {
                int op = b.Op(name, 11, 7, Array.Empty<int>(), new[]{tensor}); b.Pointer(ops + 4 + 4 * index, op);
                int blob = b.Table(8, 0, 1, 2, 7); b.Pointer(op + 12, blob);
                b.Pointer(blob + 4, b.Vector(shape)); b.Int(blob + 8, 0); b.Int(blob + 12, 1);
                var bits = new int[values.Length]; Buffer.BlockCopy(values, 0, bits, 0, checked(values.Length * 4));
                b.Pointer(blob + 32, b.Vector(bits));
            }
            Constant(OutputName + "/weights", opCount, count, weights, bias.Length, channels);
            Constant(OutputName + "/bias", opCount + 1, count + 1, bias, bias.Length);
            int matmul = b.Op(OutputName + "/matmul", 39, 25, new[]{input, count}, new[]{count + 2});
            b.Pointer(ops + 4 + 4 * (opCount + 2), matmul);
            int multiply = b.Table(5, 0, 1, 2); b.Pointer(matmul + 12, multiply);
            b.Int(multiply + 4, 1); b.Int(multiply + 8, 0); b.Int(multiply + 12, 1);
            int add = b.Op(OutputName, 7, 6, new[]{count + 2, count + 1}, new[]{count + 3});
            b.Pointer(ops + 4 + 4 * (opCount + 3), add);
            int addition = b.Table(3, 0, 1); b.Pointer(add + 12, addition); b.Int(addition + 4, 0); b.Int(addition + 8, 1);
            int tensorNames = b.Vector(new int[count + 4]); b.Pointer(net + 32, tensorNames);
            for (int i = 0; i < count; ++i) b.SourcePointer(tensorNames + 4 + 4 * i, d.Follow(originalNames + 4 + 4 * i));
            string[] names = {OutputName + "/weights", OutputName + "/bias", OutputName + "/matmul", OutputName};
            for (int i = 0; i < 4; ++i) b.Pointer(tensorNames + 4 + 4 * (count + i), b.String(names[i]));
            int outputs = b.Vector(new int[1]); b.Pointer(net + 20, outputs); b.Pointer(outputs + 4, b.String(OutputName));
            b.Int(net + 36, count + 4);
            return b.Finish(source);
        }
    }
}
