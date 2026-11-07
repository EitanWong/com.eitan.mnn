using System;
using System.IO;
using System.Linq;

namespace MNN.Unity
{
    // Append the public MNN ArgMax operator to the verified serialized model.
    // Read one int per frame instead of copying 25,055 logits to managed memory.
    internal static class MNNSenseVoiceGraph
    {
        internal const string OutputName = "mnn_unity_sensevoice_ids";

        internal static byte[] WithGreedyOutput(byte[] source)
        {
            var d = new MNNSerializedGraph.Reader(source);
            var b = new MNNSerializedGraph.Prefix();
            int root = d.Int(0), originalNames = d.Follow(d.Field(root, 7));
            int count = d.Count(originalNames), logits = -1;
            for (int i = 0; i < count; ++i)
                if (d.Text(d.Follow(originalNames + 4 + i * 4)) == "logits") logits = i;
            if (logits < 0) throw new InvalidDataException("Missing SenseVoice logits.");
            int[] fields = Enumerable.Range(0, 12).Where(slot => slot == 4 || slot == 8 || d.Field(root, slot) != 0).ToArray();
            int net = b.Table(12, fields);
            b.Pointer(0, net);
            foreach (int slot in fields)
            {
                if (slot == 3 || slot == 4 || slot == 7 || slot == 8) continue;
                int from = d.Field(root, slot), to = net + 4 + slot * 4;
                if (slot == 5 || slot == 6 || slot == 9) b.Int(to, source[from]);
                else b.SourcePointer(to, d.Follow(from));
            }
            int originalOps = d.Follow(d.Field(root, 3)), opCount = d.Count(originalOps);
            int ops = b.Vector(new int[opCount + 1]);
            b.Pointer(net + 16, ops);
            for (int i = 0; i < opCount; ++i)
                b.SourcePointer(ops + 4 + i * 4, d.Follow(originalOps + 4 + i * 4));
            int argmax = b.Op(OutputName, 2, 2, new[] { logits }, new[] { count });
            b.Pointer(ops + 4 + opCount * 4, argmax);
            int parameter = b.Table(4, 0, 1, 2);
            b.Pointer(argmax + 12, parameter);
            b.Int(parameter + 4, 0);
            b.Int(parameter + 8, 1);
            b.Int(parameter + 12, 2);
            int names = b.Vector(new int[count + 1]);
            b.Pointer(net + 32, names);
            for (int i = 0; i < count; ++i)
                b.SourcePointer(names + 4 + i * 4, d.Follow(originalNames + 4 + i * 4));
            b.Pointer(names + 4 + count * 4, b.String(OutputName));
            int outputs = b.Vector(new int[1]);
            b.Pointer(net + 20, outputs);
            b.Pointer(outputs + 4, b.String(OutputName));
            b.Int(net + 36, count + 1);
            return b.Finish(source);
        }
    }
}
