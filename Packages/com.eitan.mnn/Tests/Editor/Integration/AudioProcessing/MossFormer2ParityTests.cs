using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class MossFormer2ParityTests
    {
        private static string DirectoryPath => Path.GetFullPath(Path.Combine("Assets", "StreamingAssets", "MNN", "Models", "MossFormer2-SS-16K-MNN"));

        [TestCase("short", MNNBackendType.Auto), TestCase("short", MNNBackendType.CPU), TestCase("long", MNNBackendType.CPU), TestCase("short", MNNBackendType.Metal), TestCase("long", MNNBackendType.Metal)]
        [Explicit("Requires MossFormer2 and the official PyTorch mixture/chunk oracle.")]
        public void SeparationMatchesSourceAndPreservesChunkEdges(string name, MNNBackendType backend)
        {
            if (!MNNAcceleration.IsBackendAvailable(backend)) Assert.Ignore(backend + " unavailable.");
            var input = Read(name + "-input");
            using (var model = MNNMossFormer2Separation.Load(DirectoryPath, 2, backend))
            {
                var timer = System.Diagnostics.Stopwatch.StartNew();
                var actual = model.Separate(input);
                double elapsed = timer.Elapsed.TotalMilliseconds;
                Assert.AreEqual(backend == MNNBackendType.Auto ? MNNAcceleration.PreferredBackend : backend, model.Backend);
                Check(actual.Speaker0, Read(name + "-spk0"));
                Check(actual.Speaker1, Read(name + "-spk1"));
                TestContext.WriteLine(backend + ": " + name + " elapsed ms=" + elapsed);
                var repeated = model.Separate(input);
                Assert.That(actual.Speaker0.Select((value, i) => Math.Abs(value - repeated.Speaker0[i])).Max(), Is.LessThan(1e-6));
                Assert.Throws<OperationCanceledException>(() => model.Separate(input, cancellationToken: new System.Threading.CancellationToken(true)));
            }
        }

        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal)]
        [Explicit("Checks short/long/short session reuse against the independent PyTorch oracle.")]
        public void ChangingLengthPreservesSeparation(MNNBackendType backend)
        {
            if (!MNNAcceleration.IsBackendAvailable(backend)) Assert.Ignore(backend + " unavailable.");
            using (var model = MNNMossFormer2Separation.Load(DirectoryPath, 2, backend))
            {
                foreach (string name in new[]{"short", "long", "short"})
                {
                    var timer = System.Diagnostics.Stopwatch.StartNew();
                    var actual = model.Separate(Read(name + "-input"));
                    TestContext.WriteLine(backend + ": " + name + " ms=" + timer.Elapsed.TotalMilliseconds);
                    Check(actual.Speaker0, Read(name + "-spk0"));
                    Check(actual.Speaker1, Read(name + "-spk1"));
                }
            }
        }

        [Test, Explicit("Requires the separation graph and checks resampling through the public API.")]
        public void ResamplingPreservesOriginalSampleCount()
        {
            var input = new float[22087];
            for (int i = 0; i < input.Length; ++i) input[i] = .1f * (float)Math.Sin(i * .173);
            using (var model = MNNMossFormer2Separation.Load(DirectoryPath))
            {
                var output = model.Separate(input, 22050);
                Assert.AreEqual(input.Length, output.Speaker0.Length);
                Assert.AreEqual(input.Length, output.Speaker1.Length);
                Assert.AreEqual(22050, output.SampleRate);
            }
        }

        private static void Check(float[] actual, float[] expected)
        {
            Assert.AreEqual(expected.Length, actual.Length);
            double relative = Math.Sqrt(expected.Select((value, i) => Math.Pow((double)value - actual[i], 2)).Sum() / expected.Sum(value => (double)value * value));
            TestContext.WriteLine("Waveform NRMSE=" + relative);
            Assert.That(relative, Is.LessThan(.005));
            Assert.That(actual[0], Is.EqualTo(expected[0]).Within(Math.Max(1e-6, Math.Abs(expected[0]) * .03)));
            Assert.That(actual[actual.Length - 1], Is.EqualTo(expected[expected.Length - 1]).Within(Math.Max(1e-6, Math.Abs(expected[expected.Length - 1]) * .03)));
        }

        private static float[] Read(string name)
        {
            string directory = Environment.GetEnvironmentVariable("MNN_TEST_MOSS_REFERENCE");
            if (string.IsNullOrWhiteSpace(directory)) Assert.Ignore("Set MNN_TEST_MOSS_REFERENCE to the official PyTorch oracle.");
            var bytes = File.ReadAllBytes(Path.Combine(directory, name + ".f32"));
            var values = new float[bytes.Length / sizeof(float)];
            Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
            return values;
        }
    }
}
