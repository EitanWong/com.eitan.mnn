using System;
using System.IO;
using System.Linq;
using System.Threading;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [Category("Integration"), Category("SpeechEnhancement")]
    public sealed class FullSubNetPlusSourceParityTests
    {
        private static readonly string[] Cases = { "speech", "natural_speech", "quiet", "gain2", "silence", "noise", "tone", "short", "long", "speech" };
        private static string Directory => Path.GetFullPath("Assets/StreamingAssets/MNN/Models/FullSubNetPlus-MNN");
        private static string Reference
        {
            get
            {
                string path = Environment.GetEnvironmentVariable("MNN_TEST_FULLSUBNETPLUS_REFERENCE");
                if (string.IsNullOrWhiteSpace(path)) Assert.Ignore("Set MNN_TEST_FULLSUBNETPLUS_REFERENCE to the original checkpoint oracle.");
                return path;
            }
        }
        private static float[] Read(string name)
        {
            var bytes = File.ReadAllBytes(Path.Combine(Reference, name + ".f32"));
            var values = new float[bytes.Length / 4]; Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length); return values;
        }
        private static void Check(float[] actual, float[] expected, string name)
        {
            Assert.AreEqual(expected.Length, actual.Length);
            Assert.True(actual.All(value => !float.IsNaN(value) && !float.IsInfinity(value)), name);
            double energy = expected.Sum(value => (double)value * value);
            double error = Math.Sqrt(actual.Select((value, i) => Math.Pow((double)value - expected[i], 2)).Sum() / Math.Max(1e-30, energy));
            TestContext.WriteLine(name + ": waveform_nrmse=" + error);
            Assert.Less(error, .001, name);
        }
        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal), TestCase(MNNBackendType.Auto)]
        public void WaveformsMatchOriginalCheckpointAcrossShapesAndRepeats(MNNBackendType backend)
        {
            using (var model = MNNFullSubNet.Load(Directory, 1, backend))
            {
                foreach (string name in Cases)
                {
                    var pcm = Read(name + "-input");
                    var expected = Read(name + "-enhanced");
                    Check(model.Enhance(pcm), expected, name);
                    Check(model.Enhance(pcm), expected, name + " repeated");
                }
                Assert.AreNotEqual(MNNBackendType.Auto, model.Backend);
                if (backend != MNNBackendType.Auto) Assert.AreEqual(backend, model.Backend);
            }
        }
        [Test]
        public void StudioExecutesActualEnhancementRequest()
        {
            var profile = MNNStudioModel.Read(Directory);
            Assert.True(profile.HasAudioProcessingPipeline);
            using (var backend = MNNStudioNativeBackend.Load(profile, 1, false))
            {
                var result = backend.Run(new MNNStudioRequest { Audio = Path.Combine(Reference, "speech.wav") });
                Check(result.Waveform, Read("speech-enhanced"), "Studio");
                Assert.AreEqual(16000, result.SampleRate);
                StringAssert.Contains("FullSubNet+", result.BackendDescription);
            }
        }
        [Test]
        public void InvalidInputCancellationAndDisposalHaveExplicitContracts()
        {
            var model = MNNFullSubNet.Load(Directory, 1, MNNBackendType.CPU);
            try
            {
                Assert.Throws<ArgumentException>(() => model.Enhance(new float[1791]));
                Assert.Throws<ArgumentException>(() => model.Enhance(new[] { float.NaN }));
                Assert.Throws<ArgumentException>(() => model.Enhance(new float[160001]));
                Assert.Throws<NotSupportedException>(() => MNNFullSubNet.Load(Directory, backendType: MNNBackendType.NN));
                Assert.Throws<OperationCanceledException>(() => model.Enhance(Read("speech-input"), cancellationToken: new CancellationToken(true)));
                Check(model.Enhance(Read("speech-input")), Read("speech-enhanced"), "after cancellation");
                Assert.AreEqual(1792, model.Enhance(new float[1792]).Length);
            }
            finally { model.Dispose(); }
            Assert.Throws<ObjectDisposedException>(() => model.Enhance(new float[4096]));
        }
        [Test]
        public void MeasureWarmWholeCallThreadAndBackendChoices()
        {
            var pcm = Read("speech-input"); var expected = Read("speech-enhanced");
            foreach (var backend in new[] { MNNBackendType.CPU, MNNBackendType.Metal })
            foreach (int threads in backend == MNNBackendType.CPU ? new[] { 1, 2, 4 } : new[] { 1 })
            using (var model = MNNFullSubNet.Load(Directory, threads, backend))
            {
                Check(model.Enhance(pcm), expected, "warmup");
                var timings = new double[5];
                for (int i = 0; i < timings.Length; ++i)
                {
                    var timer = System.Diagnostics.Stopwatch.StartNew(); var result = model.Enhance(pcm);
                    timer.Stop(); timings[i] = timer.Elapsed.TotalMilliseconds; Check(result, expected, "timing");
                }
                Array.Sort(timings);
                TestContext.WriteLine("backend=" + backend + "; threads=" + threads + "; samples=" + pcm.Length + "; median_ms=" + timings[2]);
            }
        }

        [Test]
        public void MeasureAlternatingShapesAgainstOriginalWaveforms()
        {
            var names = new[] { "speech", "natural_speech", "short", "speech", "natural_speech", "short" };
            var inputs = names.Select(name => Read(name + "-input")).ToArray();
            var expected = names.Select(name => Read(name + "-enhanced")).ToArray();
            foreach (var backend in new[] { MNNBackendType.CPU, MNNBackendType.Metal })
            foreach (int threads in backend == MNNBackendType.CPU ? new[] { 1, 2, 4 } : new[] { 1 })
            using (var model = MNNFullSubNet.Load(Directory, threads, backend))
            {
                var timings = new double[names.Length];
                for (int i = 0; i < names.Length; ++i)
                {
                    var timer = System.Diagnostics.Stopwatch.StartNew();
                    var actual = model.Enhance(inputs[i]);
                    timer.Stop(); timings[i] = timer.Elapsed.TotalMilliseconds;
                    Check(actual, expected[i], names[i]);
                }
                TestContext.WriteLine("alternating_backend=" + backend + "; threads=" + threads +
                    "; cases=" + string.Join(",", names) + "; milliseconds=" + string.Join(",", timings));
            }
        }
    }
}
