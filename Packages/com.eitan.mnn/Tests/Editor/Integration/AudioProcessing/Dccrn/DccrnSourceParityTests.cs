using System;
using System.IO;
using System.Linq;
using System.Threading;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    [Category("Integration"), Category("Dccrn")]
    public sealed class DccrnSourceParityTests
    {
        private string Directory => Path.GetFullPath("Assets/StreamingAssets/MNN/Models/DCCRN-Mayavoz-VCTK-16K-MNN");
        private string Reference => Environment.GetEnvironmentVariable("MNN_TEST_DCCRN_REFERENCE")
            ?? throw new InvalidOperationException("Set the checkpoint-owned original DCCRN references.");
        [Serializable] private sealed class Case { public string @case = ""; }
        [Serializable] private sealed class Cases { public Case[] cases = null; }
        private string[] Names => JsonUtility.FromJson<Cases>(File.ReadAllText(Path.Combine(Reference, "cases.json"))).cases.Select(c => c.@case).ToArray();

        private float[] Read(string name)
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(Reference, name + ".f32"));
            var values = new float[bytes.Length / 4]; Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length); return values;
        }

        private static void Check(float[] expected, float[] actual, string context, double tolerance = .001)
        {
            Assert.AreEqual(expected.Length, actual.Length, context);
            double error = 0, energy = 0;
            for (int i = 0; i < actual.Length; ++i)
            {
                Assert.False(float.IsNaN(actual[i]) || float.IsInfinity(actual[i]), context);
                double delta = (double)actual[i] - expected[i]; error += delta * delta; energy += (double)expected[i] * expected[i];
            }
            double nrmse = Math.Sqrt(error / (energy == 0 ? 1 : energy));
            TestContext.WriteLine(context + ";nrmse=" + nrmse);
            Assert.Less(nrmse, tolerance, context);
        }

        [Test]
        public void PublicApiExposesOriginalCheckpointOwnedPcmEnhancement()
        {
            var api = typeof(MNNBackendType).Assembly.GetType("MNN.Unity.MNNDccrnEnhancement");
            Assert.NotNull(api, "The complete checkpoint-owned DCCRN PCM API is missing.");
            Assert.NotNull(api.GetMethod("Load"));
            Assert.NotNull(api.GetMethod("Enhance"));
            Assert.NotNull(api.GetMethod("EnhanceWaveFile"));
            Assert.AreEqual(16000, api.GetField("SampleRate").GetRawConstantValue());
        }

        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal)]
        public void CompletePcmMatchesOriginalAcrossLengthsAndReusedSessions(MNNBackendType backend)
        {
            Assert.AreEqual(22, Names.Length);
            using (var model = MNNDccrnEnhancement.Load(Directory, 4, backend))
            {
                foreach (string name in Names.Concat(Names.Take(1)))
                    for (int repeat = 0; repeat < 2; ++repeat)
                    {
                        float[] input = Read(name + "-input"), snapshot = (float[])input.Clone();
                        Check(Read(name + "-target"), model.Enhance(input), name);
                        CollectionAssert.AreEqual(snapshot, input);
                    }
                Assert.AreEqual(backend, model.Backend);
                Assert.AreEqual(MNNBackendType.CPU, model.AnalysisBackend);
                Assert.AreEqual(MNNBackendType.CPU, model.RecurrentBackend);
                Assert.AreEqual(backend, model.DecoderBackend);
            }
        }

        [Test]
        public void DefaultPolicyMatchesOriginalAcrossAllInputsAndShapeReturn()
        {
            using (var model = MNNDccrnEnhancement.Load(Directory))
            {
                foreach (string name in Names.Concat(Names.Take(1)))
                    for (int repeat = 0; repeat < 2; ++repeat)
                        Check(Read(name + "-target"), model.Enhance(Read(name + "-input")), "default " + name);
                Assert.That(model.Backend, Is.EqualTo(MNNBackendType.CPU).Or.EqualTo(MNNBackendType.Metal));
                Assert.AreEqual(model.Backend, model.DecoderBackend);
            }
        }

        [Test]
        public void CatalogAndStudioExecuteOriginalFloatWav()
        {
            Assert.AreEqual("AudioProcessing", MNNModelRepository.Classify("owner/DCCRN-Mayavoz-VCTK-16K-MNN"));
            var profile = MNNStudioModel.Read(Directory);
            Assert.True(profile.HasAudioProcessingPipeline);
            using (var backend = MNNStudioNativeBackend.Load(profile, 4, false))
            {
                string path = Path.Combine(Reference, "real.wav");
                var result = backend.Run(new MNNStudioRequest { Audio = path });
                Assert.AreEqual(16000, result.SampleRate);
                StringAssert.Contains("Mayavoz DCCRN", result.BackendDescription);
                Check(Read("real-target"), result.Waveform, "Studio original WAV");
                Assert.Throws<ArgumentException>(() => backend.Run(new MNNStudioRequest()));
                Assert.Throws<OperationCanceledException>(() => backend.Run(new MNNStudioRequest { Audio = path, Cancellation = new CancellationToken(true) }));
            }
        }

        [Test]
        public void ResamplingPreservesLengthAmplitudeAndInputOwnership()
        {
            using (var model = MNNDccrnEnhancement.Load(Directory, 4, MNNBackendType.CPU))
            foreach (int rate in new[] { 8000, 44100, 48000, 96000 })
            {
                float[] input = MNNSpeechDsp.Resample(Read("real-input"), 16000, rate, default), snapshot = (float[])input.Clone();
                var native = model.Enhance(MNNSpeechDsp.Resample(input, rate, 16000, default));
                var actual = model.Enhance(input, rate);
                Assert.AreEqual(input.Length, actual.Length);
                CollectionAssert.AreEqual(snapshot, input);
                Check(MNNSpeechDsp.Resample(native, 16000, rate, default), actual, "resampling " + rate, 1e-6);
            }
        }

        [Test]
        public void InvalidAudioCancellationDisposalAndUnsupportedBackendsAreExplicit()
        {
            Assert.Throws<ArgumentNullException>(() => MNNDccrnEnhancement.Load(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => MNNDccrnEnhancement.Load(Directory, 0));
            Assert.Throws<NotSupportedException>(() => MNNDccrnEnhancement.Load(Directory, backendType: MNNBackendType.NN));
            Assert.Throws<NotSupportedException>(() => MNNDccrnEnhancement.Load(Directory, backendType: MNNBackendType.OpenCL));
            using (var model = MNNDccrnEnhancement.Load(Directory, 1, MNNBackendType.CPU))
            {
                Assert.Throws<ArgumentException>(() => model.Enhance(null));
                Assert.Throws<ArgumentException>(() => model.Enhance(new float[0]));
                Assert.Throws<ArgumentException>(() => model.Enhance(new float[48001]));
                Assert.Throws<ArgumentOutOfRangeException>(() => model.Enhance(new float[400], 7999));
                var invalid = new float[400]; invalid[13] = float.PositiveInfinity;
                Assert.Throws<ArgumentException>(() => model.Enhance(invalid));
                invalid[13] = float.NaN; Assert.Throws<ArgumentException>(() => model.Enhance(invalid));
                Assert.Throws<OperationCanceledException>(() => model.Enhance(new float[400], cancellationToken: new CancellationToken(true)));
                model.Dispose(); model.Dispose();
                Assert.Throws<ObjectDisposedException>(() => model.Enhance(new float[400]));
            }
        }

        [Test]
        public void ProfileRejectsChangedSourceEveryGraphAndSharedWeights()
        {
            string temporary = Path.Combine(Path.GetFullPath("TestArtifacts~/DccrnIntegrity"), Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(temporary);
            var files = MNNDccrnModel.Files.Concat(new[] { MNNDccrnConfig.FileName }).ToArray();
            try
            {
                foreach (string file in files) File.Copy(Path.Combine(Directory, file), Path.Combine(temporary, file));
                Assert.True(MNNDccrnModel.IsComplete(temporary));
                var config = MNNDccrnConfig.Read(temporary); config.checkpoint_loading = "default-copy";
                File.WriteAllText(Path.Combine(temporary, MNNDccrnConfig.FileName), JsonUtility.ToJson(config));
                Assert.False(MNNDccrnModel.IsComplete(temporary));
                File.Copy(Path.Combine(Directory, MNNDccrnConfig.FileName), Path.Combine(temporary, MNNDccrnConfig.FileName), true);
                foreach (string file in MNNDccrnModel.Files)
                {
                    using (var stream = File.Open(Path.Combine(temporary, file), FileMode.Open, FileAccess.ReadWrite))
                    {
                        int first = stream.ReadByte(); stream.Position = 0; stream.WriteByte((byte)(first ^ 255));
                    }
                    Assert.False(MNNDccrnModel.IsComplete(temporary));
                    Assert.Throws<InvalidDataException>(() => MNNDccrnEnhancement.Load(temporary));
                    File.Copy(Path.Combine(Directory, file), Path.Combine(temporary, file), true);
                }
            }
            finally { System.IO.Directory.Delete(temporary, true); }
        }
    }
}
