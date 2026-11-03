using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MNN.Unity;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [Explicit("Uses the installed Silero model; copies only test fixtures into TestArtifacts~. Never downloads.")]
    public sealed class SileroModelReuseTests
    {
        private string _directory;

        [SetUp]
        public void CopyInstalledModel()
        {
            string source = Environment.GetEnvironmentVariable("MNN_TEST_SILERO_MODEL");
            string artifacts = Environment.GetEnvironmentVariable("MNN_TEST_ARTIFACTS");
            Assert.That(Directory.Exists(source), "Set MNN_TEST_SILERO_MODEL.");
            Assert.That(Directory.Exists(artifacts), "Set MNN_TEST_ARTIFACTS under TestArtifacts~.");
            _directory = Path.Combine(artifacts, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            foreach (string name in new[] { MNNSileroVadConfig.FileName, "silero_v5_16k_fp32.mnn" })
                File.Copy(Path.Combine(source, name), Path.Combine(_directory, name));
        }

        [TearDown]
        public void RemoveOwnedFixture() { if (_directory != null) Directory.Delete(_directory, true); }

        [TestCase(false), TestCase(true)]
        public void RealtimeAndQualityVadShareOneModelWithIndependentStateAndLifetime(bool legacy)
        {
            string directory = legacy ? Environment.GetEnvironmentVariable("MNN_TEST_SILERO_LEGACY") : _directory;
            if (!Directory.Exists(directory)) Assert.Ignore("Set MNN_TEST_SILERO_LEGACY to verify the existing legacy installation.");
            var pcm = Signal();
            object initial;
            using (var quality = MNNSileroVad.Load(directory, backendType: MNNBackendType.CPU))
            using (var realtime = MNNRealtimeVoiceActivity.Load(Path.Combine(directory, ".")))
            {
                Assert.AreSame(Runner(quality), Runner(realtime), "Same model must have one loaded graph/session.");
                initial = Runner(quality);
                var expected = quality.Detect(pcm, 16000).Probabilities;
                for (int frame = 0; frame < expected.Count; ++frame)
                {
                    // A separate recording resets its own neural state between realtime frames.
                    quality.Detect(new float[1024], 16000);
                    Assert.That(realtime.ProcessFrame(pcm, frame * 512).SpeechProbability,
                        Is.EqualTo(expected[frame]).Within(.000001));
                }
                quality.Dispose();
                realtime.Reset();
                Assert.AreEqual(0, realtime.ProcessedSamples);
                Assert.That(realtime.ProcessFrame(pcm, 0).SpeechProbability, Is.EqualTo(expected[0]).Within(.000001));
                realtime.Dispose();
            }
            using (var reloaded = MNNSileroVad.Load(directory))
            {
                Assert.AreNotSame(initial, Runner(reloaded), "No unused native graph may remain cached after the last owner exits.");
                Assert.AreEqual(pcm.Length / 512, reloaded.Detect(pcm, 16000).Probabilities.Count);
            }
        }

        [Test]
        public void ConcurrentAcquisitionLoadsOneModelAndKeepsStreamsIndependent()
        {
            var consumers = Task.WhenAll(Task.Run(() => MNNRealtimeVoiceActivity.Load(_directory)),
                Task.Run(() => MNNRealtimeVoiceActivity.Load(_directory, threshold: .9f))).GetAwaiter().GetResult();
            try
            {
                Assert.AreSame(Runner(consumers[0]), Runner(consumers[1]));
                Assert.AreEqual(.5f, consumers[0].Threshold);
                Assert.AreEqual(.9f, consumers[1].Threshold);
                var pcm = Signal();
                using (var quality = MNNSileroVad.Load(_directory))
                {
                    var recording = Task.Run(() => quality.Detect(pcm, 16000));
                    var results = Task.WhenAll(Task.Run(() => Run(consumers[0], pcm)), Task.Run(() => Run(consumers[1], pcm))).GetAwaiter().GetResult();
                    CollectionAssert.AreEqual(results[0], results[1]);
                    CollectionAssert.AreEqual(results[0], recording.GetAwaiter().GetResult().Probabilities);
                }
                consumers[0].Reset();
                Assert.AreEqual(pcm.Length, consumers[1].ProcessedSamples);
            }
            finally { foreach (var consumer in consumers) consumer.Dispose(); }
        }

        [Test]
        public void DifferentModelsOrExecutionSettingsDoNotShareAndFailedLoadCanRetry()
        {
            string copy = Path.Combine(_directory, "other");
            Directory.CreateDirectory(copy);
            foreach (string name in new[] { MNNSileroVadConfig.FileName, "silero_v5_16k_fp32.mnn" })
                File.Copy(Path.Combine(_directory, name), Path.Combine(copy, name));
            using (var first = MNNSileroVad.Load(_directory))
            using (var different = MNNSileroVad.Load(copy))
            using (var threads = MNNSileroVad.Load(_directory, 2))
            {
                Assert.AreNotSame(Runner(first), Runner(different));
                Assert.AreNotSame(Runner(first), Runner(threads));
                string profile = Path.Combine(_directory, MNNSileroVadConfig.FileName);
                string original = File.ReadAllText(profile);
                File.WriteAllText(profile, "{\"repository\":\"invalid\"}");
                Assert.Throws<InvalidDataException>(() => MNNSileroVad.Load(_directory));
                File.WriteAllText(profile, original);
                using (var retry = MNNSileroVad.Load(_directory)) Assert.AreSame(Runner(first), Runner(retry));
                string graph = Path.Combine(_directory, "silero_v5_16k_fp32.mnn");
                byte[] bytes = File.ReadAllBytes(graph);
                bytes[bytes.Length - 1] ^= 1;
                File.WriteAllBytes(graph, bytes);
                Assert.Throws<InvalidDataException>(() => MNNSileroVad.Load(_directory), "A cache hit cannot hide a replaced/corrupt graph.");
                bytes[bytes.Length - 1] ^= 1;
                File.WriteAllBytes(graph, bytes);
                using (var retry = MNNSileroVad.Load(_directory)) Assert.AreSame(Runner(first), Runner(retry));
            }
        }

        private static object Runner(object owner) => owner.GetType().GetField("_runner", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
        [Test]
        public void PreparedSharedRealtimeInferenceAllocatesNoManagedBytes()
        {
            long probeStart = GC.GetAllocatedBytesForCurrentThread();
            var probe = new byte[1024];
            GC.KeepAlive(probe);
            if (GC.GetAllocatedBytesForCurrentThread() <= probeStart)
                Assert.Ignore("This Unity runtime has no working per-thread allocation counter; zero returned bytes is not evidence of zero allocations.");
            using (var quality = MNNSileroVad.Load(_directory))
            using (var realtime = MNNRealtimeVoiceActivity.Load(_directory))
            {
                var frame = new float[512];
                quality.Detect(new float[1024], 16000);
                for (int i = 0; i < 16; ++i) realtime.ProcessFrame(frame, 0);
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 32; ++i) realtime.ProcessFrame(frame, 0);
                Assert.AreEqual(0, GC.GetAllocatedBytesForCurrentThread() - before);
            }
        }

        private static float[] Signal()
        {
            string path = Environment.GetEnvironmentVariable("MNN_TEST_SILERO_PCM");
            if (!string.IsNullOrWhiteSpace(path))
            {
                var bytes = File.ReadAllBytes(path);
                Assert.AreEqual(0, bytes.Length % sizeof(float));
                int length = Math.Min(48000, bytes.Length / sizeof(float)) / 512 * 512;
                Assert.GreaterOrEqual(length, 512);
                var recording = new float[length];
                Buffer.BlockCopy(bytes, 0, recording, 0, length * sizeof(float));
                return recording;
            }
            var pcm = new float[512 * 20];
            for (int i = 0; i < pcm.Length; ++i) pcm[i] = .08f * (float)Math.Sin(i * .07) * (i % 1024 / 1024f);
            return pcm;
        }
        private static float[] Run(MNNRealtimeVoiceActivity model, float[] pcm)
        {
            var result = new float[pcm.Length / 512];
            for (int i = 0; i < result.Length; ++i) result[i] = model.ProcessFrame(pcm, i * 512).SpeechProbability;
            return result;
        }
    }
}
