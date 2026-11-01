using System;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class DualTurnWaveTests
    {
        [Test]
        public void ChannelReaderPreservesBothSpeakersSignsAndFiniteGain()
        {
            MethodInfo method = typeof(MNNWaveAudio).GetMethod("ReadChannels", BindingFlags.Static | BindingFlags.NonPublic,
                null, new[] { typeof(string), typeof(int).MakeByRefType(), typeof(bool) }, null);
            Assert.IsNotNull(method, "Missing channel-preserving WAV reader.");
            string root = Environment.GetEnvironmentVariable("MNN_TEST_DUALTURN");
            if (string.IsNullOrEmpty(root)) Assert.Ignore("Set MNN_TEST_DUALTURN for WAV artifacts.");
            string path = Path.Combine(root, "wave-channel-v482.wav");
            float[][] expected = { new[] { -2f, .5f, 1e-5f }, new[] { 2f, -.25f, -1e-5f } };
            Write(path, expected, 24000);
            object[] args = { path, 0, false };
            var channels = (float[][])method.Invoke(null, args);
            Assert.AreEqual(24000, args[1]);
            Assert.AreEqual(2, channels.Length);
            CollectionAssert.AreEqual(expected[0], channels[0]);
            CollectionAssert.AreEqual(expected[1], channels[1]);
            Assert.Throws<InvalidDataException>(() => MNNWaveAudio.Read(path, out _));
            expected[0][0] = -.5f; expected[1][0] = .25f;
            Write(path, expected, 24000);
            CollectionAssert.AreEqual(new[] { -.125f, .125f, 0f }, MNNWaveAudio.Read(path, out _));
            Write(path, new[] { expected[0] }, 16000);
            args = new object[] { path, 0, false };
            channels = (float[][])method.Invoke(null, args);
            Assert.AreEqual(16000, args[1]);
            Assert.AreEqual(1, channels.Length);
            CollectionAssert.AreEqual(expected[0], channels[0]);
            expected[0][1] = float.NaN; Write(path, expected, 24000);
            var error = Assert.Throws<TargetInvocationException>(() => method.Invoke(null, new object[] { path, 0, false }));
            Assert.IsInstanceOf<InvalidDataException>(error.InnerException);
        }

        internal static void Write(string path, float[][] channels, int rate)
        {
            int samples = channels[0].Length, bytes = samples * channels.Length * 4;
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + bytes);
                writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                writer.Write((short)3); writer.Write((short)channels.Length); writer.Write(rate);
                writer.Write(rate * channels.Length * 4); writer.Write((short)(channels.Length * 4)); writer.Write((short)32);
                writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(bytes);
                for (int i = 0; i < samples; ++i)
                    foreach (float[] channel in channels) writer.Write(channel[i]);
            }
        }
    }
}
