using System;
using System.IO;

namespace MNN.Unity
{
    // Little-endian IEEE binary16 file assets; no native object layouts involved.
    // Row reads keep large embedding tables on disk instead of inflating them to FP32.
    internal sealed class MNNHalfTable : IDisposable
    {
        private static readonly float[] Decode = BuildDecode();
        private readonly FileStream _stream;
        private readonly int _rows, _channels;
        internal MNNHalfTable(string path, int rows, int channels)
        {
            if (rows < 1 || channels < 1) throw new ArgumentOutOfRangeException(nameof(rows));
            _stream = File.OpenRead(path); _rows = rows; _channels = channels;
            if (_stream.Length != checked((long)rows * channels * 2))
            {
                _stream.Dispose(); throw new InvalidDataException("Invalid FP16 table size: " + path);
            }
        }
        private static float[] BuildDecode()
        {
            var values = new float[65536];
            for (int n = 0; n < values.Length; ++n)
            {
                int exponent = (n >> 10) & 31, mantissa = n & 1023;
                values[n] = exponent == 31 ? (mantissa == 0 ? float.PositiveInfinity : float.NaN) :
                    (float)(exponent == 0 ? mantissa * Math.Pow(2, -24) : (1 + mantissa / 1024.0) * Math.Pow(2, exponent - 15));
                if ((n & 32768) != 0) values[n] = -values[n];
            }
            return values;
        }
        internal float[] Row(int row)
        {
            if (row < 0 || row >= _rows) throw new InvalidDataException("Embedding ID outside FP16 table.");
            _stream.Position = checked((long)row * _channels * 2);
            return Read(_channels);
        }
        private float[] Read(int count)
        {
            var bytes = new byte[checked(count * 2)]; int offset = 0;
            while (offset < bytes.Length)
            {
                int read = _stream.Read(bytes, offset, bytes.Length - offset);
                if (read == 0) throw new EndOfStreamException("Truncated FP16 table.");
                offset += read;
            }
            var values = new float[count];
            for (int i = 0; i < count; ++i)
            {
                float value = Decode[bytes[i * 2] | bytes[i * 2 + 1] << 8];
                if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidDataException("Non-finite FP16 asset.");
                values[i] = value;
            }
            return values;
        }
        internal static float[] Load(string path, int rows, int channels)
        {
            using (var table = new MNNHalfTable(path, rows, channels)) return table.Read(checked(rows * channels));
        }
        public void Dispose() => _stream.Dispose();
    }
}
