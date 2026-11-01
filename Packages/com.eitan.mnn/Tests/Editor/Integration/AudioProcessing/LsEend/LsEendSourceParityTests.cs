using System;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [Category("Integration"), Category("LsEend")]
    public sealed class LsEendSourceParityTests
    {
        private static string Root => Environment.GetEnvironmentVariable("MNN_TEST_LS_EEND")
            ?? throw new InvalidOperationException("Set MNN_TEST_LS_EEND to frozen LS-EEND evidence.");
        private static string Candidate => Path.Combine(Root, "candidate-v216");
        private static string Reference => Path.Combine(Root, "source-graphs-v208", "reference");
        private static readonly string[] Cases = { "minimum", "short", "real", "quiet", "gain2", "silence", "constant",
            "hop_before", "hop_at", "hop_after", "subsample_before", "subsample_at", "subsample_after",
            "delay_before", "delay_at", "delay_after", "chunk_before", "chunk_at", "chunk_after", "maximum" };
        private static float[] Read(string name)
        {
            var bytes = File.ReadAllBytes(Path.Combine(Reference, name + ".f32"));
            var values = new float[bytes.Length / 4]; Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length); return values;
        }
        private static void Check(float[] expected, float[] actual, double gate, string label)
        {
            Assert.AreEqual(expected.Length, actual.Length, label);
            double maximum = 0;
            for (int i = 0; i < actual.Length; ++i)
            {
                Assert.False(float.IsNaN(actual[i]) || float.IsInfinity(actual[i]), label);
                maximum = Math.Max(maximum, Math.Abs((double)actual[i] - expected[i]));
            }
            TestContext.WriteLine(label + ";maximum_absolute=" + maximum.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            Assert.Less(maximum, gate, label);
        }
        [Test]
        public void ManagedFbankMatchesAllOriginalAudioConditions()
        {
            var config = MNNLsEendConfig.Read(Candidate);
            var coefficients = new byte[200 * 8 + 23 * 129 * 4];
            Buffer.BlockCopy(config.analysis_window, 0, coefficients, 0, 200 * 8);
            Buffer.BlockCopy(config.mel_filters, 0, coefficients, 200 * 8, 23 * 129 * 4);
            File.WriteAllBytes(Path.Combine(Root, "managed-coefficients-v219.bin"), coefficients);
            File.WriteAllText(Path.Combine(Root, "managed-profile-v219.json"), UnityEngine.JsonUtility.ToJson(config, true));
            var features = new MNNLsEendFeatures(config);
            foreach (string name in Cases)
            {
                var values = features.Extract(Read(name + "-input"), default);
                var bytes = new byte[values.Length * 4]; Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
                File.WriteAllBytes(Path.Combine(Root, "managed-" + name + "-features-v218.f32"), bytes);
                Check(Read(name + "-features"), values, .0001, name);
            }
        }
        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal), TestCase(MNNBackendType.Auto)]
        public void CompleteWaveformAndStateResetMatchOriginalPosteriors(MNNBackendType backend)
        {
            using (var model = MNNLsEendDiarization.Load(Candidate, MNNLsEendDiarization.DefaultThreadCount, backend))
            {
                foreach (string name in Cases.Concat(new[] { Cases[0] }))
                {
                    var pcm = Read(name + "-input");
                    var actual = model.Analyze(pcm);
                    Check(Read(name + "-posterior"), actual.Probabilities, .0001, backend + ";" + name);
                    CollectionAssert.AreEqual(actual.Probabilities, model.Analyze(pcm).Probabilities, name + " repeat");
                    Assert.AreEqual((pcm.Length + 799) / 800, actual.FrameCount);
                    Assert.AreEqual((double)pcm.Length / 8000, actual.DurationSeconds);
                    Assert.AreEqual(File.ReadAllText(Path.Combine(Root, "managed-reference-v216", name + ".rttm")), actual.ToRttm(name));
                }
                var selected = backend == MNNBackendType.Auto ? MNNBackendType.CPU : backend;
                Assert.AreEqual(selected, model.EncoderBackend); Assert.AreEqual(selected, model.DecoderBackend);
            }
        }
        [Test]
        public void OriginalThresholdMedianAndAllNineSlotsProduceExactRttm()
        {
            foreach (string name in Cases)
            {
                var result = new MNNLsEendActivity(Read(name + "-posterior"), Read(name + "-input").Length / 8000.0);
                Assert.AreEqual(File.ReadAllText(Path.Combine(Root, "managed-reference-v216", name + ".rttm")), result.ToRttm(name));
            }
            var bytes = File.ReadAllBytes(Path.Combine(Root, "managed-reference-v216/custom-posterior.f32"));
            var custom = new float[bytes.Length / 4]; Buffer.BlockCopy(bytes, 0, custom, 0, bytes.Length);
            var activity = new MNNLsEendActivity(custom, 3);
            Assert.AreEqual(File.ReadAllText(Path.Combine(Root, "managed-reference-v216/custom.rttm")), activity.ToRttm("custom"));
            Assert.Throws<ArgumentException>(() => activity.ToRttm("bad id"));
        }
        [Test]
        public void InputBoundsCancellationAndDisposalAreExplicit()
        {
            using (var model = MNNLsEendDiarization.Load(Candidate))
            {
                Assert.Throws<ArgumentException>(() => model.Analyze(Array.Empty<float>()));
                Assert.Throws<ArgumentException>(() => model.Analyze(new float[480001]));
                Assert.Throws<ArgumentOutOfRangeException>(() => model.Analyze(new float[1], 16000));
                Assert.Throws<ArgumentException>(() => model.Analyze(new[] { float.NaN }));
                Assert.Throws<ArgumentException>(() => model.Analyze(new[] { float.PositiveInfinity }));
                Assert.AreEqual(1, model.Analyze(new[] { 1.1f }).FrameCount);
                Assert.Throws<OperationCanceledException>(() => model.Analyze(new float[1], cancellationToken: new CancellationToken(true)));
                Assert.Throws<NotSupportedException>(() => MNNLsEendDiarization.Load(Candidate, backendType: MNNBackendType.NN));
                model.Dispose();
                Assert.Throws<ObjectDisposedException>(() => model.Analyze(new float[1]));
            }
        }
        [Test]
        public void ModifiedOrTruncatedRuntimeCoefficientsAreRejected()
        {
            string directory = Path.Combine(Root, "tampered-profile-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(directory);
            try
            {
                File.Copy(Path.Combine(Candidate, MNNLsEendConfig.FileName), Path.Combine(directory, MNNLsEendConfig.FileName));
                var bytes = File.ReadAllBytes(Path.Combine(Candidate, MNNLsEendConfig.CoefficientsFile));
                bytes[100] ^= 1;
                File.WriteAllBytes(Path.Combine(directory, MNNLsEendConfig.CoefficientsFile), bytes);
                Assert.False(MNNLsEendModel.IsComplete(directory));
                Assert.Throws<InvalidDataException>(() => MNNLsEendDiarization.Load(directory));
                File.WriteAllBytes(Path.Combine(directory, MNNLsEendConfig.CoefficientsFile), new byte[3]);
                Assert.Throws<InvalidDataException>(() => MNNLsEendConfig.Read(directory));
            }
            finally { System.IO.Directory.Delete(directory, true); }
        }
    }
}
