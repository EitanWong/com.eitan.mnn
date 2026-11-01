using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    internal static class ResembleEnhancerReference
    {
        internal static string Root => Environment.GetEnvironmentVariable("MNN_TEST_RESEMBLE_FULL_PROBE")
            ?? throw new InvalidOperationException("Set the original complete Resemble reference root.");
        internal static string Model => Environment.GetEnvironmentVariable("MNN_TEST_RESEMBLE_ENHANCER_MODEL")
            ?? throw new InvalidOperationException("Set the complete Resemble model directory.");
        internal static string Reference => Path.Combine(Root, "full-cfm-managed-reference");
        internal static string[] Names => MNNModelData.Array(MNNModelData.Get(
            MNNModelData.Read(Path.Combine(Reference, "cases.json")), "cases"))
            .Select(row => (string)MNNModelData.Get(row, "case")).ToArray();

        internal static float[] Read(string name, string folder = null)
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(folder ?? Reference, name + ".f32"));
            Assert.That(bytes.Length % 4, Is.Zero);
            var result = new float[bytes.Length / 4];
            Buffer.BlockCopy(bytes, 0, result, 0, bytes.Length);
            return result;
        }

        internal static double Check(string label, float[] actual, float[] expected, double limit = .001)
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Length), label);
            double error = 0, energy = 0;
            for (int i = 0; i < actual.Length; ++i)
            {
                Assert.That(float.IsNaN(actual[i]) || float.IsInfinity(actual[i]), Is.False, label);
                double delta = (double)actual[i] - expected[i];
                error += delta * delta;
                energy += (double)expected[i] * expected[i];
            }
            double nrmse = Math.Sqrt(error / (energy == 0 ? 1 : energy));
            TestContext.WriteLine(label + ";nrmse=" + nrmse.ToString("R"));
            Assert.That(nrmse, Is.LessThan(limit), label);
            return nrmse;
        }

        internal static void CheckBackends(MNNResembleEnhancer model, MNNBackendType expected)
        {
            Assert.That(model.DenoiserBackend, Is.EqualTo(expected));
            Assert.That(model.EncoderBackend, Is.EqualTo(expected));
            Assert.That(model.VelocityBackend, Is.EqualTo(expected));
            Assert.That(model.DecoderBackend, Is.EqualTo(expected));
            Assert.That(model.VocoderBackend, Is.EqualTo(expected));
        }
    }
}
