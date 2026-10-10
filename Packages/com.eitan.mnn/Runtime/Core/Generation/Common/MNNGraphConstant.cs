using System;
using System.IO;
using System.Text;

namespace MNN.Unity
{
    // Specialize a fixed scalar input in a copy of the official serialized MNN graph.
    // Only public FlatBuffer schema fields are read. No native object layout is accessed.
    internal static class MNNGraphConstant
    {
        internal static byte[] FreezeInputs(byte[] source, params MNNGenerationGraph.Input[] inputs)
        {
            byte[] result = (byte[])source.Clone();
            foreach (var input in inputs)
                result = FreezeInput(result, input);
            return result;
        }

        private static byte[] FreezeInput(byte[] source, MNNGenerationGraph.Input input)
        {
            int Read(int p)
            {
                if (p < 0 || p > source.Length - 4)
                    throw new InvalidDataException("Truncated MNN graph.");
                return BitConverter.ToInt32(source, p);
            }

            int Follow(int p) => checked(p + Read(p));
            int Field(int table, int slot)
            {
                int v = checked(table - Read(table));
                if (v < 0 || v > source.Length - 4 || 4 + slot * 2 >= BitConverter.ToUInt16(source, v))
                    throw new InvalidDataException("Missing MNN field.");
                int off = BitConverter.ToUInt16(source, v + 4 + slot * 2);
                if (off == 0)
                    throw new InvalidDataException("Missing MNN field.");
                return checked(table + off);
            }

            int ops = Follow(Field(Read(0), 3)), count = Read(ops), target = -1;
            if (count < 1 || ops + 4L + count * 4L > source.Length)
                throw new InvalidDataException("Invalid MNN op vector.");
            for (int i = 0; i < count; ++i)
            {
                int op = Follow(ops + 4 + i * 4), name = Follow(Field(op, 3)), length = Read(name);
                if (length < 0 || name + 4L + length > source.Length)
                    throw new InvalidDataException("Invalid MNN name.");
                if (Encoding.UTF8.GetString(source, name + 4, length) != input.Name)
                    continue;
                if (Read(Field(op, 5)) != 34 || target != -1)
                    throw new InvalidDataException("Expected unique MNN Input: " + input.Name);
                target = op;
            }

            if (target < 0)
                throw new InvalidDataException("Missing MNN Input: " + input.Name);
            // Blob schema slots: dims0, dataFormat1, dataType2, int32s5, float32s7.
            int start = checked((source.Length + 3) / 4 * 4), table = start + 20, dims = table + 20, data = dims + 4 + input.Shape.Length * 4;
            byte[] result = new byte[checked(data + 4 + input.Values.Length * 4)];
            Array.Copy(source, result, source.Length);
            void Int(int p, int n) => Buffer.BlockCopy(BitConverter.GetBytes(n), 0, result, p, 4);
            void Short(int p, ushort n) => Buffer.BlockCopy(BitConverter.GetBytes(n), 0, result, p, 2);
            Short(start, 20);
            Short(start + 2, 20);
            Short(start + 4, 4);
            Short(start + 6, 8);
            Short(start + 8, 12);
            bool integer = input.Values is int[];
            if (!integer && !(input.Values is float[]))
                throw new NotSupportedException("Only int32/float32 graph constants are supported.");
            Short(start + 4 + (integer ? 5 : 7) * 2, 16);
            Int(table, 20);
            Int(table + 4, dims - (table + 4));
            Int(table + 8, 0); // NCHW in MNN_DATA_FORMAT
            Int(table + 12, integer ? 3 : 1);
            Int(table + 16, data - (table + 16));
            Int(dims, input.Shape.Length);
            Buffer.BlockCopy(input.Shape, 0, result, dims + 4, input.Shape.Length * 4);
            Int(data, input.Values.Length);
            Buffer.BlockCopy(input.Values, 0, result, data + 4, input.Values.Length * 4);
            result[Field(target, 1)] = 7;
            Int(Field(target, 2), table - Field(target, 2));
            Int(Field(target, 5), 11);
            return result;
        }

        internal static byte[] FreezeIntInput(byte[] source, string name, int value)
        {
            int Read(int offset)
            {
                if (offset < 0 || offset > source.Length - 4)
                    throw new InvalidDataException("Truncated MNN graph.");
                return BitConverter.ToInt32(source, offset);
            }

            int Follow(int offset) => checked(offset + Read(offset));
            int Field(int table, int slot)
            {
                int vtable = checked(table - Read(table));
                if (vtable < 0 || vtable > source.Length - 4)
                    throw new InvalidDataException("Invalid MNN table.");
                int length = BitConverter.ToUInt16(source, vtable), entry = checked(vtable + 4 + slot * 2);
                if (4 + slot * 2 >= length || entry > source.Length - 2)
                    throw new InvalidDataException("Missing MNN field.");
                int relative = BitConverter.ToUInt16(source, entry);
                if (relative == 0)
                    throw new InvalidDataException("Missing MNN field.");
                return checked(table + relative);
            }

            int ops = Follow(Field(Read(0), 3)), count = Read(ops), target = -1;
            if (count < 1 || ops + 4L + count * 4L > source.Length)
                throw new InvalidDataException("Invalid MNN op list.");
            for (int i = 0; i < count; ++i)
            {
                int op = Follow(ops + 4 + i * 4), text = Follow(Field(op, 3)), n = Read(text);
                if (n < 0 || text + 4L + n > source.Length)
                    throw new InvalidDataException("Invalid MNN name.");
                if (Encoding.UTF8.GetString(source, text + 4, n) != name)
                    continue;
                if (Read(Field(op, 5)) != 34)
                    throw new InvalidDataException("Expected scalar input op: " + name);
                int input = Follow(Field(op, 2)), shape = Follow(Field(input, 0));
                if (Read(shape) != 1 || Read(shape + 4) != 1 || Read(Field(input, 1)) != 3)
                    throw new InvalidDataException("Expected int32 input [1].");
                if (target != -1)
                    throw new InvalidDataException("Duplicate MNN input.");
                target = op;
            }

            if (target < 0)
                throw new InvalidDataException("Missing fixed MNN input: " + name);
            int start = checked((source.Length + 3) / 4 * 4), table = start + 16, dims = table + 20, data = dims + 8;
            byte[] result = new byte[data + 8];
            Array.Copy(source, result, source.Length);
            void Int(int offset, int number) => Buffer.BlockCopy(BitConverter.GetBytes(number), 0, result, offset, 4);
            void Short(int offset, ushort number) => Buffer.BlockCopy(BitConverter.GetBytes(number), 0, result, offset, 2);
            // Blob fields: dims, dataFormat, dataType, uint8s, int8s, int32s.
            Short(start, 16);
            Short(start + 2, 20);
            Short(start + 4, 4);
            Short(start + 6, 8);
            Short(start + 8, 12);
            Short(start + 14, 16);
            Int(table, 16);
            Int(table + 4, dims - (table + 4));
            Int(table + 12, 3);
            Int(table + 16, data - (table + 16));
            Int(dims, 1);
            Int(dims + 4, 1);
            Int(data, 1);
            Int(data + 4, value);
            result[Field(target, 1)] = 7; // OpParameter::Blob
            Int(Field(target, 2), table - Field(target, 2));
            Int(Field(target, 5), 11); // OpType::Const
            return result;
        }
    }
}
