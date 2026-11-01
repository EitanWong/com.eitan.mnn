using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    public sealed partial class SpeechProcessingPlayerTests
    {
        [Serializable] private sealed class ResembleCase { public string @case = ""; }
        [Serializable] private sealed class ResembleCases { public ResembleCase[] cases = null; }

        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Auto)]
        public void ResembleDenoiserWaveformsMatchOriginalCheckpointInBuiltPlayer(MNNBackendType backend)
        {
            RequirePlayer(MNNBackendType.CPU);
            const string variable = "MNN_TEST_RESEMBLE_DENOISER_REFERENCE";
            string reference = Environment.GetEnvironmentVariable(variable);
            var names = JsonUtility.FromJson<ResembleCases>(File.ReadAllText(Path.Combine(reference, "cases.json"))).cases.Select(c => c.@case).ToArray();
            Assert.AreEqual(24, names.Length);
            using (var model = MNNResembleDenoiser.Load(Model("Resemble-Denoiser-44K-MNN"), 8, backend))
            {
                foreach (string name in names.Concat(names.Take(1)))
                {
                    var input = Read(variable, name + "-input"); var expected = Read(variable, name + "-target");
                    for (int repeat = 0; repeat < 2; ++repeat)
                    {
                        var actual = model.Enhance(input); Assert.AreEqual(expected.Length, actual.Length);
                        double energy = 0, error = 0;
                        for (int i = 0; i < actual.Length; ++i)
                        {
                            Assert.False(float.IsNaN(actual[i]) || float.IsInfinity(actual[i]), name);
                            double value = expected[i], delta = actual[i] - value; energy += value * value; error += delta * delta;
                        }
                        double nrmse = Math.Sqrt(error / (energy == 0 ? 1 : energy));
                        TestContext.WriteLine(name + ";nrmse=" + nrmse); Assert.Less(nrmse, .001, name);
                        Assert.AreEqual(MNNBackendType.CPU, model.Backend);
                    }
                }
                float[] resampled = model.Enhance(new float[16000], 16000);
                Assert.AreEqual(16000, resampled.Length); Assert.True(resampled.All(v => v == 0));
            }
        }
    }
}
