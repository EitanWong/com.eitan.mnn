using System;
using System.IO;
using System.Linq;
using System.Threading;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [Category("Integration"), Category("Seg")]
    public sealed class SegSourceParityTests
    {
        private static string Directory => Path.GetFullPath("Assets/StreamingAssets/MNN/Models/SEG-YGD-16K-MNN");
        private static string Reference => Environment.GetEnvironmentVariable("MNN_TEST_SEG_REFERENCE")
            ?? throw new InvalidOperationException("Set MNN_TEST_SEG_REFERENCE to the pinned author oracle.");
        [Serializable] private sealed class Case { public string @case = ""; }
        [Serializable] private sealed class Cases { public Case[] cases = null; }
        private static string[] CaseNames => UnityEngine.JsonUtility.FromJson<Cases>(File.ReadAllText(Path.Combine(Reference, "cases.json"))).cases.Select(c => c.@case).ToArray();
        private static float[] Read(string name)
        {
            var bytes = File.ReadAllBytes(Path.Combine(Reference, name + ".f32"));
            var values = new float[bytes.Length / 4]; Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length); return values;
        }
        private static MNNSegGestureFeatures Gestures(string name) => new MNNSegGestureFeatures(Read(name + "-gesture"));
        private static void Check(string name, float[] actual)
        {
            var expected = Read(name + "-target"); Assert.AreEqual(expected.Length, actual.Length, name);
            double error = 0, energy = 0;
            for (int i = 0; i < expected.Length; ++i)
            {
                Assert.False(float.IsNaN(actual[i]) || float.IsInfinity(actual[i]), name);
                double delta = actual[i] - expected[i]; error += delta * delta; energy += (double)expected[i] * expected[i];
            }
            double nrmse = Math.Sqrt(error / (energy == 0 ? 1 : energy));
            TestContext.WriteLine(name + ";nrmse=" + nrmse); Assert.Less(nrmse, .001, name);
        }
        [Test]
        public void PublicApiAcceptsSynchronizedGestureFeatures()
        {
            Assert.NotNull(typeof(MNNSegExtraction).GetMethod("Extract"));
            Assert.AreEqual(15, MNNSegGestureFeatures.FramesPerSecond); Assert.AreEqual(30, MNNSegGestureFeatures.FeaturesPerFrame);
        }
        [Test]
        public void CatalogueAndStudioDiscoverGesturePipeline()
        {
            Assert.AreEqual("AudioProcessing", MNNModelRepository.Classify("owner/SEG-YGD-16K-MNN"));
            var model = MNNStudioModel.Read(Directory);
            Assert.True(model.HasAudioProcessingPipeline); Assert.True(model.IsSeg); Assert.False(model.RequiresVisualFrames); Assert.False(model.IsSpExPlus);
        }
        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Auto)]
        public void CompleteWaveformsMatchOriginalAuthorAcrossShapes(MNNBackendType backend)
        {
            using (var model = MNNSegExtraction.Load(Directory, 4, backend))
            {
                Assert.AreEqual(37, CaseNames.Length);
                foreach (string name in CaseNames.Concat(new[] { CaseNames[0] }))
                {
                    var input = Read(name + "-input"); var gestures = Gestures(name);
                    for (int repeat = 0; repeat < 2; ++repeat) Check(name, model.Extract(input, gestures));
                    Assert.AreEqual(MNNBackendType.CPU, model.Backend);
                }
            }
        }
        [Test]
        public void GestureOwnershipAndJsonContractAreExplicit()
        {
            var values = Read("real-gesture"); var gestures = new MNNSegGestureFeatures(values);
            float expected = values[0]; values[0] = float.NaN;
            Assert.AreEqual(expected, gestures.ToArray()[0]); var exposed = gestures.ToArray(); exposed[0] = float.NaN;
            Assert.AreEqual(expected, gestures.ToArray()[0]); Assert.AreEqual(45, gestures.FrameCount);
            var parsed = MNNSegGestureFeatures.ReadJson(Path.Combine(Reference, "real-gesture.json"));
            CollectionAssert.AreEqual(gestures.ToArray(), parsed.ToArray());
            Assert.Throws<ArgumentException>(() => new MNNSegGestureFeatures(new float[29]));
            Assert.Throws<ArgumentException>(() => new MNNSegGestureFeatures(new float[31]));
            Assert.Throws<ArgumentException>(() => new MNNSegGestureFeatures(new float[4530]));
            Assert.Throws<ArgumentException>(() => new MNNSegGestureFeatures(values));
            Assert.Throws<OperationCanceledException>(() => MNNSegGestureFeatures.ReadJson(Path.Combine(Reference, "real-gesture.json"), new CancellationToken(true)));
        }
        [Test]
        public void StudioRunsAudioAndGestureJsonWithNormalizedPlayback()
        {
            using (var backend = MNNStudioNativeBackend.Load(MNNStudioModel.Read(Directory), 4, false))
            {
                var request = new MNNStudioRequest { Audio = Path.Combine(Reference, "real.wav"), GestureFeatures = Path.Combine(Reference, "real-gesture.json") };
                var result = backend.Run(request); Assert.AreEqual(16000, result.SampleRate);
                var expected = Read("real-target"); float peak = expected.Max(v => Math.Abs(v)); float scale = peak > 1 ? .95f / peak : 1;
                double error = 0, energy = 0; Assert.AreEqual(expected.Length, result.Waveform.Length);
                for (int i = 0; i < expected.Length; ++i)
                {
                    double value = expected[i] * scale; double delta = result.Waveform[i] - value; error += delta * delta; energy += value * value;
                }
                Assert.Less(Math.Sqrt(error / energy), .001); Assert.LessOrEqual(result.Waveform.Max(v => Math.Abs(v)), 1f);
                Assert.Throws<ArgumentException>(() => backend.Run(new MNNStudioRequest { Audio = request.Audio }));
                request.Cancellation = new CancellationToken(true); Assert.Throws<OperationCanceledException>(() => backend.Run(request));
            }
        }
        [Test]
        public void MeasureCompleteCallsAndAlternatingDurations()
        {
            foreach (int threads in new[] { 1, 2, 4, 8 })
            using (var model = MNNSegExtraction.Load(Directory, threads, MNNBackendType.CPU))
            {
                foreach (string name in new[] { "real", "long" })
                {
                    var input = Read(name + "-input"); var gestures = Gestures(name); model.Extract(input, gestures); var times = new double[5];
                    for (int i = 0; i < times.Length; ++i)
                    {
                        var watch = System.Diagnostics.Stopwatch.StartNew(); var output = model.Extract(input, gestures); watch.Stop();
                        times[i] = watch.Elapsed.TotalMilliseconds; Check(name, output);
                    }
                    Array.Sort(times); TestContext.WriteLine("threads=" + threads + ";case=" + name + ";warm_ms=" + times[2]);
                }
                foreach (string name in new[] { "minimum", "long", "segment_at", "real", "dc_maximum", "minimum" })
                {
                    var input = Read(name + "-input"); var gestures = Gestures(name); var watch = System.Diagnostics.Stopwatch.StartNew();
                    var output = model.Extract(input, gestures); watch.Stop(); Check(name, output);
                    TestContext.WriteLine("alternating_threads=" + threads + ";case=" + name + ";ms=" + watch.Elapsed.TotalMilliseconds);
                }
            }
        }
        [Test]
        public void MeasureInterleavedThreadsAndDurations()
        {
            var models = new System.Collections.Generic.Dictionary<int, MNNSegExtraction>();
            try
            {
                foreach (int threads in new[] { 1, 2, 4, 8 }) models.Add(threads, MNNSegExtraction.Load(Directory, threads, MNNBackendType.CPU));
                int[][] orders = { new[] { 4, 1, 8, 2 }, new[] { 2, 8, 1, 4 }, new[] { 1, 4, 2, 8 } };
                for (int round = 0; round < orders.Length; ++round)
                foreach (int threads in orders[round])
                foreach (string name in new[] { "real", "long" })
                {
                    var input = Read(name + "-input"); var gestures = Gestures(name); var model = models[threads];
                    model.Extract(input, gestures); var times = new double[5];
                    for (int i = 0; i < times.Length; ++i)
                    {
                        var watch = System.Diagnostics.Stopwatch.StartNew(); var output = model.Extract(input, gestures); watch.Stop();
                        times[i] = watch.Elapsed.TotalMilliseconds; Check(name, output);
                    }
                    Array.Sort(times); TestContext.WriteLine("round=" + round + ";threads=" + threads + ";case=" + name + ";warm_ms=" + times[2]);
                }
            }
            finally { foreach (var model in models.Values) model.Dispose(); }
        }
        [TestCase(MNNBackendType.Metal), TestCase(MNNBackendType.NN), TestCase(MNNBackendType.CUDA), TestCase(MNNBackendType.OpenCL), TestCase(MNNBackendType.OpenGL), TestCase(MNNBackendType.Vulkan)]
        public void UnverifiedAcceleratorsAreRejected(MNNBackendType backend) => Assert.Throws<NotSupportedException>(() => MNNSegExtraction.Load(Directory, backendType: backend));
        [Test]
        public void BoundsResamplingCancellationAndDisposalAreExplicit()
        {
            var model = MNNSegExtraction.Load(Directory, 1, MNNBackendType.CPU); var oneFrame = new MNNSegGestureFeatures(new float[30]);
            try
            {
                Assert.Throws<ArgumentException>(() => model.Extract(new float[1066], oneFrame));
                Assert.Throws<ArgumentException>(() => model.Extract(new float[160001], oneFrame));
                Assert.Throws<ArgumentException>(() => model.Extract(new float[2134], oneFrame));
                Assert.Throws<ArgumentNullException>(() => model.Extract(new float[1067], null));
                var invalid = new float[1067]; invalid[0] = float.NaN;
                Assert.Throws<ArgumentException>(() => model.Extract(invalid, oneFrame));
                Assert.Throws<ArgumentOutOfRangeException>(() => model.Extract(new float[1067], oneFrame, 7999));
                Assert.Throws<OperationCanceledException>(() => model.Extract(new float[1067], oneFrame, cancellationToken: new CancellationToken(true)));
                var restored = model.Extract(new float[3201], oneFrame, 48000); Assert.AreEqual(3201, restored.Length); Assert.True(restored.All(v => !float.IsNaN(v) && !float.IsInfinity(v)));
                var tail = model.Extract(Read("segment_after-input"), Gestures("segment_after")); Assert.AreEqual(0, tail[tail.Length - 1]);
            }
            finally { model.Dispose(); }
            Assert.Throws<ObjectDisposedException>(() => model.Extract(new float[1067], oneFrame)); model.Dispose();
        }
    }
}
