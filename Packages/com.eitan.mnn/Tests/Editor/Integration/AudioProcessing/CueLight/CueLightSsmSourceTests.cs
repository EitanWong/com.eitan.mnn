using System;
using System.Globalization;
using System.IO;
using System.Threading;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [Category("Integration"), Category("CueLight")]
    public sealed class CueLightSsmSourceTests
    {
        internal static string Root => Environment.GetEnvironmentVariable("MNN_TEST_CUELIGHT_SSM") ?? throw new InvalidOperationException("Set MNN_TEST_CUELIGHT_SSM.");
        internal static string Candidate => Path.Combine(Root, "candidate-v274");
        internal static float[] Read(string name)
        {
            var bytes = File.ReadAllBytes(Path.Combine(Root, "source-graph-v267/reference", name + ".f32"));
            var values = new float[bytes.Length / 4]; Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length); return values;
        }
        [Test]
        public void ManagedFeaturesMatchOriginalSsmCompletePcm()
        {
            var features = new MNNCueLightFeatures(Candidate);
            foreach (string name in CueLightSourceParityTests.Cases)
            {
                features.Reset(); var pcm = Read(name + "-input"); var actual = new float[((pcm.Length + 319) / 320) * 128]; var chunk = new float[320];
                for (int i = 0; i < actual.Length / 128; ++i)
                {
                    Array.Clear(chunk, 0, 320); Array.Copy(pcm, i * 320, chunk, 0, Math.Min(320, pcm.Length - i * 320));
                    Array.Copy(features.Push(chunk), 0, actual, i * 128, 128);
                }
                CueLightPcmReference.Check(Read(name + "-features"), actual, .0001, name + ";ssm_features");
            }
        }
        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Auto)]
        public void OriginalSsmSixOutputsReplayAndEndpointsMatchFullPcm(MNNBackendType backend) =>
            CueLightPcmReference.Verify(Candidate, Read, CueLightSourceParityTests.Cases, backend);

        [Test]
        public void SsmStreamingResetLifecycleAndBackendEligibilityAreEnforced()
        {
            Assert.Throws<NotSupportedException>(() => MNNCueLightTurnDetector.Load(Candidate, backendType: MNNBackendType.Metal));
            Assert.Throws<NotSupportedException>(() => MNNCueLightTurnDetector.Load(Candidate, backendType: MNNBackendType.NN));
            using (var model = MNNCueLightTurnDetector.Load(Candidate))
            {
                Assert.AreEqual("CueLight-Nano-SSM-MNN", model.ModelName); Assert.AreEqual(MNNBackendType.CPU, model.Backend);
                Assert.Throws<ArgumentException>(() => model.Push(new float[319])); Assert.Throws<ArgumentException>(() => model.Push(new float[321]));
                Assert.Throws<ArgumentException>(() => model.Analyze(Array.Empty<float>())); Assert.Throws<ArgumentException>(() => model.Analyze(new float[960001]));
                Assert.Throws<ArgumentException>(() => model.Analyze(new[] { float.NaN })); Assert.Throws<ArgumentException>(() => model.Analyze(new[] { float.NegativeInfinity }));
                Assert.Throws<ArgumentOutOfRangeException>(() => model.Analyze(new float[1], 8000));
                Assert.Throws<OperationCanceledException>(() => model.Push(new float[320], cancellationToken: new CancellationToken(true)));
                var pcm = Read("long_call-input"); var expected = Read("long_call-events"); var chunk = new float[320];
                for (int i = 0; i < expected.Length / 5; ++i)
                {
                    Array.Clear(chunk, 0, 320); Array.Copy(pcm, i * 320, chunk, 0, Math.Min(320, pcm.Length - i * 320));
                    var f = model.Push(chunk); AssertFrame(expected, i, f);
                }
                model.Reset(); var first = model.Push(new float[320]); model.Reset();
                CollectionAssert.AreEqual(first.Probabilities, model.Push(new float[320]).Probabilities);
                model.Analyze(new[] { 1.1f }); var next = model.Push(new float[320]);
                model.Reset(); var initial = new float[320]; initial[0] = 1.1f; model.Push(initial);
                CollectionAssert.AreEqual(next.Probabilities, model.Push(new float[320]).Probabilities, "Analyze state continues into Push");
                model.Dispose(); Assert.Throws<ObjectDisposedException>(() => model.Push(new float[320])); Assert.Throws<ObjectDisposedException>(() => model.Reset());
            }
        }
        internal static void AssertFrame(float[] expected, int i, MNNCueLightFrame f)
        {
            Assert.Less(Math.Abs((double)f.SpeechProbability - expected[i * 5]), .0001);
            Assert.Less(Math.Abs((double)f.EndOfTurnProbability - expected[i * 5 + 1]), .0001);
            Assert.AreEqual((int)expected[i * 5 + 2], f.ResetCount);
            Assert.AreEqual(expected[i * 5 + 3] == 1, f.SnappyEndpoint); Assert.AreEqual(expected[i * 5 + 4] == 1, f.CarefulEndpoint);
        }
        [Test]
        public void SsmIdentityDimensionsGraphsAndCoefficientsRejectSubstitution()
        {
            string dir = Path.Combine(Root, "tamper-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
            try
            {
                foreach (string path in Directory.GetFiles(Candidate)) File.Copy(path, Path.Combine(dir, Path.GetFileName(path)));
                string configPath = Path.Combine(dir, MNNCueLightConfig.FileName); string original = File.ReadAllText(configPath);
                foreach (string modified in new[] { original.Replace("4f686d9a", "0f686d9a"), original.Replace("106", "112"),
                    original.Replace("sshah03/cuelight-nano-ssm", "sshah03/cuelight-nano"), original.Replace("\"metal_graph\": \"\"", "\"metal_graph\": \"step_accurate_fp32.mnn\"") })
                {
                    Assert.AreNotEqual(original, modified); File.WriteAllText(configPath, modified);
                    Assert.False(MNNCueLightModel.IsComplete(dir)); Assert.Throws<InvalidDataException>(() => MNNCueLightTurnDetector.Load(dir));
                }
                File.WriteAllText(configPath, original); Assert.True(MNNCueLightModel.IsComplete(dir));
                foreach (string name in new[] { MNNCueLightModel.CpuGraph, MNNCueLightConfig.CoefficientsFile })
                {
                    string path = Path.Combine(dir, name); var bytes = File.ReadAllBytes(path); bytes[4000] ^= 1; File.WriteAllBytes(path, bytes);
                    Assert.False(MNNCueLightModel.IsComplete(dir)); Assert.Throws<InvalidDataException>(() => MNNCueLightTurnDetector.Load(dir));
                    File.Copy(Path.Combine(Candidate, name), path, true);
                }
            }
            finally { Directory.Delete(dir, true); }
        }
        [Test]
        public void SsmAuthorThresholdsAndSilencePoliciesRemainDistinctFromGru()
        {
            var profile = MNNCueLightModel.ReadVerified(Candidate); Assert.AreEqual(.8, profile.SnappyThreshold); Assert.AreEqual(.8, profile.CarefulThreshold);
            foreach (bool careful in new[] { false, true })
            {
                var endpoint = new MNNCueLightEndpoint(careful ? profile.CarefulThreshold : profile.SnappyThreshold, careful ? .7 : .5, careful ? 2 : 1.5);
                Assert.False(endpoint.Update(.5f, 0)); int frames = careful ? 35 : 25;
                for (int i = 0; i < frames; ++i) Assert.False(endpoint.Update(0, .799999f));
                Assert.True(endpoint.Update(0, .8f)); Assert.False(endpoint.Update(0, 1));
            }
        }
        [Test]
        public void StudioDiscoversSsmAndExecutesOriginalLongCall()
        {
            var model = MNNStudioModel.Read(Candidate); Assert.AreEqual(MNNStudioTask.AudioProcessing, model.Task);
            Assert.True(model.HasAudioProcessingPipeline); Assert.True(MNNStudioTasks.Get((int)model.Task).CanRun(model));
            Assert.AreEqual(MNNModelCatalogCategory.TurnDetection.ToString(), MNNModelCatalog.Classify("example/CueLight-Nano-SSM-MNN"));
            using (var backend = MNNStudioNativeBackend.Load(model, 8, false))
            {
                string wav = Path.Combine(CueLightSourceParityTests.Root, "studio-reference-v252/long_call.wav");
                var result = backend.Run(new MNNStudioRequest { Audio = wav });
                StringAssert.Contains("CueLight-Nano-SSM-MNN", result.BackendDescription); StringAssert.Contains("CPU", result.BackendDescription);
                var lines = result.Text.Substring(result.Text.IndexOf("time_seconds", StringComparison.Ordinal)).TrimEnd().Split('\n');
                var expected = Read("long_call-events"); Assert.AreEqual(expected.Length / 5 + 1, lines.Length);
                for (int i = 1; i < lines.Length; ++i)
                {
                    var c = lines[i].Split('\t'); Assert.AreEqual(6, c.Length); Assert.AreEqual(i * .02, double.Parse(c[0], CultureInfo.InvariantCulture), 1e-10);
                    Assert.Less(Math.Abs(float.Parse(c[1], CultureInfo.InvariantCulture) - expected[(i - 1) * 5]), .0001);
                    Assert.Less(Math.Abs(float.Parse(c[2], CultureInfo.InvariantCulture) - expected[(i - 1) * 5 + 1]), .0001);
                    for (int j = 0; j < 3; ++j) Assert.AreEqual((int)expected[(i - 1) * 5 + j + 2], int.Parse(c[j + 3]));
                }
                Assert.Throws<ArgumentException>(() => backend.Run(new MNNStudioRequest()));
                Assert.Throws<OperationCanceledException>(() => backend.Run(new MNNStudioRequest { Audio = wav, Cancellation = new CancellationToken(true) }));
            }
        }
    }
}
