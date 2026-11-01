using System;
using System.IO;
using System.Text;

namespace MNN.Unity
{
    public static class MNNWaveAudio
    {
        /// <summary>Bounded mono PCM16 WAV for speech endpoints; no disk roundtrip.</summary>
        public static byte[] EncodePcm16(float[] samples, int sampleRate)
        {
            if (samples == null || sampleRate < 8000 || sampleRate > 96000 || samples.Length < 1 || samples.Length > sampleRate * 120L)
                throw new ArgumentException("Supply 0–120 seconds of mono audio at 8–96 kHz.");
            using (var stream = new MemoryStream(checked(44 + samples.Length * 2)))
            using (var writer = new BinaryWriter(stream, Encoding.ASCII))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples.Length * 2);
                writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                writer.Write((short)1); writer.Write((short)1); writer.Write(sampleRate);
                writer.Write(sampleRate * 2); writer.Write((short)2); writer.Write((short)16);
                writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(samples.Length * 2);
                foreach (float sample in samples)
                {
                    if (float.IsNaN(sample) || float.IsInfinity(sample) || Math.Abs(sample) > 1)
                        throw new ArgumentException("Supply finite normalized PCM.");
                    writer.Write((short)Math.Max(short.MinValue, Math.Min(short.MaxValue, Math.Round(sample * 32768.0))));
                }
                return stream.ToArray();
            }
        }

        // Shared RIFF decoding; task-specific duration/silence constraints belong to callers.
        internal static float[] Read(string path, out int sampleRate)
        {
            float[][] channels = ReadChannels(path, out sampleRate, true);
            var mono = new float[channels[0].Length];
            for (int i = 0; i < mono.Length; ++i)
                for (int channel = 0; channel < channels.Length; ++channel)
                    mono[i] += channels[channel][i] / channels.Length;
            return mono;
        }

        // Preserve speaker channels; the caller chooses whether finite gain above one is valid.
        internal static float[][] ReadChannels(string path, out int sampleRate, bool requireNormalized)
        {
            using (var stream = File.OpenRead(path))
            using (var reader = new BinaryReader(stream, Encoding.ASCII))
            {
                if (stream.Length < 44 || stream.Length > 64 * 1024 * 1024 || new string(reader.ReadChars(4)) != "RIFF")
                    throw new InvalidDataException("Expected a RIFF WAV recording.");
                long end = (long)reader.ReadUInt32() + 8;
                if (end != stream.Length || new string(reader.ReadChars(4)) != "WAVE")
                    throw new InvalidDataException("Truncated or unsupported WAV container.");
                ushort format = 0, channels = 0, bits = 0, align = 0;
                int rate = 0;
                byte[] data = null;
                bool hasFormat = false;
                while (stream.Position + 8 <= end)
                {
                    string id = new string(reader.ReadChars(4));
                    uint count = reader.ReadUInt32();
                    long next = stream.Position + count + (count & 1);
                    if (next > end)
                        throw new InvalidDataException("Truncated WAV chunk.");
                    if (id == "fmt ")
                    {
                        if (hasFormat || count < 16)
                            throw new InvalidDataException("Invalid WAV format chunk.");
                        hasFormat = true;
                        format = reader.ReadUInt16(); channels = reader.ReadUInt16(); rate = reader.ReadInt32();
                        reader.ReadUInt32(); align = reader.ReadUInt16(); bits = reader.ReadUInt16();
                    }
                    else if (id == "data")
                    {
                        if (data != null)
                            throw new InvalidDataException("Multiple WAV data chunks are unsupported.");
                        data = reader.ReadBytes(checked((int)count));
                    }
                    stream.Position = next;
                }
                if (stream.Position != end || !hasFormat || data == null || channels < 1 || channels > 2 || rate < 8000 || rate > 96000 ||
                    !(format == 1 && (bits == 16 || bits == 24 || bits == 32) || format == 3 && bits == 32) ||
                    align != channels * (bits / 8) || data.Length % align != 0)
                    throw new InvalidDataException("Use mono/stereo PCM16/24/32 or float32 WAV at 8–96 kHz.");
                var samples = new float[channels][];
                for (int channel = 0; channel < channels; ++channel)
                    samples[channel] = new float[data.Length / align];
                int bytes = bits / 8;
                for (int i = 0; i < samples[0].Length; ++i)
                    for (int c = 0; c < channels; ++c)
                    {
                        int p = i * align + c * bytes;
                        double x = format == 3 ? BitConverter.ToSingle(data, p) : bits == 16 ? BitConverter.ToInt16(data, p) / 32768.0 : bits == 32 ? BitConverter.ToInt32(data, p) / 2147483648.0 : ((data[p] | data[p + 1] << 8 | data[p + 2] << 16) << 8 >> 8) / 8388608.0;
                        if (double.IsNaN(x) || double.IsInfinity(x) || requireNormalized && Math.Abs(x) > 1)
                            throw new InvalidDataException("Non-finite or out-of-range WAV PCM.");
                        samples[c][i] = (float)x;
                    }
                sampleRate = rate;
                return samples;
            }
        }
    }
}
