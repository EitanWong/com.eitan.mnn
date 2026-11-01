using System;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    internal static class CueLightReference
    {
        internal static void Verify(string directory, string reference, MNNBackendType backend)
        {
            var cases = new[] { "minimum", "hop_before", "hop_at", "hop_after", "frame_before", "frame_at", "frame_after", "real", "mixed_language",
                "quiet", "gain2", "silence", "constant", "noise", "context_before", "context_at", "context_after", "reset_before", "reset_at", "reset_after", "long_call", "maximum" };
            using (var model = MNNCueLightTurnDetector.Load(directory, backendType: backend))
            {
                foreach (string name in cases.Concat(new[] { cases[0] }))
                {
                    var pcm = CueLightReference.Read(reference, name + "-input"); var expected = CueLightReference.Read(reference, name + "-cold_probabilities");
                    var cold = model.Analyze(pcm, resetOnTurnStart: false);
                    CueLightReference.Check(name + ";cold", expected, cold.Probabilities);
                    CollectionAssert.AreEqual(cold.Probabilities, model.Analyze(pcm, resetOnTurnStart: false).Probabilities, name + " cold repeat");
                    expected = CueLightReference.Read(reference, name + "-events");
                    var actual = model.Analyze(pcm); var repeat = model.Analyze(pcm);
                    CueLightReference.CheckEvents(name + ";default", expected, actual); CueLightReference.CheckEvents(name + ";default_repeat", expected, repeat);
                    CollectionAssert.AreEqual(actual.Probabilities, repeat.Probabilities, name + " default repeat");
                    Assert.AreEqual((pcm.Length + 319) / 320, actual.FrameCount); Assert.AreEqual(pcm.Length / 16000.0, actual.DurationSeconds);
                }
                Assert.AreEqual(backend == MNNBackendType.Auto ? MNNBackendType.CPU : backend, model.Backend);
                Assert.Throws<OperationCanceledException>(() => model.Analyze(new float[1], cancellationToken: new CancellationToken(true)));
                model.Dispose(); Assert.Throws<ObjectDisposedException>(() => model.Push(new float[320]));
            }
        }
        internal static float[] Read(string directory, string name)
        {
            var bytes = File.ReadAllBytes(Path.Combine(directory, name + ".f32")); var values = new float[bytes.Length / 4];
            Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length); return values;
        }
        internal static void Check(string label, float[] expected, float[] actual)
        {
            Assert.AreEqual(expected.Length, actual.Length, label); double maximum = 0;
            for (int i = 0; i < expected.Length; ++i)
            {
                Assert.That(actual[i], Is.InRange(0f, 1f), label); maximum = Math.Max(maximum, Math.Abs((double)expected[i] - actual[i]));
            }
            TestContext.WriteLine(label + ";maximum_absolute=" + maximum.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            Assert.Less(maximum, .0001, label);
        }
        internal static void CheckEvents(string label, float[] expected, MNNCueLightAnalysis actual)
        {
            Assert.AreEqual(expected.Length / 5, actual.FrameCount, label); var e = new float[actual.FrameCount * 2]; var a = new float[e.Length];
            for (int i = 0; i < actual.FrameCount; ++i)
            {
                var f = actual.Frames[i]; e[i * 2] = expected[i * 5]; e[i * 2 + 1] = expected[i * 5 + 1];
                a[i * 2] = f.SpeechProbability; a[i * 2 + 1] = f.EndOfTurnProbability;
                Assert.AreEqual((int)expected[i * 5 + 2], f.ResetCount, label + " reset " + i);
                Assert.AreEqual(expected[i * 5 + 3] == 1, f.SnappyEndpoint, label + " snappy " + i);
                Assert.AreEqual(expected[i * 5 + 4] == 1, f.CarefulEndpoint, label + " careful " + i);
            }
            Check(label, e, a);
        }
    }
}
