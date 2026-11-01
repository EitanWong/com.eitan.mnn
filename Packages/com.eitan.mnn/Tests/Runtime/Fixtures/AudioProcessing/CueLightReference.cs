using System;
using System.IO;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    internal static class CueLightReference
    {
        internal static float[] Read(string directory, string name)
        {
            var bytes = File.ReadAllBytes(Path.Combine(directory, name + ".f32")); var values = new float[bytes.Length / 4];
            Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length); return values;
        }
        internal static void Check(string label, float[] expected, float[] actual)
        {
            Assert.AreEqual(expected.Length, actual.Length, label); double maximum = 0;
            for (int i = 0; i < expected.Length; ++i)
            {
                Assert.That(actual[i], Is.InRange(0f, 1f), label); maximum = Math.Max(maximum, Math.Abs((double)expected[i] - actual[i]));
            }
            TestContext.WriteLine(label + ";maximum_absolute=" + maximum.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            Assert.Less(maximum, .0001, label);
        }
        internal static void CheckEvents(string label, float[] expected, MNNCueLightAnalysis actual)
        {
            Assert.AreEqual(expected.Length / 5, actual.FrameCount, label); var e = new float[actual.FrameCount * 2]; var a = new float[e.Length];
            for (int i = 0; i < actual.FrameCount; ++i)
            {
                var f = actual.Frames[i]; e[i * 2] = expected[i * 5]; e[i * 2 + 1] = expected[i * 5 + 1];
                a[i * 2] = f.SpeechProbability; a[i * 2 + 1] = f.EndOfTurnProbability;
                Assert.AreEqual((int)expected[i * 5 + 2], f.ResetCount, label + " reset " + i);
                Assert.AreEqual(expected[i * 5 + 3] == 1, f.SnappyEndpoint, label + " snappy " + i);
                Assert.AreEqual(expected[i * 5 + 4] == 1, f.CarefulEndpoint, label + " careful " + i);
            }
            Check(label, e, a);
        }
    }
}
