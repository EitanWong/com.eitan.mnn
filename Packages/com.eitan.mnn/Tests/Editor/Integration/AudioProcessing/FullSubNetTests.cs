using System;
using System.IO;
using System.Linq;
using System.Threading;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class FullSubNetTests
    {
        private static string ModelDirectory => Path.GetFullPath("Assets/StreamingAssets/MNN/Models/FullSubNet-MNN");
        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal)]
        [Explicit("Requires frozen official FullSubNet checkpoint outputs; compares variable-length source masks.")]
        public void MasksMatchUnmodifiedPyTorchAcrossShapes(MNNBackendType backend)
        {
            if (!MNNAcceleration.IsBackendAvailable(backend)) Assert.Ignore(backend + " unavailable.");
            using (var model = MNNFullSubNet.Load(ModelDirectory, 1, backend))
            {
                foreach (string name in new[]{"speech_1", "speech_0.001", "speech_0.01", "speech_0.1", "speech_2", "tone", "noise", "silence", "long", "short", "speech_1"})
                {
                    var features = Read(name, "features");
                    var actual = model.PredictMask(features, features.Length / 257);
                    TestContext.WriteLine(backend + " mask " + name);
                    Assert.That(Error(actual, Read(name, "mask")), Is.LessThan(.002));
                }
                Assert.AreEqual(backend, model.Backend);
            }
        }
        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal)]
        [Explicit("Requires source waveform references, including quiet input and long/short reuse.")]
        public void WaveformsMatchOriginalAndRetainInputCount(MNNBackendType backend)
        {
            if (!MNNAcceleration.IsBackendAvailable(backend)) Assert.Ignore(backend + " unavailable.");
            using (var model = MNNFullSubNet.Load(ModelDirectory, 1, backend))
            {
                foreach (string name in new[]{"speech_1", "speech_0.001", "speech_0.01", "speech_0.1", "speech_2", "tone", "noise", "silence", "long", "short", "speech_1"})
                {
                    TestContext.WriteLine(backend + " waveform " + name);
                    Assert.That(Error(model.Enhance(Read(name, "input")), Read(name, "enhanced")), Is.LessThan(.002));
                }
                Assert.That(model.Enhance(new float[11063], 22050).Length, Is.EqualTo(11063));
                Assert.That(model.Enhance(new float[1]).Length, Is.EqualTo(1));
                Assert.AreEqual(backend, model.Backend);
            }
        }
        [Test, Explicit("Requires a complete profile for Studio execution.")]
        public void StudioEnhancesWaveform()
        {
            var profile = MNNStudioModel.Read(ModelDirectory);
            Assert.True(profile.HasAudioProcessingPipeline);
            string path = Path.GetFullPath("TestArtifacts~/fullsubnet-studio.wav");
            File.WriteAllBytes(path, MNNWaveAudio.EncodePcm16(Read("speech_1", "input"), 16000));
            try
            {
                using (var backend = MNNStudioNativeBackend.Load(profile, 1, false))
                {
                    var actual = backend.Run(new MNNStudioRequest{Audio=path});
                    Assert.That(Error(actual.Waveform, Read("speech_1", "enhanced")), Is.LessThan(.003));
                    Assert.That(actual.BackendDescription, Does.StartWith("FullSubNet"));
                }
            }
            finally { File.Delete(path); }
        }
        [Test, Explicit("Requires the verified graph to check cancellation recovery and boundary rejection.")]
        public void RejectsInvalidInputAndRecoversAfterCancellation()
        {
            using (var model = MNNFullSubNet.Load(ModelDirectory))
            {
                Assert.Throws<ArgumentException>(() => model.Enhance(new[]{float.NaN}));
                Assert.Throws<ArgumentException>(() => model.Enhance(new float[160001]));
                Assert.Throws<ArgumentException>(() => model.PredictMask(new[]{-1f}, 2));
                Assert.Throws<OperationCanceledException>(() => model.Enhance(Read("speech_1", "input"), cancellationToken:new CancellationToken(true)));
                Assert.That(Error(model.Enhance(Read("speech_1", "input")), Read("speech_1", "enhanced")), Is.LessThan(.002));
                model.Dispose();
                Assert.Throws<ObjectDisposedException>(() => model.Enhance(new float[257]));
            }
        }
        [TestCase(MNNBackendType.CPU,1), TestCase(MNNBackendType.CPU,2), TestCase(MNNBackendType.CPU,4), TestCase(MNNBackendType.Metal,1)]
        [Explicit("Measures warm end-to-end enhancement and verifies every timed output.")]
        public void MeasuresVerifiedWarmLatency(MNNBackendType backend, int threads)
        {
            if (!MNNAcceleration.IsBackendAvailable(backend)) Assert.Ignore("Backend unavailable.");
            using (var model = MNNFullSubNet.Load(ModelDirectory, threads, backend))
            {
                var input = Read("speech_1", "input");
                var expected = Read("speech_1", "enhanced");
                model.Enhance(input);
                var times = new double[5];
                for (int i = 0; i < times.Length; ++i)
                {
                    var timer = System.Diagnostics.Stopwatch.StartNew();
                    var output = model.Enhance(input);
                    timer.Stop();
                    times[i] = timer.Elapsed.TotalMilliseconds;
                    Assert.That(Error(output,expected),Is.LessThan(.002));
                }
                Array.Sort(times);
                TestContext.WriteLine(backend + " threads=" + threads + " median_ms=" + times[2]);
            }
        }
        internal static float[] Read(string name, string part)
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_FULLSUBNET_REFERENCE");
            if (string.IsNullOrWhiteSpace(root)) Assert.Ignore("Set MNN_TEST_FULLSUBNET_REFERENCE; no downloads.");
            var bytes = File.ReadAllBytes(Path.Combine(root, "conditions", name + "-" + part + ".f32"));
            var result = new float[bytes.Length / 4];Buffer.BlockCopy(bytes,0,result,0,bytes.Length);return result;
        }
        private static double Error(float[] actual, float[] expected)
        {
            Assert.AreEqual(expected.Length,actual.Length);
            double value=Math.Sqrt(actual.Select((x,i)=>Math.Pow((double)x-expected[i],2)).Sum()/Math.Max(1e-30,expected.Sum(x=>(double)x*x)));
            TestContext.WriteLine("NRMSE="+value);return value;
        }
    }
}
