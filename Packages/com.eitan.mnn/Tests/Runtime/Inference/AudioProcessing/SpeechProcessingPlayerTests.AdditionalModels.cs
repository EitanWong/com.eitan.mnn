using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    public sealed partial class SpeechProcessingPlayerTests
    {
        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal)]
        public void SileroPosteriorsAndRealtimeMatchOfficialSourceInBuiltPlayer(MNNBackendType backend)
        {
            RequirePlayer(backend);
            string directory = Model("Silero-v5.1-MNN");
            Assert.True(MNNSileroModel.IsOfficialProfile(MNNSileroVadConfig.Read(directory)), "The packaged Silero identity must select its official FP32 graph.");
            using (var model = MNNSileroVad.Load(directory, 1, backend))
            {
                foreach (string name in new[]{"en", "zh-en", "en-padded", "zh-en-padded", "two-utterances", "noise", "tone", "quiet", "silence", "short", "en"})
                {
                    var expected = Read("MNN_TEST_SILERO_REFERENCE", name + "-probabilities");
                    var actual = model.Detect(Read("MNN_TEST_SILERO_REFERENCE", name + "-input"), 16000).Probabilities;
                    Assert.AreEqual(expected.Length, actual.Count);
                    Assert.That(expected.Select((value,i)=>Math.Abs(value-actual[i])).Max(),Is.LessThan(.0002));
                }
                Assert.AreEqual(backend, model.Backend);
            }
            using (var realtime = MNNRealtimeVoiceActivity.Load(directory, backendType:backend))
            {
                var pcm = Read("MNN_TEST_SILERO_REFERENCE", "en-input");
                var expected = Read("MNN_TEST_SILERO_REFERENCE", "en-probabilities");
                var frame = new float[512];
                for (int repeat=0;repeat<2;++repeat)
                {
                    realtime.Reset();
                    for (int i=0;i<expected.Length;++i)
                    {
                        Array.Clear(frame,0,frame.Length);
                        Array.Copy(pcm,i*512,frame,0,Math.Min(512,pcm.Length-i*512));
                        Assert.That(Math.Abs(realtime.ProcessFrame(frame,0).SpeechProbability-expected[i]),Is.LessThan(.0002));
                    }
                }
                Assert.AreEqual(backend,realtime.Backend);
            }
        }

        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal)]
        public void SmartTurnProbabilitiesMatchOfficialFloatingSourceInBuiltPlayer(MNNBackendType backend)
        {
            RequirePlayer(backend);
            string directory = Model("SmartTurn-v3.2-FP32-MNN");
            Assert.True(MNNSmartTurnModel.IsOfficialProfile(MNNSmartTurnConfig.Read(directory)));
            string reference = Environment.GetEnvironmentVariable("MNN_TEST_SMART_TURN_REFERENCE");
            if (string.IsNullOrWhiteSpace(reference)) Assert.Ignore("Set MNN_TEST_SMART_TURN_REFERENCE to the official floating ONNX oracle.");
            var rows = JsonUtility.FromJson<SmartTurnCases>(File.ReadAllText(Path.Combine(reference, "cases.json"))).rows;
            using (var model = MNNSmartTurn.Load(directory, 4, backend))
            {
                foreach (var row in rows.Concat(new[] { rows.Single(value => value.name == "en") }))
                {
                    float actual = model.Predict(Read("MNN_TEST_SMART_TURN_REFERENCE", row.name + "-input"), 16000).CompletionProbability;
                    Assert.That(Math.Abs(actual - row.probability), Is.LessThan(.0002), row.name);
                    Assert.AreEqual(row.probability > .5f, actual > .5f, row.name);
                }
                Assert.AreEqual(backend, model.Backend);
            }
        }

        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal)]
        public void FullSubNetWaveformMatchesOriginalPyTorchInBuiltPlayer(MNNBackendType backend)
        {
            RequirePlayer(backend);
            using (var model = MNNFullSubNet.Load(Model("FullSubNet-MNN"), 1, backend))
            {
                foreach (string name in new[]{"speech_0.001", "speech_1", "tone", "noise", "long", "speech_1"})
                    CheckWaveform(model.Enhance(Read("MNN_TEST_FULLSUBNET_REFERENCE", "conditions/" + name + "-input")),
                        Read("MNN_TEST_FULLSUBNET_REFERENCE", "conditions/" + name + "-enhanced"), .002);
                Assert.AreEqual(backend, model.Backend);
            }
        }

        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal)]
        public void MarbleNetPosteriorsMatchNemoOnnxInBuiltPlayer(MNNBackendType backend)
        {
            RequirePlayer(backend);
            using (var model = MNNMarbleNetVad.Load(Model("MarbleNet-VAD-MNN"), 1, backend))
            {
                foreach (string name in new[]{"en", "zh_en", "silence", "tone", "noise", "short", "en"})
                {
                    var expected = Read("MNN_TEST_MARBLENET_REFERENCE", name + "-probabilities");
                    var actual = model.Detect(Read("MNN_TEST_MARBLENET_REFERENCE", name + "-input"), 16000).Probabilities;
                    Assert.AreEqual(expected.Length, actual.Count);
                    Assert.That(expected.Select((value, i) => Math.Abs(value - actual[i])).Max(), Is.LessThan(.002));
                }
                Assert.AreEqual(backend, model.Backend);
            }
        }

        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal)]
        public void GtcrnWaveformMatchesOfficialPyTorchInBuiltPlayer(MNNBackendType backend)
        {
            RequirePlayer(backend);
            using (var model = MNNGtcrn.Load(Model("GTCRN-MNN"), 1, backend))
            {
                foreach (string name in new[]{"speech_0.001", "speech_1", "noise", "long", "speech_1"})
                    CheckWaveform(model.Enhance(Read("MNN_TEST_GTCRN_REFERENCE", "conditions/" + name + "-input")),
                        Read("MNN_TEST_GTCRN_REFERENCE", "conditions/" + name + "-enhanced"), .002);
                Assert.AreEqual(backend, model.Backend);
            }
        }

        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal)]
        public void FsmnPosteriorsMatchOfficialPyTorchInBuiltPlayer(MNNBackendType backend)
        {
            RequirePlayer(backend);
            var input = Read("MNN_TEST_FSMN_REFERENCE", "input");
            var expected = Read("MNN_TEST_FSMN_REFERENCE", "probabilities");
            using (var model = MNNFsmnVad.Load(Model("FSMN-VAD-16K-MNN"), 1, backend))
            {
                var actual = model.Detect(input, 16000).Probabilities.ToArray();
                Assert.AreEqual(expected.Length, actual.Length);
                Assert.That(actual.Select((value, i) => Math.Abs(value - expected[i])).Max(), Is.LessThan(.002));
                var repeated = model.Detect(input, 16000).Probabilities;
                Assert.That(actual.Select((value, i) => Math.Abs(value - repeated[i])).Max(), Is.LessThan(1e-6));
                Assert.AreEqual(backend, model.Backend);
            }
        }

        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal)]
        public void FrcrnWaveformMatchesOfficialDspOnnxInBuiltPlayer(MNNBackendType backend)
        {
            RequirePlayer(backend);
            var input = Read("MNN_TEST_FRCRN_REFERENCE", "input");
            var expected = Read("MNN_TEST_FRCRN_REFERENCE", "enhanced");
            using (var model = MNNFrcrnSe.Load(Model("FRCRN-SE-16K-MNN"), 2, backend))
            {
                Verify(() => model.Enhance(input), expected, .005);
                Assert.AreEqual(backend, model.Backend);
            }
        }

        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal)]
        public void MossFormerShortLongShortMatchesPyTorchInBuiltPlayer(MNNBackendType backend)
        {
            RequirePlayer(backend);
            using (var model = MNNMossFormer2Separation.Load(Model("MossFormer2-SS-16K-MNN"), 2, backend))
            {
                foreach (string name in new[]{"short", "long", "short"})
                {
                    var input = Read("MNN_TEST_MOSS_REFERENCE", name + "-input");
                    var actual = model.Separate(input);
                    CheckWaveform(actual.Speaker0, Read("MNN_TEST_MOSS_REFERENCE", name + "-spk0"), .005);
                    CheckWaveform(actual.Speaker1, Read("MNN_TEST_MOSS_REFERENCE", name + "-spk1"), .005);
                }
                Assert.AreEqual(backend, model.Backend);
            }
        }

        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal)]
        public void LiveKitTranscriptsMatchOriginalOnnxInBuiltPlayer(MNNBackendType backend)
        {
            RequirePlayer(backend);
            string directory = Environment.GetEnvironmentVariable("MNN_TEST_LIVEKIT_REFERENCE");
            if (string.IsNullOrWhiteSpace(directory)) Assert.Ignore("Set MNN_TEST_LIVEKIT_REFERENCE to the prepared official transcript oracle.");
            var cases = JsonUtility.FromJson<TurnCases>(File.ReadAllText(Path.Combine(directory, "cases.json")));
            using (var model = MNNLiveKitTurnDetector.Load(Model("LiveKit-Turn-Detector-MNN"), 2, backend))
            {
                foreach (var row in cases.rows)
                {
                    var messages = row.messages.Select(value => new MNNLiveKitTurnMessage(value.role, value.content)).ToArray();
                    float actual = model.Predict(messages);
                    Assert.That(Math.Abs(actual - row.probability_fp32), Is.LessThan(backend == MNNBackendType.CPU ? .00002 : .0005), row.name);
                    Assert.That(Math.Abs(actual - model.Predict(messages)), Is.LessThan(1e-6), row.name + " repeated");
                }
                Assert.AreEqual(backend, model.Backend);
            }
        }

        private static void CheckWaveform(float[] actual, float[] expected, double tolerance)
        {
            Assert.AreEqual(expected.Length, actual.Length);
            double error = Math.Sqrt(actual.Select((value, i) => Math.Pow((double)value - expected[i], 2)).Sum() / expected.Sum(value => (double)value * value));
            TestContext.WriteLine("Waveform NRMSE=" + error + "; samples=" + actual.Length);
            Assert.That(error, Is.LessThan(tolerance));
        }

        [Serializable] private sealed class TurnMessage { public string role, content; }
        [Serializable] private sealed class TurnRow
        {
            public string name;
            public TurnMessage[] messages;
            public float probability_fp32;
        }
        [Serializable] private sealed class TurnCases { public TurnRow[] rows; }
        [Serializable] private sealed class SmartTurnRow { public string name; public float probability; }
        [Serializable] private sealed class SmartTurnCases { public SmartTurnRow[] rows; }
    }
}
