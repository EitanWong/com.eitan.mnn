using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MNN.Unity
{
    // Numeric file format only: little-endian FP32/int32 NPY, v1/v2.
    // Converts Fortran storage to managed row-major storage when needed.
    // Never deserializes Python objects or pickle data.
    internal static class MNNNumpy
    {
        internal static float[] Floats(string path, params int[] shape)
        {
            using (var stream = File.OpenRead(path)) return (float[])Read(stream, stream.Length, true, shape);
        }

        internal static float[] Floats(ZipArchive archive, string name, params int[] shape) =>
            (float[])Entry(archive, name, true, shape);
        internal static int[] Ints(ZipArchive archive, string name, params int[] shape) =>
            (int[])Entry(archive, name, false, shape);

        private static Array Entry(ZipArchive archive, string name, bool floating, int[] shape)
        {
            var entry = archive.GetEntry(name + ".npy");
            if (entry == null) throw new InvalidDataException("Missing NumPy array: " + name);
            try { using (var stream = entry.Open()) return Read(stream, entry.Length, floating, shape); }
            catch (InvalidDataException e) { throw new InvalidDataException(name + ": " + e.Message, e); }
        }

        private static Array Read(Stream stream, long length, bool floating, int[] shape)
        {
            int count = 1;
            foreach (int d in shape)
            {
                if (d < 1) throw new InvalidDataException("Invalid expected NumPy shape.");
                count = checked(count * d);
            }
            if (!BitConverter.IsLittleEndian || length > 256L * 1024 * 1024 || length < 10)
                throw new InvalidDataException("Unsupported NumPy array size or platform byte order.");
            using (var reader = new BinaryReader(stream, Encoding.ASCII, true))
            {
                if (!reader.ReadBytes(6).SequenceEqual(new byte[] {147, 78, 85, 77, 80, 89}))
                    throw new InvalidDataException("Invalid NumPy signature.");
                int major = reader.ReadByte(), minor = reader.ReadByte();
                if ((major != 1 && major != 2) || minor != 0) throw new InvalidDataException("Unsupported NumPy version.");
                int headerSize = major == 1 ? reader.ReadUInt16() : checked((int)reader.ReadUInt32());
                if (headerSize < 1 || headerSize > 65536) throw new InvalidDataException("Invalid NumPy header size.");
                byte[] headerBytes = reader.ReadBytes(headerSize);
                if (headerBytes.Length != headerSize) throw new EndOfStreamException();
                string header = Encoding.ASCII.GetString(headerBytes);
                string type = floating ? "<f4" : "<i4";
                var dimensions = Regex.Match(header, @"'shape'\s*:\s*\(([^)]*)\)");
                var actual = dimensions.Groups[1].Value.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0);
                if (!header.Contains("'" + type + "'") || !Regex.IsMatch(header, @"'fortran_order'\s*:\s*(False|True)") ||
                    !dimensions.Success || !actual.SequenceEqual(shape.Select(d => d.ToString(System.Globalization.CultureInfo.InvariantCulture))) ||
                    length != 8 + (major == 1 ? 2 : 4) + headerSize + count * 4L)
                    throw new InvalidDataException("Unexpected NumPy type, order or shape.");
                Array result = floating ? (Array)new float[count] : new int[count];
                var buffer = new byte[Math.Min(1024 * 1024, checked(count * 4))];
                int position = 0, remaining = checked(count * 4);
                while (remaining > 0)
                {
                    int read = stream.Read(buffer, 0, Math.Min(buffer.Length, remaining));
                    if (read == 0) throw new EndOfStreamException("Truncated NumPy array.");
                    Buffer.BlockCopy(buffer, 0, result, position, read);
                    position += read; remaining -= read;
                }
                if (Regex.IsMatch(header, @"'fortran_order'\s*:\s*True"))
                {
                    Array reordered = floating ? (Array)new float[count] : new int[count];
                    var strides = new int[shape.Length];
                    int stride = 1;
                    for (int d = 0; d < shape.Length; ++d) { strides[d] = stride; stride = checked(stride * shape[d]); }
                    for (int i = 0; i < count; ++i)
                    {
                        int remainder = i, source = 0;
                        for (int d = shape.Length - 1; d >= 0; --d)
                        {
                            source += remainder % shape[d] * strides[d]; remainder /= shape[d];
                        }
                        if (floating) ((float[])reordered)[i] = ((float[])result)[source];
                        else ((int[])reordered)[i] = ((int[])result)[source];
                    }
                    result = reordered;
                }
                if (result is float[] values) MNNGenerationGraph.Validate(values, "NumPy asset");
                return result;
            }
        }
    }
}
