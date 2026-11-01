using System;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using MNN.Unity.Editor;

namespace MNN.Unity.Tests
{
    public sealed class MarbleNetParityTests
    {
        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal)]
        [Explicit("Measures repeated full-recording VAD and verifies Studio profile discovery.")]
        public void WarmLatencyAndStudioContract(MNNBackendType backend)
        {
            if (!MNNAcceleration.IsBackendAvailable(backend)) Assert.Ignore(backend + " unavailable.");
            string directory = Path.GetFullPath("Assets/StreamingAssets/MNN/Models/MarbleNet-VAD-MNN");
            using (var model = MNNMarbleNetVad.Load(directory, 1, backend))
            {
                var pcm = Read("en-input");
                model.Detect(pcm, 16000);
                var timings = new double[5];
                for (int i = 0; i < timings.Length; ++i)
                {
                    var timer = System.Diagnostics.Stopwatch.StartNew();
                    var actual = model.Detect(pcm, 16000).Probabilities;
                    timings[i] = timer.Elapsed.TotalMilliseconds;
                    Assert.That(actual.Count, Is.EqualTo(Read("en-probabilities").Length));
                }
                Array.Sort(timings);
                TestContext.WriteLine(backend + " warm whole-recording median ms=" + timings[2]);
            }
            var studio = MNNStudioModel.Read(directory);
            Assert.True(studio.HasAudioProcessingPipeline);
            string path = Path.GetFullPath("TestArtifacts~/marblenet-studio.wav");
            File.WriteAllBytes(path, MNNWaveAudio.EncodePcm16(Read("en-input"), 16000));
            try
            {
                using (var session = MNNStudioNativeBackend.Load(studio, 1, false))
                {
                    var result = session.Run(new MNNStudioRequest{Audio=path});
                    Assert.That(result.VoiceActivity.Probabilities.Count, Is.EqualTo(Read("en-probabilities").Length));
                }
            }
            finally { File.Delete(path); }
        }

        [Test, Explicit("Requires the unmodified NeMo class and checkpoint-buffer frontend oracle.")]
        public void FrontendMatchesFrozenNemo()
        {
            foreach (string name in new[]{"en", "zh_en", "silence", "tone", "noise", "short"})
            {
                var actual = MNNMarbleNetFeatures.Extract(Read(name + "-input"), out int frames, default);
                var expected = Read(name + "-features");
                Assert.AreEqual(expected.Length, frames * 80);
                double error = expected.Select((value, i) => Math.Abs(value - actual[i])).Max();
                TestContext.WriteLine(name + " frontend max=" + error);
                Assert.That(error, Is.LessThan(.002), name);
            }
        }

        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal)]
        [Explicit("Requires frozen NVIDIA NeMo frontend buffers and independent ONNX posterior references.")]
        public void NaturalSpeechAndPhysicalSignalsMatchNemoOnnx(MNNBackendType backend)
        {
            if (!MNNAcceleration.IsBackendAvailable(backend)) Assert.Ignore(backend + " unavailable.");
            using (var model = MNNMarbleNetVad.Load(Path.GetFullPath("Assets/StreamingAssets/MNN/Models/MarbleNet-VAD-MNN"), 1, backend))
            {
                foreach (string name in new[]{"en", "zh_en", "silence", "tone", "noise", "short", "en"})
                {
                    var actual = model.Detect(Read(name + "-input"), 16000).Probabilities;
                    var expected = Read(name + "-probabilities");
                    Assert.AreEqual(expected.Length, actual.Count, name + " frame cadence");
                    double error = expected.Select((value, i) => Math.Abs(value - actual[i])).Max();
                    TestContext.WriteLine(backend + " " + name + " posterior max error=" + error);
                    Assert.That(error, Is.LessThan(.002), name);
                }
                Assert.AreEqual(backend, model.Backend);
                Assert.Throws<OperationCanceledException>(() => model.Detect(Read("en-input"), 16000, cancellationToken: new CancellationToken(true)));
                Assert.That(model.Detect(Read("en-input"), 16000).Probabilities.Count, Is.EqualTo(Read("en-probabilities").Length));
            }
        }

        internal static float[] Read(string name)
        {
            string directory = Environment.GetEnvironmentVariable("MNN_TEST_MARBLENET_REFERENCE");
            if (string.IsNullOrWhiteSpace(directory)) Assert.Ignore("Set MNN_TEST_MARBLENET_REFERENCE; no downloads.");
            var bytes = File.ReadAllBytes(Path.Combine(directory, name + ".f32"));
            var values = new float[bytes.Length / sizeof(float)];
            Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
            return values;
        }
    }
}
