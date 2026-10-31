using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    public sealed partial class SpeechProcessingPlayerTests
    {
        [Serializable] private sealed class TigerCase { public string @case = ""; }
        [Serializable] private sealed class TigerCases { public TigerCase[] cases = null; }

        [TestCase("TIGER-Speech-16K-MNN", "MNN_TEST_TIGER_REFERENCE", MNNBackendType.CPU)]
        [TestCase("TIGER-Speech-16K-MNN", "MNN_TEST_TIGER_REFERENCE", MNNBackendType.Metal)]
        public void TigerWaveformsMatchOriginalCheckpointInBuiltPlayer(string modelName, string variable, MNNBackendType backend)
        {
            RequirePlayer(backend);
            string reference = Environment.GetEnvironmentVariable(variable);
            var names = JsonUtility.FromJson<TigerCases>(File.ReadAllText(Path.Combine(reference, "cases.json"))).cases.Select(c => c.@case).ToArray();
            Assert.AreEqual(25, names.Length);
            using (var model = MNNTigerSeparation.Load(Model(modelName), 4, backend))
            {
                foreach (string name in names.Concat(names.Take(1)))
                {
                    var input = Read(variable, name + "-input");
                    var expected = Read(variable, name + "-target");
                    for (int repeat = 0; repeat < 2; ++repeat)
                    {
                        var actual = model.Separate(input);
                        Assert.AreEqual(16000, actual.SampleRate);
                        CheckTigerSource(expected, actual.Speaker0, 0, name);
                        CheckTigerSource(expected, actual.Speaker1, 1, name);
                        Assert.AreEqual(backend, model.Backend);
                    }
                }
                float[] zero = new float[8000];
                var resampled = model.Separate(zero, 8000);
                Assert.AreEqual(8000, resampled.SampleRate);
                Assert.AreEqual(8000, resampled.Speaker0.Length);
                Assert.AreEqual(8000, resampled.Speaker1.Length);
                Assert.True(resampled.Speaker0.All(v => v == 0));
                Assert.True(resampled.Speaker1.All(v => v == 0));
            }
        }

        private static void CheckTigerSource(float[] expected, float[] actual, int speaker, string name)
        {
            Assert.AreEqual(expected.Length / 2, actual.Length);
            double error = 0, energy = 0;
            int offset = speaker * actual.Length;
            for (int i = 0; i < actual.Length; ++i)
            {
                Assert.False(float.IsNaN(actual[i]) || float.IsInfinity(actual[i]), name);
                double value = expected[offset + i], delta = (double)actual[i] - value;
                energy += value * value; error += delta * delta;
            }
            double nrmse = Math.Sqrt(error / (energy == 0 ? 1 : energy));
            TestContext.WriteLine(name + ";source=" + speaker + ";nrmse=" + nrmse);
            Assert.Less(nrmse, .001, name);
        }
    }
}
