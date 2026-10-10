using System;
using System.IO;
using System.Text;

namespace MNN.Unity.Tests
{
    internal static class AudioFixture
    {
        internal static void SaveWave(string path, float[] samples, int sampleRate)
        {
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + samples.Length * 2);
                writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(sampleRate);
                writer.Write(sampleRate * 2);
                writer.Write((short)2);
                writer.Write((short)16);
                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write(samples.Length * 2);
                foreach (float sample in samples)
                    writer.Write((short)(Math.Max(-1f, Math.Min(1f, sample)) * 32767));
            }
        }
    }
}
