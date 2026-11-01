using System;
using System.IO;
using System.Linq;

namespace MNN.Unity
{
    // Generalize the pinned 128-frame export's public serialized position tables.
    // Keep time embedding's independent 128 frequencies and source file unchanged.
    internal static class MNNIndicF5Graph
    {
        internal static byte[] Prepare(byte[] source, int frames)
        {
            if (frames < 64 || frames > 4096) throw new ArgumentOutOfRangeException(nameof(frames));
            var d = new MNNSerializedGraph.Reader(source); var b = new MNNSerializedGraph.Prefix();
            int root = d.Int(0), ops = d.Follow(d.Field(root, 3));
            int[] fields = Enumerable.Range(0, 12).Where(slot => d.Field(root, slot) != 0).ToArray();
            int net = b.Table(12, fields); b.Pointer(0, net);
            foreach (int slot in fields)
            {
                if (slot == 3) continue;
                int field = net + 4 + 4 * slot, original = d.Field(root, slot);
                if (slot == 5 || slot == 6 || slot == 9) b.Int(field, source[original]);
                else if (slot == 8) b.Int(field, d.Int(original));
                else b.SourcePointer(field, d.Follow(original));
            }
            int newOps = b.Vector(new int[d.Count(ops)]); b.Pointer(net + 16, newOps); int matched = 0;
            for (int i = 0; i < d.Count(ops); ++i)
            {
                int oldOp = d.Follow(ops + 4 + 4 * i), nameField = d.Field(oldOp, 3);
                string name = nameField == 0 ? "" : d.Text(d.Follow(nameField));
                int kind = name == "/dit/text_embed/Constant_3_output_0" ? 1
                    : name == "/dit/text_embed/Gather_output_0" ? 2
                    : name == "/dit/transformer_blocks.0/attn/Cos_output_0" ? 3
                    : name == "/dit/transformer_blocks.0/attn/Sin_output_0" ? 4
                    : name == "/dit/Reshape_output_0" ? 5 : 0;
                if (name == "x" || name == "cond" || name == "text")
                {
                    int input = b.Op(name, 34, source[d.Field(oldOp, 1)], Array.Empty<int>(), d.Integers(d.Field(oldOp, 4)));
                    b.Pointer(newOps + 4 + 4 * i, input);
                    int parameter = b.Table(3, 0, 1, 2); b.Pointer(input + 12, parameter);
                    b.Pointer(parameter + 4, b.Vector(name == "text" ? new[]{1, frames} : new[]{1, frames, 100}));
                    b.Int(parameter + 8, name == "text" ? 3 : 1); b.Int(parameter + 12, 0);
                    continue;
                }
                if (kind == 0) { b.SourcePointer(newOps + 4 + 4 * i, oldOp); continue; }
                if (d.Int(d.Field(oldOp, 5)) != 11) throw new InvalidDataException("Expected IndicF5 constant: " + name);
                ++matched;
                int op = b.Op(name, 11, 7, Array.Empty<int>(), d.Integers(d.Field(oldOp, 4)));
                b.Pointer(newOps + 4 + 4 * i, op);
                int blob = b.Table(8, 0, 1, 2, kind == 1 ? 5 : 7); b.Pointer(op + 12, blob);
                b.Pointer(blob + 4, b.Vector(kind == 1 ? new[]{1} : new[]{1, frames, kind == 2 ? 512 : 64}));
                b.Int(blob + 8, 0); b.Int(blob + 12, kind == 1 ? 3 : 1);
                if (kind == 1) b.Pointer(blob + 24, b.Vector(new[]{frames}));
                else
                {
                    int width = kind == 2 ? 512 : 64;
                    int original = d.Follow(d.Field(d.Follow(d.Field(oldOp, 2)), 7));
                    var values = new int[frames * width];
                    for (int t = 0; t < frames; ++t)
                    for (int c = 0; c < width; ++c)
                    {
                        if (t < 128) { values[t * width + c] = d.Int(original + 4 + 4 * (t * width + c)); continue; }
                        float frequency = (float)(1 / Math.Pow(10000, kind == 2 ? (c % 256) / 256.0 : (c / 2) / 32.0));
                        float angle = t * frequency;
                        float value = kind == 2 ? (float)(c < 256 ? Math.Cos(angle) : Math.Sin(angle))
                            : kind == 3 ? (float)Math.Cos(angle) : kind == 4 ? (float)Math.Sin(angle) : angle;
                        values[t * width + c] = BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
                    }
                    b.Pointer(blob + 32, b.Vector(values));
                }
            }
            if (matched != 5) throw new InvalidDataException("Incomplete IndicF5 position constants.");
            return b.Finish(source);
        }
    }
}
