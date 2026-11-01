using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class FrcrnParityTests
    {
        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal)]
        [Explicit("Requires FRCRN and the original spectrum ONNX plus official ConvSTFT/ConviSTFT waveform oracle.")]
        public void EnhancedWaveformMatchesOfficialDspAndSourceGraph(MNNBackendType backend)
        {
            if (!MNNAcceleration.IsBackendAvailable(backend)) Assert.Ignore(backend + " unavailable.");
            var input = Read("input");
            var expected = Read("enhanced");
            string directory = Path.GetFullPath(Path.Combine("Assets", "StreamingAssets", "MNN", "Models", "FRCRN-SE-16K-MNN"));
            using (var model = MNNFrcrnSe.Load(directory, 2, backend))
            {
                var timer = System.Diagnostics.Stopwatch.StartNew();
                var actual = model.Enhance(input);
                double elapsed = timer.Elapsed.TotalMilliseconds;
                Assert.AreEqual(backend, model.Backend);
                Assert.AreEqual(input.Length, actual.Length);
                double relative = Math.Sqrt(expected.Select((value, i) => Math.Pow((double)value - actual[i], 2)).Sum() / expected.Sum(value => (double)value * value));
                TestContext.WriteLine("Waveform NRMSE=" + relative + "; first ms=" + elapsed);
                Assert.That(relative, Is.LessThan(.005));
                Assert.That(actual[0], Is.EqualTo(expected[0]).Within(1e-6));
                var repeated = model.Enhance(input);
                Assert.That(actual.Select((value, i) => Math.Abs(value - repeated[i])).Max(), Is.LessThan(1e-6));
                var nonAligned = new float[22087];
                for (int i = 0; i < nonAligned.Length; ++i) nonAligned[i] = .1f * (float)Math.Sin(i * .17);
                Assert.AreEqual(nonAligned.Length, model.Enhance(nonAligned, 22050).Length);
            }
        }

        private static float[] Read(string name)
        {
            string directory = Environment.GetEnvironmentVariable("MNN_TEST_FRCRN_REFERENCE");
            if (string.IsNullOrWhiteSpace(directory)) Assert.Ignore("Set MNN_TEST_FRCRN_REFERENCE to the independent ConvSTFT/ONNX oracle.");
            var bytes = File.ReadAllBytes(Path.Combine(directory, name + ".f32"));
            var values = new float[bytes.Length / sizeof(float)];
            Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
            return values;
        }
    }
}
