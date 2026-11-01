using System;
using System.IO;
using System.Linq;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [Category("Integration"), Category("AvConvTasNet")]
    public sealed class AvConvTasNetSourceParityTests
    {
        private static string Directory => Path.GetFullPath("Assets/StreamingAssets/MNN/Models/AV-ConvTasNet-LRS2-16K-MNN");
        private static string Reference => Environment.GetEnvironmentVariable("MNN_TEST_AV_CONVTASNET_REFERENCE")
            ?? throw new InvalidOperationException("Set MNN_TEST_AV_CONVTASNET_REFERENCE to the pinned author oracle.");
        private static readonly string[] Cases = { "minimum", "encoder_before", "encoder_at", "encoder_after", "segment_before", "segment_at", "segment_after", "short", "noise", "long", "real", "quiet", "gain2", "silence", "constant", "shifted_visual", "mean_visual", "maximum", "receptive_before", "receptive_at", "receptive_after" };
        private static float[] Read(string name)
        {
            var bytes = File.ReadAllBytes(Path.Combine(Reference, name + ".f32"));
            var values = new float[bytes.Length / 4]; Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length); return values;
        }
        private static void Check(string name, float[] actual)
        {
            var expected = Read(name + "-target"); Assert.AreEqual(expected.Length, actual.Length, name);
            double energy = 0, error = 0;
            for (int i = 0; i < actual.Length; ++i)
            {
                Assert.False(float.IsNaN(actual[i]) || float.IsInfinity(actual[i]), name);
                energy += (double)expected[i] * expected[i]; double delta = actual[i] - expected[i]; error += delta * delta;
            }
            double nrmse = Math.Sqrt(error / (energy == 0 ? 1 : energy));
            TestContext.WriteLine(name + ";nrmse=" + nrmse); Assert.Less(nrmse, .001, name);
        }
        private static float[] Run(MNNAvConvTasNetExtraction model, string name)
        {
            var visual = Read(name + "-visual"); return model.Extract(Read(name + "-input"), visual, visual.Length / (112 * 112));
        }
        [Test]
        public void PublicApiHasCompleteTargetExtraction()
        {
            var type = typeof(MNNAvConvTasNetExtraction); Assert.NotNull(type.GetMethod("Extract")); Assert.NotNull(type.GetMethod("ExtractWaveFile"));
        }
        [Test]
        public void StudioDiscoversCompleteTargetPipelineAndRequestsVisualFrames()
        {
            var model = MNNStudioModel.Read(Directory); Assert.True(model.HasAudioProcessingPipeline); Assert.True(model.RequiresVisualFrames); Assert.True(model.IsAvConvTasNet); Assert.True(model.RequiresExactVisualFrames);
        }
        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal), TestCase(MNNBackendType.Auto)]
        public void CompleteTargetWaveformsMatchOriginalAuthor(MNNBackendType backend)
        {
            using (var model = MNNAvConvTasNetExtraction.Load(Directory, 4, backend))
            {
                foreach (string name in Cases.Concat(new[] { "minimum" }))
                    for (int repeat = 0; repeat < 2; ++repeat) Check(name, Run(model, name));
                Assert.AreEqual(backend == MNNBackendType.Auto ? MNNBackendType.Metal : backend, model.Backend);
                Assert.AreEqual(backend == MNNBackendType.Auto ? MNNBackendType.Metal : backend, model.AudioBackend);
            }
        }
        [Test]
        public void OneInputResizeRetainsBothMetalStages()
        {
            using (var model = MNNAvConvTasNetExtraction.Load(Directory, 4, MNNBackendType.Metal))
            {
                foreach (string name in new[] { "minimum", "encoder_before", "encoder_at", "minimum" })
                {
                    Check(name, Run(model, name));
                    Assert.AreEqual(MNNBackendType.Metal, model.Backend, name);
                    Assert.AreEqual(MNNBackendType.Metal, model.AudioBackend, name);
                }
            }
        }
        [Test]
        public void AutoUsesMetalForVariableDurationRecordings()
        {
            using (var model = MNNAvConvTasNetExtraction.Load(Directory))
            {
                Assert.AreEqual(MNNBackendType.Metal, model.Backend);
                Assert.AreEqual(MNNBackendType.Metal, model.AudioBackend);
            }
        }
        [Test]
        public void StudioRunsRealAudioAndSynchronizedCropsAndCancellation()
        {
            using (var backend = MNNStudioNativeBackend.Load(MNNStudioModel.Read(Directory), 4, false))
            {
                var visual = Read("real-visual"); string path = Path.Combine(Reference, "real.wav");
                var result = backend.Run(new MNNStudioRequest { Audio = path, VisualFrames = visual });
                Check("real", result.Waveform); Assert.AreEqual(16000, result.SampleRate);
                Assert.Throws<ArgumentException>(() => backend.Run(new MNNStudioRequest { Audio = path }));
                Assert.Throws<OperationCanceledException>(() => backend.Run(new MNNStudioRequest { Audio = path, VisualFrames = visual, Cancellation = new System.Threading.CancellationToken(true) }));
            }
        }
        [Test]
        public void MeasureWholeCallsAndAlternatingLengths()
        {
            foreach (int threads in new[] { 1, 2, 4 }) Measure(MNNBackendType.CPU, threads);
            Measure(MNNBackendType.Metal, 4);
        }
        private static void Measure(MNNBackendType backend, int threads)
        {
            using (var model = MNNAvConvTasNetExtraction.Load(Directory, threads, backend))
            {
                var pcm = Read("real-input"); var visual = Read("real-visual"); int count = visual.Length / (112 * 112);
                model.Extract(pcm, visual, count); var timings = new double[5];
                for (int i = 0; i < timings.Length; ++i)
                {
                    var timer = System.Diagnostics.Stopwatch.StartNew(); var actual = model.Extract(pcm, visual, count); timer.Stop();
                    timings[i] = timer.Elapsed.TotalMilliseconds; Check("real", actual);
                }
                Array.Sort(timings); TestContext.WriteLine("backend=" + backend + ";threads=" + threads + ";warm_ms=" + timings[2]);
                foreach (string name in new[] { "real", "minimum", "long", "maximum", "real" })
                {
                    var input = Read(name + "-input"); var frames = Read(name + "-visual"); var timer = System.Diagnostics.Stopwatch.StartNew();
                    var actual = model.Extract(input, frames, frames.Length / (112 * 112)); timer.Stop(); Check(name, actual);
                    TestContext.WriteLine("alternating_backend=" + backend + ";threads=" + threads + ";case=" + name + ";ms=" + timer.Elapsed.TotalMilliseconds);
                }
            }
        }
        [TestCase(MNNBackendType.CUDA), TestCase(MNNBackendType.OpenCL), TestCase(MNNBackendType.NN), TestCase(MNNBackendType.OpenGL), TestCase(MNNBackendType.Vulkan)]
        public void UnverifiedAcceleratorsAreRejected(MNNBackendType backend) => Assert.Throws<NotSupportedException>(() => MNNAvConvTasNetExtraction.Load(Directory, backendType: backend));
        [Test]
        public void OriginalDurationAlignmentSurvivesResamplingBoundary()
        {
            using (var model = MNNAvConvTasNetExtraction.Load(Directory, 1, MNNBackendType.CPU))
            {
                // 3839 samples at48kHz have one complete25FPS frame. Resampling
                // rounds up to1280 samples, which must not invent a second frame.
                var output = model.Extract(new float[3839], new float[112 * 112], 1, 48000);
                Assert.AreEqual(3839, output.Length);
                foreach (float sample in output) Assert.AreEqual(0, sample);
            }
        }
        [Test]
        public void AlignmentBoundsFiniteCancellationAndDisposalAreExplicit()
        {
            var model = MNNAvConvTasNetExtraction.Load(Directory, 1, MNNBackendType.CPU); var frames = new float[112 * 112];
            try
            {
                Assert.Throws<ArgumentException>(() => model.Extract(new float[639], frames, 1));
                Assert.Throws<ArgumentException>(() => model.Extract(new float[96001], frames, 1));
                Assert.Throws<ArgumentException>(() => model.Extract(new float[1280], frames, 1));
                Assert.Throws<ArgumentException>(() => model.Extract(new float[640], frames, 2));
                Assert.Throws<ArgumentOutOfRangeException>(() => model.Extract(new float[640], frames, 1, 7999));
                var invalid = new float[640]; invalid[1] = float.NaN;
                Assert.Throws<ArgumentException>(() => model.Extract(invalid, frames, 1));
                frames[0] = float.PositiveInfinity;
                Assert.Throws<ArgumentException>(() => model.Extract(new float[640], frames, 1)); frames[0] = 0;
                Assert.Throws<OperationCanceledException>(() => model.Extract(new float[640], frames, 1, cancellationToken: new System.Threading.CancellationToken(true)));
            }
            finally { model.Dispose(); }
            Assert.Throws<ObjectDisposedException>(() => model.Extract(new float[640], frames, 1));
        }
    }
}
