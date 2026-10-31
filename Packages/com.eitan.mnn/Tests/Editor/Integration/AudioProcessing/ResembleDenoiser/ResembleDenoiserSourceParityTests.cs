using System;
using System.IO;
using System.Linq;
using System.Threading;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    [Category("Integration"), Category("ResembleDenoiser")]
    public sealed class ResembleDenoiserSourceParityTests
    {
        private static string Directory => Path.GetFullPath("Assets/StreamingAssets/MNN/Models/Resemble-Denoiser-44K-MNN");
        private static string Reference => Environment.GetEnvironmentVariable("MNN_TEST_RESEMBLE_DENOISER_REFERENCE")
            ?? throw new InvalidOperationException("Set the original Resemble Denoiser reference directory.");
        [Serializable] private sealed class Case { public string @case = ""; }
        [Serializable] private sealed class Cases { public Case[] cases = null; }
        private static string[] CaseNames => JsonUtility.FromJson<Cases>(File.ReadAllText(Path.Combine(Reference, "cases.json"))).cases.Select(c => c.@case).ToArray();
        private static float[] Read(string name)
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(Reference, name + ".f32"));
            var values = new float[bytes.Length / 4]; Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length); return values;
        }
        private static void Check(string name, float[] actual)
        {
            float[] expected = Read(name + "-target"); Assert.AreEqual(expected.Length, actual.Length, name);
            double error = 0, energy = 0;
            for (int i = 0; i < expected.Length; ++i)
            {
                Assert.False(float.IsNaN(actual[i]) || float.IsInfinity(actual[i]), name);
                double delta = (double)actual[i] - expected[i]; error += delta * delta; energy += (double)expected[i] * expected[i];
            }
            double nrmse = Math.Sqrt(error / (energy == 0 ? 1 : energy));
            TestContext.WriteLine(name + ";nrmse=" + nrmse); Assert.Less(nrmse, .001, name);
        }
        [Test]
        public void PublicApiExposesCompletePcmDenoising()
        {
            Assert.NotNull(typeof(MNNResembleDenoiser).GetMethod("Load")); Assert.NotNull(typeof(MNNResembleDenoiser).GetMethod("Enhance"));
            Assert.AreEqual(44100, MNNResembleDenoiser.SampleRate);
        }
        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Auto)]
        public void CompletePcmMatchesOriginalAcrossLengthsAndReusedSessions(MNNBackendType backend)
        {
            using (var model = MNNResembleDenoiser.Load(Directory, 8, backend))
            {
                Assert.AreEqual(24, CaseNames.Length);
                foreach (string name in CaseNames.Concat(CaseNames.Take(1)))
                    for (int repeat = 0; repeat < 2; ++repeat) Check(name, model.Enhance(Read(name + "-input")));
                Assert.AreEqual(MNNBackendType.CPU, model.Backend);
            }
        }
        [Test]
        public void CatalogueAndStudioExecuteTheOriginalWaveform()
        {
            Assert.AreEqual("AudioProcessing", MNNModelRepository.Classify("owner/Resemble-Denoiser-44K-MNN"));
            MNNStudioModel model = MNNStudioModel.Read(Directory); Assert.True(model.HasAudioProcessingPipeline);
            using (var backend = MNNStudioNativeBackend.Load(model, 8, false))
            {
                var result = backend.Run(new MNNStudioRequest { Audio = Path.Combine(Reference, "real.wav") });
                Assert.AreEqual(44100, result.SampleRate); Check("real", result.Waveform);
                Assert.Throws<ArgumentException>(() => backend.Run(new MNNStudioRequest()));
                Assert.Throws<OperationCanceledException>(() => backend.Run(new MNNStudioRequest { Audio = Path.Combine(Reference, "real.wav"), Cancellation = new CancellationToken(true) }));
            }
        }
        [Test]
        public void InvalidAudioCancellationBackendAndDisposalAreExplicit()
        {
            Assert.Throws<NotSupportedException>(() => MNNResembleDenoiser.Load(Directory, backendType: MNNBackendType.Metal));
            using (var model = MNNResembleDenoiser.Load(Directory))
            {
                Assert.Throws<ArgumentException>(() => model.Enhance(new float[399]));
                Assert.Throws<ArgumentException>(() => model.Enhance(new float[132301]));
                var bad = new float[400]; bad[20] = float.NaN; Assert.Throws<ArgumentException>(() => model.Enhance(bad));
                Assert.Throws<OperationCanceledException>(() => model.Enhance(new float[400], cancellationToken: new CancellationToken(true)));
                float[] zero = model.Enhance(new float[16000], 16000); Assert.AreEqual(16000, zero.Length); Assert.True(zero.All(v => v == 0));
                model.Dispose(); model.Dispose(); Assert.Throws<ObjectDisposedException>(() => model.Enhance(new float[400]));
            }
        }
        [Test]
        public void ProfileRejectsChangedWindowAndGraphBytes()
        {
            string temporary = Path.Combine(Path.GetFullPath("TestArtifacts~/ResembleIntegrity"), Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(temporary);
            try
            {
                foreach (string name in new[] { MNNResembleDenoiserConfig.FileName, MNNResembleDenoiserModel.Graph })
                    File.Copy(Path.Combine(Directory, name), Path.Combine(temporary, name));
                Assert.True(MNNResembleDenoiserModel.IsComplete(temporary));
                var config = MNNResembleDenoiserConfig.Read(temporary); config.analysis_window[1] += .1f;
                File.WriteAllText(Path.Combine(temporary, MNNResembleDenoiserConfig.FileName), JsonUtility.ToJson(config));
                Assert.False(MNNResembleDenoiserModel.IsComplete(temporary));
                File.Copy(Path.Combine(Directory, MNNResembleDenoiserConfig.FileName), Path.Combine(temporary, MNNResembleDenoiserConfig.FileName), true);
                using (var stream = File.OpenWrite(Path.Combine(temporary, MNNResembleDenoiserModel.Graph))) stream.WriteByte(255);
                Assert.False(MNNResembleDenoiserModel.IsComplete(temporary));
            }
            finally { System.IO.Directory.Delete(temporary, true); }
        }

        [Test]
        public void MeasureInterleavedCompletePcmCalls()
        {
            foreach (int[] order in new[] { new[] { 1, 2, 4, 8 }, new[] { 8, 4, 2, 1 }, new[] { 2, 8, 1, 4 } })
            foreach (int threads in order)
            using (var model = MNNResembleDenoiser.Load(Directory, threads, MNNBackendType.CPU))
            foreach (string name in new[] { "real", "maximum" })
            {
                float[] input = Read(name + "-input"); model.Enhance(input); var times = new double[5];
                for (int repeat = 0; repeat < times.Length; ++repeat)
                {
                    var watch = System.Diagnostics.Stopwatch.StartNew(); float[] output = model.Enhance(input); watch.Stop();
                    times[repeat] = watch.Elapsed.TotalMilliseconds; Check(name, output);
                }
                TestContext.WriteLine("threads=" + threads + ";case=" + name + ";times_ms=" + string.Join(",", times.Select(v => v.ToString("R", System.Globalization.CultureInfo.InvariantCulture))));
            }
        }
    }
}
