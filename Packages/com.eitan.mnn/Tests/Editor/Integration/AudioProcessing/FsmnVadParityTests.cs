using System;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class FsmnVadParityTests
    {
        [Test, Explicit("Requires the independent official FunASR frontend oracle.")]
        public void FrontendMatchesOfficialHammingAndCenteredLfr()
        {
            var expected = Read("lfr");
            var actual = MNNFsmnFeatures.ExtractLfr(Read("input"), CancellationToken.None);
            Assert.AreEqual(expected.Length, actual.Length, "LFR must retain the 10 ms frame cadence.");
            double maximum = actual.Select((value, i) => Math.Abs((double)value - expected[i])).Max();
            TestContext.WriteLine("LFR maximum error=" + maximum);
            Assert.That(maximum, Is.LessThan(.001));
            string directory = Path.GetFullPath(Path.Combine("Assets", "StreamingAssets", "MNN", "Models", "FSMN-VAD-16K-MNN"));
            MNNFsmnFeatures.ApplyCmvn(actual, MNNFsmnVadConfig.Read(directory));
            var normalized = Read("features");
            maximum = actual.Select((value, i) => Math.Abs((double)value - normalized[i])).Max();
            TestContext.WriteLine("CMVN maximum error=" + maximum);
            Assert.That(maximum, Is.LessThan(.001));
        }

        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal)]
        [Explicit("Requires FSMN and the official PyTorch multi-chunk posterior oracle.")]
        public void PosteriorsMatchOriginalOnnxAndResetBetweenCalls(MNNBackendType backend)
        {
            if (!MNNAcceleration.IsBackendAvailable(backend)) Assert.Ignore(backend + " unavailable.");
            var input = Read("input");
            var expected = Read("probabilities");
            string directory = Path.GetFullPath(Path.Combine("Assets", "StreamingAssets", "MNN", "Models", "FSMN-VAD-16K-MNN"));
            using (var model = MNNFsmnVad.Load(directory, 1, backend))
            {
                var timer = System.Diagnostics.Stopwatch.StartNew();
                var actual = model.Detect(input, 16000);
                double elapsed = timer.Elapsed.TotalMilliseconds;
                Assert.AreEqual(backend, model.Backend);
                Assert.AreEqual(expected.Length, actual.Probabilities.Count);
                double maximum = actual.Probabilities.Select((value, i) => Math.Abs((double)value - expected[i])).Max();
                TestContext.WriteLine("Posterior max error=" + maximum + "; elapsed ms=" + elapsed);
                Assert.That(maximum, Is.LessThan(.002));
                string report = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "{{\"backend\":\"{0}\",\"samples\":{1},\"frames\":{2},\"first_ms\":{3:F4},\"posterior_max_error\":{4:G9}}}",
                    backend, input.Length, expected.Length, elapsed, maximum);
                File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("MNN_TEST_FSMN_REFERENCE"), "unity-" + backend + ".json"), report + "\n");
                var repeat = model.Detect(input, 16000);
                Assert.That(actual.Probabilities.Select((value, i) => Math.Abs(value - repeat.Probabilities[i])).Max(), Is.LessThan(1e-6));
                Assert.Throws<OperationCanceledException>(() => model.Detect(input, 16000, cancellationToken: new CancellationToken(true)));
                Assert.That(model.Detect(input, 16000).Probabilities.Select((value, i) => Math.Abs(value - repeat.Probabilities[i])).Max(), Is.LessThan(1e-6));
                var tooShort = model.Detect(new float[399], 16000);
                Assert.Zero(tooShort.Probabilities.Count, "Snipped-edge Kaldi frames require 25 ms.");
                Assert.Zero(tooShort.Segments.Count);
            }
        }

        [Test]
        public void PosteriorIsNotSoftmaxedTwice()
        {
            var posterior = new float[248];
            posterior[0] = .9f;
            posterior[1] = .1f;
            Assert.That(MNNFsmnVad.SpeechProbability(posterior, 0), Is.EqualTo(.1f).Within(1e-6));
        }

        private static float[] Read(string name)
        {
            string directory = Environment.GetEnvironmentVariable("MNN_TEST_FSMN_REFERENCE");
            if (string.IsNullOrWhiteSpace(directory)) Assert.Ignore("Set MNN_TEST_FSMN_REFERENCE to the independent FunASR oracle.");
            var bytes = File.ReadAllBytes(Path.Combine(directory, name + ".f32"));
            var values = new float[bytes.Length / sizeof(float)];
            Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
            return values;
        }
    }
}
