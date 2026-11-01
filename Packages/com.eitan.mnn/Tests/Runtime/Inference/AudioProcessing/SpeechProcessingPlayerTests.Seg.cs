using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    public sealed partial class SpeechProcessingPlayerTests
    {
        [Serializable] private sealed class SegCase { public string @case = ""; }
        [Serializable] private sealed class SegCases { public SegCase[] cases = null; }

        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Auto)]
        public void SegGestureConditionedWaveformsMatchOriginalCheckpointInBuiltPlayer(MNNBackendType backend)
        {
            RequirePlayer(MNNBackendType.CPU);
            string reference = Environment.GetEnvironmentVariable("MNN_TEST_SEG_REFERENCE");
            var names = JsonUtility.FromJson<SegCases>(File.ReadAllText(Path.Combine(reference, "cases.json"))).cases.Select(c => c.@case).ToArray();
            Assert.AreEqual(37, names.Length);
            using (var model = MNNSegExtraction.Load(Model("SEG-YGD-16K-MNN"), 4, backend))
            {
                foreach (string name in names.Concat(new[] { names[0] }))
                {
                    var input = Read("MNN_TEST_SEG_REFERENCE", name + "-input");
                    var values = Read("MNN_TEST_SEG_REFERENCE", name + "-gesture");
                    var gestures = new MNNSegGestureFeatures(values); values[0] = float.NaN;
                    var expected = Read("MNN_TEST_SEG_REFERENCE", name + "-target");
                    for (int repeat = 0; repeat < 2; ++repeat)
                    {
                        CheckSegArray(model.Extract(input, gestures), expected, name + ":" + repeat);
                        Assert.AreEqual(MNNBackendType.CPU, model.Backend);
                    }
                }
                var restored = model.Extract(new float[3201], new MNNSegGestureFeatures(new float[30]), 48000);
                Assert.AreEqual(3201, restored.Length); Assert.True(restored.All(v => !float.IsNaN(v) && !float.IsInfinity(v)));
            }
        }

        private static void CheckSegArray(float[] actual, float[] expected, string name)
        {
            Assert.AreEqual(expected.Length, actual.Length, name); double energy = 0, error = 0;
            for (int i = 0; i < actual.Length; ++i)
            {
                Assert.False(float.IsNaN(actual[i]) || float.IsInfinity(actual[i]), name);
                double value = expected[i]; energy += value * value; double delta = actual[i] - value; error += delta * delta;
            }
            double nrmse = Math.Sqrt(error / (energy == 0 ? 1 : energy));
            TestContext.WriteLine(name + ";nrmse=" + nrmse); Assert.Less(nrmse, .001, name);
        }
    }
}
