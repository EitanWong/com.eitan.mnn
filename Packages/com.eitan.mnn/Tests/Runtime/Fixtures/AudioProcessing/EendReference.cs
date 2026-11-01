using System;
using System.IO;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    internal static class EendReference
    {
        internal static float[] Read(string path)
        {
            var bytes = File.ReadAllBytes(path); var result = new float[bytes.Length / 4];
            Buffer.BlockCopy(bytes, 0, result, 0, bytes.Length); return result;
        }
        internal static void Check(string label, float[] expected, float[] actual)
        {
            Assert.AreEqual(expected.Length, actual.Length, label);
            double maximum = 0;
            for (int i = 0; i < actual.Length; ++i)
            {
                Assert.That(actual[i], Is.InRange(0f, 1f), label);
                maximum = Math.Max(maximum, Math.Abs((double)actual[i] - expected[i]));
            }
            TestContext.WriteLine(label + ";maximum_absolute=" + maximum.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            Assert.Less(maximum, .0001, label);
        }
    }
}
