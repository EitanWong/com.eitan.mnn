using System;
using System.IO;
using System.Linq;

namespace MNN.Unity
{
    // Attach the public Op.externalPath field to external-weight operations in
    // an in-memory graph. Official Module loaders read the original weight file;
    // no temporary graph, weight copy, native object access or runtime mutation.
    internal static class MNNGraphExternalWeights
    {
        internal static byte[] Bind(byte[] source, string path)
        {
            path = Path.GetFullPath(path);
            long fileLength = File.Exists(path) ? new FileInfo(path).Length : -1;
            int externalCount = 0;
            var data = new MNNSerializedGraph.Reader(source);
            var prefix = new MNNSerializedGraph.Prefix();

            int Clone(int table, int slots, int[] scalarBytes, int[] scalarInts, int replace = -1)
            {
                int[] present = Enumerable.Range(0, slots).Where(s => s == replace || data.Field(table, s) != 0).ToArray();
                int result = prefix.Table(slots, present);
                foreach (int slot in present)
                {
                    int field = result + 4 + slot * 4;
                    if (slot == replace) continue;
                    int original = data.Field(table, slot);
                    if (scalarBytes.Contains(slot)) prefix.Int(field, source[original]);
                    else if (scalarInts.Contains(slot)) prefix.Int(field, data.Int(original));
                    else prefix.SourcePointer(field, data.Follow(original));
                }
                return result;
            }

            int External(int op)
            {
                int union = data.Field(op, 1), parameter = data.Field(op, 2);
                if (union == 0 || parameter == 0) return 0;
                // Public OpParameter / Blob / Convolution2D / Scale / LayerNorm schemas.
                int slot;
                switch (source[union])
                {
                    case 7: slot = 9; break;
                    case 9: slot = 6; break;
                    case 58: slot = 3; break;
                    case 88: slot = 5; break;
                    default: return 0;
                }
                int external = data.Field(data.Follow(parameter), slot);
                if (external == 0) return 0;
                int vector = data.Follow(external), count = data.Int(vector);
                if (count == 0) return 0;
                if (fileLength < 0)
                    throw new FileNotFoundException("Missing MNN external weights.", path);
                if (count < 2 || count > 6 || vector + 4L + 8L * count > source.Length)
                    throw new InvalidDataException("Invalid MNN external weight range.");
                long position = BitConverter.ToInt64(source, vector + 4);
                if (position < 0 || position > fileLength)
                    throw new InvalidDataException("MNN external weight offset is outside the weight file.");
                for (int i = 1; i < count; ++i)
                {
                    long size = BitConverter.ToInt64(source, vector + 4 + 8 * i);
                    if (size < 0 || size > fileLength - position)
                        throw new InvalidDataException("MNN external weights are truncated.");
                    position += size;
                }
                return external;
            }

            int Ops(int original)
            {
                int vector = data.Follow(original), count = data.Count(vector);
                int result = prefix.Vector(new int[count]);
                for (int i = 0; i < count; ++i)
                {
                    int op = data.Follow(vector + 4 + i * 4), field = result + 4 + i * 4;
                    if (External(op) == 0)
                    {
                        prefix.SourcePointer(field, op);
                        continue;
                    }
                    int copy = Clone(op, 8, new[]{1, 6}, new[]{5}, 7);
                    ++externalCount;
                    prefix.Pointer(field, copy);
                    prefix.Pointer(copy + 32, prefix.String(path));
                }
                return result;
            }

            int root = data.Int(0);
            int[] rootFields = Enumerable.Range(0, 12).Where(s => data.Field(root, s) != 0).ToArray();
            int net = prefix.Table(12, rootFields);
            prefix.Pointer(0, net);
            foreach (int slot in rootFields)
            {
                int field = net + 4 + slot * 4, original = data.Field(root, slot);
                if (slot == 3) prefix.Pointer(field, Ops(original));
                else if (slot == 10)
                {
                    int vector = data.Follow(original), count = data.Count(vector);
                    int subgraphs = prefix.Vector(new int[count]);
                    prefix.Pointer(field, subgraphs);
                    for (int i = 0; i < count; ++i)
                    {
                        int graph = data.Follow(vector + 4 + i * 4);
                        int copy = Clone(graph, 6, Array.Empty<int>(), Array.Empty<int>(), 4);
                        prefix.Pointer(subgraphs + 4 + i * 4, copy);
                        prefix.Pointer(copy + 20, Ops(data.Field(graph, 4)));
                    }
                }
                else if (slot == 8) prefix.Int(field, data.Int(original));
                else if (slot == 5 || slot == 6 || slot == 9) prefix.Int(field, source[original]);
                else prefix.SourcePointer(field, data.Follow(original));
            }
            return externalCount == 0 ? source : prefix.Finish(source);
        }
    }
}
