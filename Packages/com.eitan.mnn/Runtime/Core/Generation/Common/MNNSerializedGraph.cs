using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MNN.Unity
{
    // Reads and prefixes the public MNN FlatBuffer serialization schema.
    // These are file offsets, never native C++ object or ABI layouts.
    internal static class MNNSerializedGraph
    {
        internal static Dictionary<string, string> Metadata(byte[] graph)
        {
            var data = new Reader(graph);
            int extra = data.Follow(data.Field(data.Int(0), 2));
            int buffer = data.Follow(data.Field(extra, 0)), count = data.Int(buffer);
            if (count < 4 || count > 1024 * 1024 || buffer + 4L + count > graph.Length)
                throw new InvalidDataException("Invalid MNN metadata buffer.");
            var bytes = new byte[count];
            Buffer.BlockCopy(graph, buffer + 4, bytes, 0, count);
            var metadata = new Reader(bytes);
            int attributes = metadata.Follow(metadata.Field(metadata.Int(0), 3));
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < metadata.Count(attributes); ++i)
            {
                int attribute = metadata.Follow(attributes + 4 + i * 4);
                values.Add(metadata.Text(metadata.Follow(metadata.Field(attribute, 3))), metadata.Text(metadata.Follow(metadata.Field(attribute, 0))));
            }
            return values;
        }

        internal sealed class Reader
        {
            private readonly byte[] _data;
            internal Reader(byte[] data) { _data = data; }
            internal int Int(int offset)
            {
                if (offset < 0 || offset > _data.Length - 4) throw new InvalidDataException("Truncated MNN graph.");
                return BitConverter.ToInt32(_data, offset);
            }
            internal int Follow(int offset) => checked(offset + Int(offset));
            internal int Field(int table, int slot)
            {
                int vtable = checked(table - Int(table)), offset = checked(vtable + 4 + 2 * slot);
                if (vtable < 0 || vtable > _data.Length - 4 || offset > _data.Length - 2)
                    throw new InvalidDataException("Invalid MNN table.");
                if (4 + 2 * slot >= BitConverter.ToUInt16(_data, vtable)) return 0;
                int relative = BitConverter.ToUInt16(_data, offset);
                return relative == 0 ? 0 : checked(table + relative);
            }
            internal int Count(int vector)
            {
                int count = Int(vector);
                if (count < 0 || vector + 4L + 4L * count > _data.Length)
                    throw new InvalidDataException("Invalid MNN vector.");
                return count;
            }
            internal int[] Integers(int field)
            {
                if (field == 0) return Array.Empty<int>();
                int vector = Follow(field), count = Count(vector);
                var result = new int[count];
                for (int i = 0; i < count; ++i) result[i] = Int(vector + 4 + i * 4);
                return result;
            }
            internal string Text(int value)
            {
                int length = Int(value);
                if (length < 0 || value + 4L + length > _data.Length) throw new InvalidDataException("Invalid MNN name.");
                return new UTF8Encoding(false, true).GetString(_data, value + 4, length);
            }
        }

        internal sealed class Prefix
        {
            private readonly List<byte> _bytes = new List<byte>(new byte[4]);
            private readonly List<KeyValuePair<int, int>> _sourcePointers = new List<KeyValuePair<int, int>>();
            private void Align() { while ((_bytes.Count & 3) != 0) _bytes.Add(0); }
            private int Allocate(int count) { int p = _bytes.Count; _bytes.AddRange(new byte[count]); return p; }
            internal void Int(int p, int n)
            {
                byte[] bytes = BitConverter.GetBytes(n);
                for (int i = 0; i < 4; ++i) _bytes[p + i] = bytes[i];
            }
            private void Short(int p, ushort n) { _bytes[p] = (byte)n; _bytes[p + 1] = (byte)(n >> 8); }
            internal void Pointer(int p, int target)
            {
                if (target <= p) throw new InvalidDataException("MNN offsets must point forward.");
                Int(p, checked(target - p));
            }
            internal void SourcePointer(int p, int target) => _sourcePointers.Add(new KeyValuePair<int, int>(p, target));
            internal int Table(int slots, params int[] present)
            {
                Align(); int v = Allocate(4 + 2 * slots); Align(); int table = Allocate(4 + 4 * slots);
                Short(v, (ushort)(4 + 2 * slots)); Short(v + 2, (ushort)(4 + 4 * slots)); Int(table, table - v);
                foreach (int slot in present) Short(v + 4 + 2 * slot, (ushort)(4 + 4 * slot));
                return table;
            }
            internal int Vector(int[] values)
            {
                Align(); int vector = Allocate(4 + 4 * values.Length); Int(vector, values.Length);
                for (int i = 0; i < values.Length; ++i) Int(vector + 4 + 4 * i, values[i]);
                return vector;
            }
            internal int String(string text)
            {
                Align(); byte[] bytes = Encoding.UTF8.GetBytes(text); int value = Allocate(5 + bytes.Length); Int(value, bytes.Length);
                for (int i = 0; i < bytes.Length; ++i) _bytes[value + 4 + i] = bytes[i];
                return value;
            }
            internal int Op(string name, int type, int parameter, int[] inputs, int[] outputs)
            {
                int op = Table(8, 0, 1, 2, 3, 4, 5);
                Int(op + 8, parameter); Int(op + 24, type);
                Pointer(op + 4, Vector(inputs)); Pointer(op + 16, String(name)); Pointer(op + 20, Vector(outputs));
                return op;
            }
            internal byte[] Finish(byte[] source)
            {
                // The source can contain int64/double vectors; preserve their alignment.
                while ((_bytes.Count & 7) != 0) _bytes.Add(0);
                int shift = _bytes.Count;
                foreach (var pointer in _sourcePointers) Pointer(pointer.Key, checked(shift + pointer.Value));
                var result = new byte[checked(shift + source.Length)]; _bytes.CopyTo(result);
                Buffer.BlockCopy(source, 0, result, shift, source.Length);
                return result;
            }
        }
    }
}
