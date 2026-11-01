using System;
using System.IO;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    internal static class FsEendReference
    {
        internal static string Root => Environment.GetEnvironmentVariable("MNN_TEST_FS_EEND") ?? throw new InvalidOperationException("Set MNN_TEST_FS_EEND.");
        internal static string Candidate => Path.Combine(Root, "candidate-v297");
        internal static string CallhomeRoot => Environment.GetEnvironmentVariable("MNN_TEST_FS_EEND_CALLHOME") ?? throw new InvalidOperationException("Set MNN_TEST_FS_EEND_CALLHOME.");
        internal static string CallhomeCandidate => Path.Combine(CallhomeRoot, "candidate-v324");
        internal static string CallhomeDirectory => Path.Combine(CallhomeRoot, "source-graph-v320/reference");
        internal static string Directory => Path.Combine(Root, "source-graph-v293/reference");
        internal static readonly string[] Cases = { "minimum", "short", "real", "quiet", "gain2", "silence", "constant", "hop_before", "hop_at", "hop_after",
            "subsample_before", "subsample_at", "subsample_after", "delay_before", "delay_at", "delay_after", "context_before", "context_at", "context_after", "maximum" };
        internal static float[] Read(string name)
            => ReadFrom(Directory, name);
        internal static float[] ReadFrom(string directory, string name)
        {
            var bytes = File.ReadAllBytes(Path.Combine(directory, name + ".f32")); var values = new float[bytes.Length / 4];
            Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length); return values;
        }
        internal static void Check(float[] expected, float[] actual, string label)
        {
            Assert.AreEqual(expected.Length, actual.Length, label); double maximum = 0;
            for (int i = 0; i < actual.Length; ++i)
            {
                Assert.False(float.IsNaN(actual[i]) || float.IsInfinity(actual[i]), label);
                maximum = Math.Max(maximum, Math.Abs((double)actual[i] - expected[i]));
            }
            TestContext.WriteLine(label + ";maximum_absolute=" + maximum.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            Assert.Less(maximum, .0001, label);
        }
    }
}
