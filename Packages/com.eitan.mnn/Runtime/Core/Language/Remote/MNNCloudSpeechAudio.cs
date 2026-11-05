using System;
using System.IO;
using System.Text;
using UnityEngine;
namespace MNN.Unity
{
    public static class MNNCloudSpeechAudio
    {
        private static InvalidOperationException Invalid() => new InvalidOperationException("The speech service returned incomplete or unsupported audio.");
        public static byte[] Hex(string hex)
        {
            if (hex == null || hex.Length > 32 * 1024 * 1024 || hex.Length == 0 || hex.Length % 2 != 0) throw Invalid();
            var data = new byte[hex.Length / 2];
            for (int i = 0; i < data.Length; ++i)
            {
                int high = Digit(hex[i * 2]), low = Digit(hex[i * 2 + 1]);
                if (high < 0 || low < 0) throw Invalid();
                data[i] = (byte)(high * 16 + low);
            }
            return data;
        }
        private static int Digit(char c) => c >= '0' && c <= '9' ? c - '0' : c >= 'a' && c <= 'f' ? c - 'a' + 10 : c >= 'A' && c <= 'F' ? c - 'A' + 10 : -1;
        [Serializable] private sealed class Chunk { public int code = -1; public string data; }
        public static byte[] VolcChunks(string lines)
        {
            using (var output = new MemoryStream())
            using (var reader = new StringReader(lines))
            {
                bool ended = false;
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var chunk = JsonUtility.FromJson<Chunk>(line);
                    if (chunk.code == 20000000) { ended = true; break; }
                    if (chunk.code != 0) throw Invalid();
                    if (string.IsNullOrEmpty(chunk.data)) continue;
                    byte[] data = Convert.FromBase64String(chunk.data);
                    output.Write(data, 0, data.Length);
                }
                if (!ended || output.Length == 0) throw Invalid();
                return output.ToArray();
            }
        }
        public static float[] Decode(byte[] bytes, bool wave, int targetRate)
        {
            if (bytes == null || bytes.Length > 16 * 1024 * 1024 || targetRate < 8000 || targetRate > 96000) throw Invalid();
            int offset = 0, length = bytes.Length, rate = targetRate, channels = 1;
            if (wave) ReadWave(bytes, out offset, out length, out rate, out channels);
            if (length == 0 || length % (2 * channels) != 0) throw Invalid();
            int frames = length / (2 * channels);
            var mono = new float[frames];
            for (int i = 0; i < frames; ++i)
            {
                float sum = 0;
                for (int channel = 0; channel < channels; ++channel)
                {
                    int at = offset + (i * channels + channel) * 2;
                    sum += (short)(bytes[at] | bytes[at + 1] << 8) / 32768f;
                }
                mono[i] = sum / channels;
            }
            if (rate == targetRate) return mono;
            int count = checked((int)((long)frames * targetRate / rate));
            if (count < 1 || count > 24000 * 360) throw Invalid();
            var result = new float[count];
            for (int i = 0; i < count; ++i)
            {
                double source = (double)i * rate / targetRate;
                int lo = Math.Min(frames - 1, (int)source), hi = Math.Min(frames - 1, lo + 1);
                result[i] = mono[lo] + (mono[hi] - mono[lo]) * (float)(source - lo);
            }
            return result;
        }
        private static void ReadWave(byte[] data, out int start, out int length, out int rate, out int channels)
        {
            start = length = rate = channels = 0;
            if (data.Length < 44 || Encoding.ASCII.GetString(data, 0, 4) != "RIFF" || Encoding.ASCII.GetString(data, 8, 4) != "WAVE") throw Invalid();
            using (var reader = new BinaryReader(new MemoryStream(data)))
            {
                reader.BaseStream.Position = 12;
                while (reader.BaseStream.Position + 8 <= data.Length)
                {
                    string id = Encoding.ASCII.GetString(reader.ReadBytes(4));
                    uint count = reader.ReadUInt32();
                    long next = reader.BaseStream.Position + count + (count & 1);
                    if (count > int.MaxValue || next > data.Length) throw Invalid();
                    if (id == "fmt ")
                    {
                        if (count < 16 || reader.ReadUInt16() != 1) throw Invalid();
                        channels = reader.ReadUInt16(); rate = reader.ReadInt32(); reader.ReadUInt32(); reader.ReadUInt16();
                        if (reader.ReadUInt16() != 16 || channels < 1 || channels > 2 || rate < 8000 || rate > 96000) throw Invalid();
                    }
                    if (id == "data") { start = (int)reader.BaseStream.Position; length = (int)count; }
                    reader.BaseStream.Position = next;
                }
            }
            if (length == 0 || rate == 0 || channels == 0) throw Invalid();
        }
    }
}
