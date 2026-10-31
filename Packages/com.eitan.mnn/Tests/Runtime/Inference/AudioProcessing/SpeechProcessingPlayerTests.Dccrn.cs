using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    public sealed partial class SpeechProcessingPlayerTests
    {
        [Serializable] private sealed class DccrnCase { public string @case = ""; }
        [Serializable] private sealed class DccrnCases { public DccrnCase[] cases = null; }

        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal)]
        public void MayavozDccrnWaveformsMatchOriginalCheckpointInBuiltPlayer(MNNBackendType backend)
        {
            RequirePlayer(backend);
            const string variable = "MNN_TEST_DCCRN_REFERENCE";
            string reference = Environment.GetEnvironmentVariable(variable);
            var names = JsonUtility.FromJson<DccrnCases>(File.ReadAllText(Path.Combine(reference, "cases.json"))).cases.Select(c => c.@case).ToArray();
            Assert.AreEqual(22, names.Length);
            using (var model = MNNDccrnEnhancement.Load(Model("DCCRN-Mayavoz-VCTK-16K-MNN"), 4, backend))
            {
                foreach (string name in names.Concat(names.Take(1)))
                    for (int repeat = 0; repeat < 2; ++repeat)
                    {
                        float[] input = Read(variable, name + "-input"), snapshot = (float[])input.Clone();
                        var result = model.Enhance(input);
                        CheckDccrnWaveform(Read(variable, name + "-target"), result, name);
                        CollectionAssert.AreEqual(snapshot, input);
                        Assert.AreEqual(backend, model.Backend);
                        Assert.AreEqual(MNNBackendType.CPU, model.AnalysisBackend);
                        Assert.AreEqual(MNNBackendType.CPU, model.RecurrentBackend);
                        Assert.AreEqual(backend, model.DecoderBackend);
                    }
                foreach (int rate in new[] { 8000, 48000 })
                {
                    var input = new float[rate];
                    var actual = model.Enhance(input, rate);
                    Assert.AreEqual(input.Length, actual.Length);
                    Assert.True(actual.All(value => !float.IsNaN(value) && !float.IsInfinity(value)));
                }
            }
        }

        private static void CheckDccrnWaveform(float[] expected, float[] actual, string name)
        {
            Assert.AreEqual(expected.Length, actual.Length);
            double error = 0, energy = 0;
            for (int i = 0; i < actual.Length; ++i)
            {
                Assert.False(float.IsNaN(actual[i]) || float.IsInfinity(actual[i]), name);
                double value = expected[i], delta = (double)actual[i] - value;
                error += delta * delta; energy += value * value;
            }
            double nrmse = Math.Sqrt(error / (energy == 0 ? 1 : energy));
            TestContext.WriteLine(name + ";nrmse=" + nrmse);
            Assert.Less(nrmse, .001, name);
        }
    }
}
