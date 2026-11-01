using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    internal static class CueLightPcmReference
    {
        internal static void Verify(string directory, Func<string, float[]> read, IEnumerable<string> cases, MNNBackendType backend)
        {
            using (var model = MNNCueLightTurnDetector.Load(directory, MNNCueLightTurnDetector.DefaultThreadCount, backend))
            {
                foreach (string name in cases.Concat(new[] { cases.First() }))
                {
                    var pcm = read(name + "-input"); var cold = model.Analyze(pcm, resetOnTurnStart: false);
                    Check(read(name + "-cold_probabilities"), cold.Probabilities, .0001, backend + ";" + name + ";cold");
                    CollectionAssert.AreEqual(cold.Probabilities, model.Analyze(pcm, resetOnTurnStart: false).Probabilities, name + " cold repeat");
                    var expected = read(name + "-events"); var result = model.Analyze(pcm); var repeat = model.Analyze(pcm);
                    Assert.AreEqual((pcm.Length + 319) / 320, result.FrameCount); Assert.AreEqual((double)pcm.Length / 16000, result.DurationSeconds);
                    var actual = new float[result.FrameCount * 2]; var probabilities = new float[result.FrameCount * 2];
                    for (int i = 0; i < result.FrameCount; ++i)
                    {
                        var f = result.Frames[i]; var r = repeat.Frames[i];
                        probabilities[i * 2] = expected[i * 5]; probabilities[i * 2 + 1] = expected[i * 5 + 1];
                        actual[i * 2] = f.SpeechProbability; actual[i * 2 + 1] = f.EndOfTurnProbability;
                        Assert.AreEqual((int)expected[i * 5 + 2], f.ResetCount, name + " reset " + i);
                        Assert.AreEqual(expected[i * 5 + 3] == 1, f.SnappyEndpoint, name + " snappy " + i);
                        Assert.AreEqual(expected[i * 5 + 4] == 1, f.CarefulEndpoint, name + " careful " + i);
                        Assert.AreEqual(f.ResetCount, r.ResetCount); Assert.AreEqual(f.SnappyEndpoint, r.SnappyEndpoint); Assert.AreEqual(f.CarefulEndpoint, r.CarefulEndpoint);
                    }
                    CollectionAssert.AreEqual(result.Probabilities, repeat.Probabilities, name + " default repeat");
                    Check(probabilities, actual, .0001, backend + ";" + name + ";default");
                }
                Assert.AreEqual(backend == MNNBackendType.Auto ? MNNBackendType.CPU : backend, model.Backend);
            }
        }
        internal static void Check(float[] expected, float[] actual, double gate, string label)
        {
            Assert.AreEqual(expected.Length, actual.Length, label); double maximum = 0;
            for (int i = 0; i < actual.Length; ++i)
            {
                Assert.False(float.IsNaN(actual[i]) || float.IsInfinity(actual[i]), label);
                maximum = Math.Max(maximum, Math.Abs((double)actual[i] - expected[i]));
            }
            TestContext.WriteLine(label + ";maximum_absolute=" + maximum.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            Assert.Less(maximum, gate, label);
        }
    }
}
