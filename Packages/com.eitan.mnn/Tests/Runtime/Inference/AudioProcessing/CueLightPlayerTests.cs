using System;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    [Category("NativeInference"), Category("SpeechModels")]
    public sealed class CueLightPlayerTests
    {
        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal), TestCase(MNNBackendType.Auto)]
        public void OriginalPcmSixOutputsReplayAndEndpointsMatchBuiltPlayer(MNNBackendType backend)
        {
            Assert.False(Application.isEditor, "Run in a built Player."); MNNPlatformSupport.RequireSupported();
            string root = Environment.GetEnvironmentVariable("MNN_TEST_CUELIGHT"); Assert.That(root, Is.Not.Null.And.Not.Empty);
            string directory = Path.Combine(Application.streamingAssetsPath, "MNN", "Models", "CueLight-Nano-MNN");
            string reference = Path.Combine(root, "source-graph-v237", "reference");
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
    }
}
