using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [Category("Integration"), Category("AvTfGridNet")]
    public sealed class AvTfGridNetNumericsTests
    {
        private static string Reference => Environment.GetEnvironmentVariable("MNN_TEST_AV_TFGRIDNET_REFERENCE")
            ?? throw new InvalidOperationException("Set MNN_TEST_AV_TFGRIDNET_REFERENCE to the original author oracle.");
        private static float[] Read(string name)
        {
            var bytes = File.ReadAllBytes(Path.Combine(Reference, name + ".f32"));
            var values = new float[bytes.Length / 4]; Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length); return values;
        }

        [Test]
        public void SpectralAnalysisAndSynthesisMatchOriginalTorch()
        {
            var spectral = new MNNAvTfGridNetSpectral(Read("hann-window"));
            foreach (string name in new[] { "minimum", "short", "noise", "long", "real", "quiet", "gain2", "silence", "constant", "shifted_visual", "mean_visual", "hop_before", "hop_at", "hop_after", "video_before", "video_at", "video_after", "maximum_before", "silence_minimum", "silence_short", "silence_second", "silence_odd", "silence_shifted", "silence_mean", "quiet_minimum", "very_quiet" })
            {
                var pcm = Read(name + "-input"); var spectrum = spectral.Analyze(pcm, CancellationToken.None, out int frames);
                Assert.AreEqual(pcm.Length / 128 + 1, frames, name);
                Compare(spectrum, Read(name + "-spectrum"), 1e-6, name + ":stft");
                // Compare inverse FFT independently. The original float32 Torch
                // roundtrip itself reaches 3.91e-6 at a short final Hann envelope.
                Compare(spectral.Synthesize(Read(name + "-spectrum"), frames, pcm.Length, CancellationToken.None),
                    Read(name + "-roundtrip"), 1e-5, name + ":istft");
            }
        }

        private static void Compare(float[] actual, float[] expected, double budget, string name)
        {
            Assert.AreEqual(expected.Length, actual.Length, name); double energy = 0, error = 0;
            for (int i = 0; i < actual.Length; ++i)
            {
                Assert.False(float.IsNaN(actual[i]) || float.IsInfinity(actual[i]), name);
                double value = expected[i]; energy += value * value; double delta = actual[i] - value; error += delta * delta;
            }
            double nrmse = Math.Sqrt(error / (energy == 0 ? 1 : energy)); TestContext.WriteLine(name + ";nrmse=" + nrmse);
            Assert.Less(nrmse, budget, name);
        }

        [Test]
        public void InitialNormalizationMatchesEveryOriginalTorchFloat()
        {
            var normalization = new MNNAvTfGridNetNormalization(Read("norm-gamma"), Read("norm-beta"));
            foreach (string line in File.ReadAllLines(Path.Combine(Reference, "normalization-sha256.txt")))
            {
                string[] fields = line.Split(' ');
                var values = Read(fields[0] + "-convolved");
                normalization.NormalizeInPlace(values, CancellationToken.None);
                var bytes = new byte[values.Length * 4];
                Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
                using (var sha = SHA256.Create())
                    Assert.AreEqual(fields[1], BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(), fields[0]);
            }
        }

    }
}
