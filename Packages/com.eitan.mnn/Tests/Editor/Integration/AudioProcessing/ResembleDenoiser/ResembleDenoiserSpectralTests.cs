using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    [Category("Integration"), Category("ResembleDenoiser")]
    public sealed class ResembleDenoiserSpectralTests
    {
        [Serializable] private sealed class Case { public string @case = ""; public int frames; public int samples; }
        [Serializable] private sealed class Cases { public Case[] cases = null; }
        private static string Reference => Environment.GetEnvironmentVariable("MNN_TEST_RESEMBLE_DENOISER_REFERENCE")
            ?? throw new InvalidOperationException("Set the original Resemble Denoiser reference directory.");

        private static float[] Read(string name)
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(Reference, name + ".f32"));
            var result = new float[bytes.Length / 4]; Buffer.BlockCopy(bytes, 0, result, 0, bytes.Length); return result;
        }

        [Test]
        public void SourceOrderedRealFftPreservesAllOriginalAnalysisPhases()
        {
            var spectral = new MNNResembleDenoiserSpectral(Read("window"));
            var cases = JsonUtility.FromJson<Cases>(File.ReadAllText(Path.Combine(Reference, "cases.json"))).cases;
            foreach (var item in cases)
            {
                float[] pcm = Read(item.@case + "-input"), expected = Read(item.@case + "-features");
                float[] actual = spectral.Analyze(pcm, out int frames, out float scale, default);
                Assert.AreEqual(item.frames, frames);
                double delta = 0, energy = 0;
                for (int i = 0; i < expected.Length; ++i)
                {
                    double error = (double)actual[i] - expected[i]; delta += error * error; energy += (double)expected[i] * expected[i];
                }
                double nrmse = Math.Sqrt(delta / (energy == 0 ? 1 : energy));
                TestContext.WriteLine(item.@case + ";features_nrmse=" + nrmse);
                Assert.Less(nrmse, 1e-5, item.@case + " original magnitude/cos/sin features");
                byte[] bytes = new byte[actual.Length * 4]; Buffer.BlockCopy(actual, 0, bytes, 0, bytes.Length);
                File.WriteAllBytes(Path.Combine(Reference, item.@case + "-managed-features.f32"), bytes);
            }
        }

        [Test]
        public void IndependentOriginalSpectrumReconstructsTheOriginalWaveform()
        {
            var spectral = new MNNResembleDenoiserSpectral(Read("window"));
            var cases = JsonUtility.FromJson<Cases>(File.ReadAllText(Path.Combine(Reference, "cases.json"))).cases;
            foreach (var item in cases)
            {
                float peak = 0;
                foreach (float value in Read(item.@case + "-input")) peak = Math.Max(peak, Math.Abs(value));
                var actual = spectral.Synthesize(Read(item.@case + "-spectrum"), item.frames, item.samples, Math.Max(peak, 1e-7f), default);
                var expected = Read(item.@case + "-target"); double delta = 0, energy = 0;
                for (int i = 0; i < expected.Length; ++i)
                {
                    double error = (double)actual[i] - expected[i]; delta += error * error; energy += (double)expected[i] * expected[i];
                }
                Assert.Less(Math.Sqrt(delta / (energy == 0 ? 1 : energy)), 1e-5, item.@case);
            }
        }

        [Test]
        public void Shared400PointPlanPreservesOriginalGoldenFrames()
        {
            var fft = new MNNSourceRealFft(400); var input = new float[400]; var real = new float[201]; var imaginary = new float[201];
            float[] frames = Read("fft400-input"), expected = Read("fft400-output");
            for (int frame = 0; frame < frames.Length / 400; ++frame)
            {
                Array.Copy(frames, frame * 400, input, 0, 400); fft.Forward(input, real, imaginary);
                for (int bin = 0; bin < 201; ++bin)
                {
                    Assert.AreEqual(expected[2 * (frame * 201 + bin)], real[bin], "frame/real " + frame + "/" + bin);
                    Assert.AreEqual(expected[2 * (frame * 201 + bin) + 1], imaginary[bin], "frame/imaginary " + frame + "/" + bin);
                }
            }
        }
    }
}
