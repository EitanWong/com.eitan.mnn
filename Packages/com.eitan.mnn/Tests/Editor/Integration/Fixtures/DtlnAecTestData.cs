using System;
using System.IO;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    internal static class DtlnAecTestData
    {
        internal static string Root
        {
            get
            {
                string path = Environment.GetEnvironmentVariable("MNN_TEST_DTLN_AEC_ROOT");
                if (string.IsNullOrWhiteSpace(path)) Assert.Ignore("Set MNN_TEST_DTLN_AEC_ROOT to existing AEC Challenge inputs and independent references; no downloads.");
                return path;
            }
        }
        internal static string ModelDirectory(int units)
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_DTLN_AEC_MODEL_ROOT");
            if (string.IsNullOrWhiteSpace(root))
                Assert.Ignore("Set MNN_TEST_DTLN_AEC_MODEL_ROOT to existing Assets/StreamingAssets/MNN/Models; no downloads.");
            return Path.Combine(root, "DTLN-aec-" + units + "-MNN");
        }
        internal static float[] Read(string name)
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(Root, "Reference", name));
            Assert.Zero(bytes.Length % 4);
            var values = new float[bytes.Length / 4]; Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
            return values;
        }
        internal static void Compare(float[] expected, float[] actual)
        {
            Assert.AreEqual(expected.Length, actual.Length);
            double max = 0, squared = 0; int bad = 0;
            for (int i = 0; i < actual.Length; ++i)
            {
                Assert.False(float.IsNaN(actual[i]) || float.IsInfinity(actual[i]), "finite output");
                double error = Math.Abs(expected[i] - actual[i]); max = Math.Max(max, error); squared += error * error;
                if (error > 1e-4 + 1e-3 * Math.Abs(expected[i])) ++bad;
            }
            TestContext.WriteLine("samples=" + actual.Length + " max=" + max + " RMSE=" + Math.Sqrt(squared / actual.Length) + " outside_tolerance=" + bad);
            Assert.Zero(bad, "Compare complete waveform with independent Python MNN reference.");
        }
    }
}
