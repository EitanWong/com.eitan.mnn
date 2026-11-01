using System;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [Category("Integration"), Category("CueLight")]
    public sealed class CueLightSourceParityTests
    {
        internal static string Root => Environment.GetEnvironmentVariable("MNN_TEST_CUELIGHT") ?? throw new InvalidOperationException("Set MNN_TEST_CUELIGHT.");
        internal static string Candidate => Path.Combine(Root, "candidate-v250");
        internal static readonly string[] Cases = { "minimum", "hop_before", "hop_at", "hop_after", "frame_before", "frame_at", "frame_after",
            "real", "mixed_language", "quiet", "gain2", "silence", "constant", "noise", "context_before", "context_at", "context_after",
            "reset_before", "reset_at", "reset_after", "long_call", "maximum" };
        internal static float[] Read(string name)
        {
            var bytes = File.ReadAllBytes(Path.Combine(Root, "source-graph-v237/reference", name + ".f32"));
            var values = new float[bytes.Length / 4]; Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length); return values;
        }
        internal static void Check(float[] expected, float[] actual, double gate, string label) =>
            CueLightPcmReference.Check(expected, actual, gate, label);
        [Test]
        public void ManagedCausalFeaturesMatchOriginalCompletePcm()
        {
            var features = new MNNCueLightFeatures(Candidate);
            foreach (string name in Cases)
            {
                features.Reset(); var pcm = Read(name + "-input"); var actual = new float[((pcm.Length + 319) / 320) * 128]; var chunk = new float[320];
                for (int i = 0; i < actual.Length / 128; ++i)
                {
                    Array.Clear(chunk, 0, 320); Array.Copy(pcm, i * 320, chunk, 0, Math.Min(320, pcm.Length - i * 320));
                    Array.Copy(features.Push(chunk), 0, actual, i * 128, 128);
                }
                Check(Read(name + "-features"), actual, .0001, name + ";features");
            }
        }
        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal), TestCase(MNNBackendType.Auto)]
        public void OriginalSixOutputsResetReplayAndEndpointsMatchFullPcm(MNNBackendType backend)
        {
            CueLightPcmReference.Verify(Candidate, Read, Cases, backend);
        }
        [Test]
        public void PushResetInputCancellationAndDisposalPreserveContract()
        {
            using (var model = MNNCueLightTurnDetector.Load(Candidate))
            {
                Assert.Throws<ArgumentException>(() => model.Push(new float[319]));
                Assert.Throws<ArgumentException>(() => model.Push(new float[321]));
                Assert.Throws<ArgumentException>(() => model.Analyze(Array.Empty<float>()));
                Assert.Throws<ArgumentException>(() => model.Analyze(new float[960001]));
                Assert.Throws<ArgumentException>(() => model.Analyze(new[] { float.NaN }));
                Assert.Throws<ArgumentException>(() => model.Analyze(new[] { float.PositiveInfinity }));
                Assert.Throws<ArgumentOutOfRangeException>(() => model.Analyze(new float[1], 8000));
                Assert.Throws<OperationCanceledException>(() => model.Push(new float[320], cancellationToken: new CancellationToken(true)));
                Assert.Throws<NotSupportedException>(() => MNNCueLightTurnDetector.Load(Candidate, backendType: MNNBackendType.NN));
                var pcm = Read("long_call-input"); var expected = Read("long_call-events"); model.Reset();
                var chunk = new float[320];
                for (int i = 0; i < expected.Length / 5; ++i)
                {
                    Array.Clear(chunk, 0, 320); Array.Copy(pcm, i * 320, chunk, 0, Math.Min(320, pcm.Length - i * 320));
                    var result = model.Push(chunk); Assert.Less(Math.Abs((double)result.SpeechProbability - expected[i * 5]), .0001);
                    Assert.Less(Math.Abs((double)result.EndOfTurnProbability - expected[i * 5 + 1]), .0001); Assert.AreEqual((int)expected[i * 5 + 2], result.ResetCount);
                }
                model.Reset(); var first = model.Push(new float[320]); model.Reset();
                CollectionAssert.AreEqual(first.Probabilities, model.Push(new float[320]).Probabilities);
                Assert.AreEqual(1, model.Analyze(new[] { 1.1f }).FrameCount);
                model.Dispose(); Assert.Throws<ObjectDisposedException>(() => model.Push(new float[320])); Assert.Throws<ObjectDisposedException>(() => model.Reset());
            }
        }
        [Test]
        public void AuthorOnsetAndEndpointThresholdBoundariesAreExact()
        {
            var onset = new MNNCueLightOnset(); for (int i = 0; i < 99; ++i) Assert.False(onset.Update(0));
            for (int i = 0; i < 3; ++i) Assert.False(onset.Update(.5f));
            for (int i = 0; i < 100; ++i) Assert.False(onset.Update(0));
            Assert.False(onset.Update(.5f)); Assert.False(onset.Update(.5f)); Assert.True(onset.Update(.5f)); Assert.False(onset.Update(.5f));
            var endpoint = new MNNCueLightEndpoint(.73, .5, 1.5);
            for (int i = 0; i < 100; ++i) Assert.False(endpoint.Update(0, 1));
            Assert.False(endpoint.Update(.5f, 1)); for (int i = 0; i < 24; ++i) Assert.False(endpoint.Update(0, .73f));
            Assert.True(endpoint.Update(0, .73f)); Assert.False(endpoint.Update(0, 1));
            Assert.False(endpoint.Update(.5f, 0)); for (int i = 0; i < 74; ++i) Assert.False(endpoint.Update(0, 0)); Assert.True(endpoint.Update(0, 0));
        }
        [Test]
        public void ModifiedRuntimeCoefficientsAreRejected()
        {
            string dir = Path.Combine(Root, "tamper-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
            try
            {
                File.Copy(Path.Combine(Candidate, MNNCueLightConfig.FileName), Path.Combine(dir, MNNCueLightConfig.FileName));
                var data = File.ReadAllBytes(Path.Combine(Candidate, MNNCueLightConfig.CoefficientsFile)); data[4000] ^= 1;
                File.WriteAllBytes(Path.Combine(dir, MNNCueLightConfig.CoefficientsFile), data);
                Assert.False(MNNCueLightModel.IsComplete(dir)); Assert.Throws<InvalidDataException>(() => MNNCueLightTurnDetector.Load(dir));
                File.WriteAllBytes(Path.Combine(dir, MNNCueLightConfig.CoefficientsFile), new byte[3]); Assert.Throws<InvalidDataException>(() => new MNNCueLightFeatures(dir));
            }
            finally { Directory.Delete(dir, true); }
        }
    }
}
