using System;
using System.IO;
using System.Linq;
using System.Threading;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    [Category("Integration"), Category("SmartTurn")]
    public sealed class SmartTurnSourceParityTests
    {
        [Serializable] private sealed class Row { public string name; public float probability; }
        [Serializable] private sealed class Cases { public Row[] rows; }
        private static string ModelDirectory => Path.GetFullPath("Assets/StreamingAssets/MNN/Models/SmartTurn-v3.2-FP32-MNN");
        private static string Reference
        {
            get
            {
                string path = Environment.GetEnvironmentVariable("MNN_TEST_SMART_TURN_REFERENCE");
                if (string.IsNullOrWhiteSpace(path)) Assert.Ignore("Set MNN_TEST_SMART_TURN_REFERENCE to the official floating ONNX/transformers oracle.");
                return path;
            }
        }
        private static Row[] Rows => JsonUtility.FromJson<Cases>(File.ReadAllText(Path.Combine(Reference, "cases.json"))).rows;
        private static float[] Read(string name, string suffix)
        {
            var bytes = File.ReadAllBytes(Path.Combine(Reference, name + "-" + suffix + ".f32"));
            var values = new float[bytes.Length / 4];
            Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
            return values;
        }

        [Test]
        public void FrontendMatchesIndependentWhisperExtractor()
        {
            double maximum = 0;
            foreach (var row in Rows)
            {
                var actual = MNNSmartTurnFeatures.Extract(Read(row.name, "input"), default);
                var expected = Read(row.name, "features");
                Assert.AreEqual(expected.Length, actual.Length);
                double error = actual.Select((value, i) => Math.Abs(value - expected[i])).Max();
                maximum = Math.Max(maximum, error);
                TestContext.WriteLine(row.name + " feature max absolute=" + error);
            }
            TestContext.WriteLine("Feature max absolute=" + maximum);
            // Independent float STFT/Hann implementations differ near the log-mel floor.
            // Keep the feature budget explicit; posterior parity is gated separately.
            Assert.That(maximum, Is.LessThan(.00003));
        }

        [Test] public void CpuPosteriorsMatchOfficialFloatingSource() => CheckPosteriors(MNNBackendType.CPU);
        [Test] public void MetalPosteriorsMatchOfficialFloatingSource() => CheckPosteriors(MNNBackendType.Metal);
        [Test] public void AutoPosteriorsMatchOfficialFloatingSource() => CheckPosteriors(MNNBackendType.Auto);
        private static void CheckPosteriors(MNNBackendType backend)
        {
            if (!MNNAcceleration.IsBackendAvailable(backend)) Assert.Ignore(backend + " unavailable.");
            using (var model = MNNSmartTurn.Load(ModelDirectory, 1, backend))
            {
                double maximum = 0;
                foreach (var row in Rows.Concat(new[] { Rows.Single(value => value.name == "en") }))
                {
                    float actual = model.Predict(Read(row.name, "input"), 16000).CompletionProbability;
                    double error = Math.Abs(actual - row.probability);
                    maximum = Math.Max(maximum, error);
                    TestContext.WriteLine(row.name + ": source=" + row.probability + "; actual=" + actual + "; error=" + error);
                    Assert.That(error, Is.LessThan(.0002), row.name);
                    Assert.AreEqual(row.probability > .5f, actual > .5f, row.name);
                }
                Assert.AreEqual(backend == MNNBackendType.Auto ? MNNBackendType.CPU : backend, model.Backend);
                TestContext.WriteLine("Posterior max absolute=" + maximum);
            }
        }

        [TestCase(1), TestCase(2), TestCase(4)]
        public void WarmCpuThreadBudgetsKeepProbability(int threads)
        {
            var row = Rows.Single(value => value.name == "en");
            var pcm = Read("en", "input");
            using (var model = MNNSmartTurn.Load(ModelDirectory, threads, MNNBackendType.CPU))
            {
                model.Predict(pcm, 16000);
                var times = new double[5];
                for (int i = 0; i < times.Length; ++i)
                {
                    var timer = System.Diagnostics.Stopwatch.StartNew();
                    float actual = model.Predict(pcm, 16000).CompletionProbability;
                    times[i] = timer.Elapsed.TotalMilliseconds;
                    Assert.That(Math.Abs(actual - row.probability), Is.LessThan(.0002));
                }
                Array.Sort(times);
                TestContext.WriteLine("Warm CPU threads=" + threads + "; median ms=" + times[2]);
            }
        }

        [Test]
        public void StudioCancellationRecoveryAndDisposalKeepContract()
        {
            Assert.Throws<NotSupportedException>(() => MNNSmartTurn.Load(ModelDirectory, backendType: MNNBackendType.NN));
            Assert.AreEqual(MNNStudioTask.AudioProcessing, MNNStudioModel.Read(ModelDirectory).Task);
            var row = Rows.Single(value => value.name == "en");
            var pcm = Read("en", "input");
            var model = MNNSmartTurn.Load(ModelDirectory);
            try
            {
                Assert.Throws<OperationCanceledException>(() => model.Predict(pcm, 16000, cancellationToken: new CancellationToken(true)));
                Assert.That(Math.Abs(model.Predict(pcm, 16000).CompletionProbability - row.probability), Is.LessThan(.0002));
                Assert.Throws<ArgumentException>(() => model.Predict(new[] { float.NaN }, 16000));
                Assert.Throws<ArgumentOutOfRangeException>(() => model.Predict(pcm, 16000, float.NaN));
                Assert.Throws<ArgumentOutOfRangeException>(() => model.Predict(pcm, 7999));
            }
            finally { model.Dispose(); }
            Assert.Throws<ObjectDisposedException>(() => model.Predict(pcm, 16000));
            string audio = Path.GetFullPath("TestArtifacts~/SmartTurnSourceParity/studio.wav");
            Directory.CreateDirectory(Path.GetDirectoryName(audio));
            File.WriteAllBytes(audio, MNNWaveAudio.EncodePcm16(pcm, 16000));
            using (var studio = MNNStudioNativeBackend.Load(MNNStudioModel.Read(ModelDirectory), 4, false))
            {
                var result = studio.Run(new MNNStudioRequest { Audio = audio });
                Assert.IsNotNull(result.TurnDetection);
                Assert.That(Math.Abs(result.TurnDetection.CompletionProbability - row.probability), Is.LessThan(.0002));
            }
        }

        [Test]
        public void WarmMetalKeepsProbability()
        {
            if (!MNNAcceleration.IsBackendAvailable(MNNBackendType.Metal)) Assert.Ignore("Metal unavailable.");
            var row = Rows.Single(value => value.name == "en");
            var pcm = Read("en", "input");
            using (var model = MNNSmartTurn.Load(ModelDirectory, 1, MNNBackendType.Metal))
            {
                model.Predict(pcm, 16000);
                var times = new double[5];
                for (int i = 0; i < times.Length; ++i)
                {
                    var timer = System.Diagnostics.Stopwatch.StartNew();
                    Assert.That(Math.Abs(model.Predict(pcm, 16000).CompletionProbability - row.probability), Is.LessThan(.0002));
                    times[i] = timer.Elapsed.TotalMilliseconds;
                }
                Array.Sort(times);
                TestContext.WriteLine("Warm Metal median ms=" + times[2]);
            }
        }
    }
}
