using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public class CosyVoice300MMetalStabilityTests
    {
        [TestCase(1)]
        [TestCase(4)]
        public void EvictedMetalLengthsRebuildSafely(int capacity)
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_COSYVOICE300M_ROOT");
            if (string.IsNullOrEmpty(root)) Assert.Ignore("Requires existing local model; no downloads.");
            Task.Run(() =>
            {
                string[] texts = {"你好，今天我们一起学习。", "今天阳光很好，我们出去走走。", "请把这本书放回书架。", "我们明天下午在公园门口见面。",
                    "沿着这条小路往前走，就能看见山脚下的湖泊。"};
                using (var model = MNNCosyVoice300M.Load(root, acousticBackend: MNNBackendType.Metal, metalShapeCacheCapacity: capacity))
                {
                    foreach (int phrase in new[] {0, 1, 2, 3, 4, 0})
                    {
                        var audio = model.Synthesize(texts[phrase]);
                        Assert.IsFalse(model.LastAcousticCpuRetry, "Eviction must rebuild without cross-shape reuse.");
                        Assert.AreEqual(MNNBackendType.Metal, model.FlowBackend);
                        Assert.AreEqual(MNNBackendType.Metal, model.VocoderBackend);
                        AdditionalTtsValidation.ValidateAndSave(audio, $"evicted-{capacity}-{phrase}.wav", 22050);
                    }
                }
            }).GetAwaiter().GetResult();
        }

        [TestCase(MNNCosyVoice300MDecoderMode.Float16)]
        [TestCase(MNNCosyVoice300MDecoderMode.ExperimentalInt8)]
        public void MixedLengthsRetainMetalAndValidAudio(MNNCosyVoice300MDecoderMode mode)
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_COSYVOICE300M_ROOT");
            if (string.IsNullOrEmpty(root)) Assert.Ignore("Set existing local model root; no downloads.");
            if (mode != MNNCosyVoice300MDecoderMode.Float16 && !File.Exists(Path.Combine(root, MNNCosyVoice300MModel.ConservativeGraph(mode))))
                Assert.Ignore("Explicitly generated INT8 candidate is required.");
            Task.Run(() =>
            {
                string[] texts = {"你好，欢迎使用语音合成。", "The nearest station is across the street, next to the post office.",
                    "图书馆下午五点关门，请提前归还借阅的杂志。", "Please keep the microphone close to your mouth and speak clearly."};
                Directory.CreateDirectory(AdditionalTtsValidation.Artifacts);
                string csv = Path.Combine(AdditionalTtsValidation.Artifacts, "metal-stability-" + mode + ".csv");
                File.WriteAllText(csv, "phrase,run,tokens,token_seconds,flow_seconds,vocoder_seconds,seconds,audio_seconds,rtf,flow,vocoder,retry\n");
                using (var model = MNNCosyVoice300M.Load(root, acousticBackend: MNNBackendType.Metal, decoderMode: mode))
                {
                    for (int run = 0; run < 3; ++run)
                    foreach (int phrase in new[] {0, 1, 2, 3, 0, 2})
                    {
                        var audio = model.Synthesize(texts[phrase]);
                        string F(double v) => v.ToString("R", CultureInfo.InvariantCulture);
                        File.AppendAllText(csv, string.Join(",", phrase, run, model.LastSpeechTokens, F(model.LastTokenSeconds),
                            F(model.LastFlowSeconds), F(model.LastVocoderSeconds), F(model.LastSynthesisSeconds),
                            F(audio.Waveform.Length / 22050d), F(model.LastRealTimeFactor), model.FlowBackend, model.VocoderBackend, model.LastAcousticCpuRetry) + "\n");
                        Assert.IsFalse(model.LastAcousticCpuRetry, $"{mode}, run={run}, phrase={phrase}");
                        Assert.AreEqual(MNNBackendType.Metal, model.FlowBackend);
                        Assert.AreEqual(MNNBackendType.Metal, model.VocoderBackend);
                        AdditionalTtsValidation.ValidateAndSave(audio, $"stable-{mode}-{phrase}-{run}.wav", 22050);
                        if (run == 2) File.WriteAllText(Path.Combine(AdditionalTtsValidation.Artifacts, $"stable-{mode}-{phrase}-{run}.expected.txt"), texts[phrase]);
                    }
                }
            }).GetAwaiter().GetResult();
        }
    }
}
