using System;
using System.IO;
using System.Linq;
using System.Threading;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    [Category("Integration"), Category("Tiger")]
    [TestFixture("TIGER-Speech-16K-MNN", "MNN_TEST_TIGER_REFERENCE")]
    public sealed class TigerSourceParityTests
    {
        private readonly string _modelName;
        private readonly string _referenceVariable;
        public TigerSourceParityTests(string modelName, string referenceVariable)
        {
            _modelName = modelName;
            _referenceVariable = referenceVariable;
        }

        private string Directory => Path.GetFullPath("Assets/StreamingAssets/MNN/Models/" + _modelName);
        private string Reference => Environment.GetEnvironmentVariable(_referenceVariable)
            ?? throw new InvalidOperationException("Set the original TIGER source reference directory.");
        [Serializable] private sealed class Case { public string @case = ""; }
        [Serializable] private sealed class Cases { public Case[] cases = null; }
        private string[] Names => JsonUtility.FromJson<Cases>(File.ReadAllText(Path.Combine(Reference, "cases.json"))).cases.Select(c => c.@case).ToArray();

        private float[] Read(string name)
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(Reference, name + ".f32"));
            var values = new float[bytes.Length / 4];
            Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
            return values;
        }

        private static void Check(float[] expected, float[] actual, int speaker, string context, double tolerance = .001)
        {
            int length = expected.Length / (speaker < 0 ? 1 : 2);
            Assert.AreEqual(length, actual.Length, context);
            int offset = Math.Max(speaker, 0) * length;
            double error = 0, energy = 0;
            for (int i = 0; i < actual.Length; ++i)
            {
                Assert.False(float.IsNaN(actual[i]) || float.IsInfinity(actual[i]), context);
                double value = expected[offset + i], delta = (double)actual[i] - value;
                error += delta * delta; energy += value * value;
            }
            double nrmse = Math.Sqrt(error / (energy == 0 ? 1 : energy));
            TestContext.WriteLine(context + ";nrmse=" + nrmse);
            Assert.Less(nrmse, tolerance, context);
        }

        private void CheckSources(string name, MNNSeparatedAudio audio)
        {
            float[] expected = Read(name + "-target");
            Check(expected, audio.Speaker0, 0, name + " source0");
            Check(expected, audio.Speaker1, 1, name + " source1");
            Assert.AreEqual(16000, audio.SampleRate);
        }

        [Test]
        public void PublicApiExposesTwoSourcePcmSeparation()
        {
            var api = typeof(MNNBackendType).Assembly.GetType("MNN.Unity.MNNTigerSeparation");
            Assert.NotNull(api, "The complete TIGER PCM separation API is missing.");
            Assert.NotNull(api.GetMethod("Load"));
            Assert.NotNull(api.GetMethod("Separate"));
            Assert.NotNull(api.GetMethod("SeparateWaveFile"));
            Assert.AreEqual(16000, api.GetField("SampleRate").GetRawConstantValue());
        }

        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal)]
        public void CompletePcmMatchesOriginalAcrossLengthsAndReusedSessions(MNNBackendType backend)
        {
            Assert.AreEqual(25, Names.Length);
            using (var model = MNNTigerSeparation.Load(Directory, 4, backend))
            {
                foreach (string name in Names.Concat(Names.Take(1)))
                    for (int repeat = 0; repeat < 2; ++repeat)
                        CheckSources(name, model.Separate(Read(name + "-input")));
                Assert.AreEqual(backend, model.Backend);
            }
        }

        [Test]
        public void DefaultPolicyMatchesOriginalAcrossAllInputsAndShapeReturn()
        {
            using (var model = MNNTigerSeparation.Load(Directory))
            {
                foreach (string name in Names.Concat(Names.Take(1)))
                    for (int repeat = 0; repeat < 2; ++repeat)
                        CheckSources(name, model.Separate(Read(name + "-input")));
                Assert.That(model.Backend, Is.EqualTo(MNNBackendType.CPU).Or.EqualTo(MNNBackendType.Metal));
            }
        }

        [Test]
        public void CatalogAndStudioExecuteTheOriginalWav()
        {
            Assert.AreEqual("AudioProcessing", MNNModelRepository.Classify("owner/" + _modelName));
            var profile = MNNStudioModel.Read(Directory);
            Assert.True(profile.HasAudioProcessingPipeline);
            using (var backend = MNNStudioNativeBackend.Load(profile, 4, false))
            {
                string path = Path.Combine(Reference, "real.wav");
                var result = backend.Run(new MNNStudioRequest { Audio = path });
                Assert.AreEqual(16000, result.SampleRate);
                StringAssert.Contains(_modelName.StartsWith("DAVE-", StringComparison.Ordinal) ? "DAVE TIGER-M" : "TIGER", result.BackendDescription);
                Check(Read("real-target"), result.Waveform, 0, "Studio original WAV source0");
                Assert.Throws<ArgumentException>(() => backend.Run(new MNNStudioRequest()));
                Assert.Throws<OperationCanceledException>(() => backend.Run(new MNNStudioRequest { Audio = path, Cancellation = new CancellationToken(true) }));
            }
        }

        [Test]
        public void ResamplingPreservesLengthAmplitudeAndInputOwnership()
        {
            using (var model = MNNTigerSeparation.Load(Directory, 4, MNNBackendType.CPU))
            foreach (int rate in new[] { 8000, 44100, 48000, 96000 })
            {
                float[] input = MNNSpeechDsp.Resample(Read("length_4800-input"), 16000, rate, default);
                var snapshot = (float[])input.Clone();
                float[] modelInput = MNNSpeechDsp.Resample(input, rate, 16000, default);
                var native = model.Separate(modelInput);
                var actual = model.Separate(input, rate);
                Assert.AreEqual(rate, actual.SampleRate);
                Assert.AreEqual(input.Length, actual.Speaker0.Length);
                Assert.AreEqual(input.Length, actual.Speaker1.Length);
                CollectionAssert.AreEqual(snapshot, input);
                Check(MNNSpeechDsp.Resample(native.Speaker0, 16000, rate, default), actual.Speaker0, -1, "resampled source0", 1e-6);
                Check(MNNSpeechDsp.Resample(native.Speaker1, 16000, rate, default), actual.Speaker1, -1, "resampled source1", 1e-6);
            }
        }

        [Test]
        public void InvalidAudioCancellationDisposalAndUnsupportedBackendsAreExplicit()
        {
            Assert.Throws<ArgumentNullException>(() => MNNTigerSeparation.Load(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => MNNTigerSeparation.Load(Directory, 0));
            Assert.Throws<NotSupportedException>(() => MNNTigerSeparation.Load(Directory, backendType: MNNBackendType.NN));
            Assert.Throws<NotSupportedException>(() => MNNTigerSeparation.Load(Directory, backendType: MNNBackendType.OpenCL));
            using (var model = MNNTigerSeparation.Load(Directory, 1, MNNBackendType.CPU))
            {
                Assert.Throws<ArgumentException>(() => model.Separate(null));
                Assert.Throws<ArgumentException>(() => model.Separate(new float[320]));
                Assert.Throws<ArgumentException>(() => model.Separate(new float[48001]));
                Assert.Throws<ArgumentOutOfRangeException>(() => model.Separate(new float[400], 7999));
                var invalid = new float[400]; invalid[13] = float.PositiveInfinity;
                Assert.Throws<ArgumentException>(() => model.Separate(invalid));
                invalid[13] = float.NaN; Assert.Throws<ArgumentException>(() => model.Separate(invalid));
                Assert.Throws<OperationCanceledException>(() => model.Separate(new float[400], cancellationToken: new CancellationToken(true)));
                model.Dispose(); model.Dispose();
                Assert.Throws<ObjectDisposedException>(() => model.Separate(new float[400]));
            }
        }

        [Test]
        public void ProfileRejectsChangedWindowGraphAndExternalWeights()
        {
            string temporary = Path.Combine(Path.GetFullPath("TestArtifacts~/TigerIntegrity"), Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(temporary);
            string[] files = { MNNTigerConfig.FileName, MNNTigerModel.Graph, MNNTigerModel.Graph + ".weight" };
            try
            {
                foreach (string file in files) File.Copy(Path.Combine(Directory, file), Path.Combine(temporary, file));
                Assert.True(MNNTigerModel.IsComplete(temporary));
                var config = MNNTigerConfig.Read(temporary); config.analysis_window[1] += .1f;
                File.WriteAllText(Path.Combine(temporary, MNNTigerConfig.FileName), JsonUtility.ToJson(config));
                Assert.False(MNNTigerModel.IsComplete(temporary));
                File.Copy(Path.Combine(Directory, MNNTigerConfig.FileName), Path.Combine(temporary, MNNTigerConfig.FileName), true);
                foreach (string file in files.Skip(1))
                {
                    using (var stream = File.Open(Path.Combine(temporary, file), FileMode.Open, FileAccess.ReadWrite))
                    {
                        int first = stream.ReadByte(); stream.Position = 0; stream.WriteByte((byte)(first ^ 255));
                    }
                    Assert.False(MNNTigerModel.IsComplete(temporary));
                    Assert.Throws<InvalidDataException>(() => MNNTigerSeparation.Load(temporary));
                    File.Copy(Path.Combine(Directory, file), Path.Combine(temporary, file), true);
                }
            }
            finally { System.IO.Directory.Delete(temporary, true); }
        }

    }
}
