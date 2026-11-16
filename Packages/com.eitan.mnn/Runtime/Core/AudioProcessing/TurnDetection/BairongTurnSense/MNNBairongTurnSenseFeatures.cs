using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>The author's snipped Hamming fbank, centered 7/6 LFR and 560-dimensional CMVN.</summary>
    internal sealed class MNNBairongTurnSenseFeatures
    {
        private readonly double[] _shift, _scale;
        internal MNNBairongTurnSenseFeatures(string cmvn)
        {
            string[] lines = File.ReadAllLines(cmvn);
            _shift = Read(lines, "<AddShift>"); _scale = Read(lines, "<Rescale>");
        }
        private static double[] Read(string[] lines, string marker)
        {
            int index = Array.FindIndex(lines, line => line.TrimStart().StartsWith(marker, StringComparison.Ordinal));
            if (index < 0 || index + 1 >= lines.Length) throw new InvalidDataException("Missing CMVN section: " + marker);
            string row = lines[index + 1]; int start = row.IndexOf('['), end = row.IndexOf(']');
            if (start < 0 || end <= start) throw new InvalidDataException("Invalid CMVN vector.");
            double[] values = row.Substring(start + 1, end - start - 1).Split((char[])null, StringSplitOptions.RemoveEmptyEntries)
                .Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray();
            if (values.Length != 560 || values.Any(v => double.IsNaN(v) || double.IsInfinity(v)))
                throw new InvalidDataException("CMVN must contain 560 finite values.");
            return values;
        }
        internal float[] Extract(float[] samples, out int frames, CancellationToken token)
        {
            if (samples == null || samples.Length < 400 || samples.Length > 128000)
                throw new ArgumentException("Supply 25 ms to eight seconds of 16 kHz mono PCM.", nameof(samples));
            var fbank = MNNKaldiFeatures.ExtractFsmn(samples, out int count, token);
            frames = (count + 5) / 6;
            var result = new float[frames * 560];
            for (int frame = 0; frame < frames; ++frame)
            {
                token.ThrowIfCancellationRequested();
                for (int context = 0; context < 7; ++context)
                {
                    int source = Math.Max(0, Math.Min(count - 1, frame * 6 + context - 3)) * 80;
                    for (int bin = 0; bin < 80; ++bin)
                    {
                        int column = context * 80 + bin;
                        result[frame * 560 + column] = (float)((fbank[source + bin] + _shift[column]) * _scale[column]);
                    }
                }
            }
            return result;
        }
    }
}
