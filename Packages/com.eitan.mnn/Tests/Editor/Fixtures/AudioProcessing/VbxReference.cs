using System;
using System.IO;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    internal static class VbxReference
    {
        internal static string Root => Environment.GetEnvironmentVariable("MNN_TEST_VBX") ?? throw new InvalidOperationException("Set MNN_TEST_VBX.");
        internal static string Candidate => Path.Combine(Root, "candidate-v346");
        internal static string Directory => Path.Combine(Root, "source-v341/reference");
        internal static readonly string[] Cases = { "minimum", "short", "real", "quiet", "gain2", "silence", "constant", "hop_before", "hop_at", "hop_after",
            "cmn_before", "cmn_at", "cmn_after", "segment_before", "segment_at", "segment_after", "jump_before", "jump_at", "jump_after", "maximum" };
        internal static float[] Read(string name)
        {
            var bytes = File.ReadAllBytes(Path.Combine(Directory, name + ".f32")); var result = new float[bytes.Length / 4];
            Buffer.BlockCopy(bytes, 0, result, 0, bytes.Length); return result;
        }
        internal static void Check(float[] expected, float[] actual, string label)
        {
            Assert.AreEqual(256, actual.Length, label); double error = 0, norm = 0, actualNorm = 0, product = 0;
            for (int i = 0; i < actual.Length; ++i)
            {
                Assert.False(float.IsNaN(actual[i]) || float.IsInfinity(actual[i]), label);
                double e = expected[i], a = actual[i]; error += (a - e) * (a - e); norm += e * e; actualNorm += a * a; product += e * a;
            }
            double nrmse = Math.Sqrt(error / norm), cosine = product / Math.Sqrt(norm * actualNorm);
            TestContext.WriteLine(label + ";nrmse=" + nrmse.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ";cosine=" + cosine.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            Assert.Less(nrmse, .001, label); Assert.GreaterOrEqual(cosine, .99999, label);
        }
    }
}
