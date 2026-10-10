using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [Category("RepresentativeModel")]
    public class OmniGpuSpeechTests
    {
        [Test]
        public void ExplicitCpuSpeechPreservesBothCpuRuntimes()
        {
            using (var model = MNNLlm.Load(MultimodalTestData.Model("Qwen2.5-Omni-3B-MNN"), backendType: MNNBackendType.CPU))
            {
                var result = model.GenerateMultimodal("Say hello in one short sentence.", maxNewTokens: 32, generateSpeech: true, maxAudioTokens: 192);
                Assert.AreEqual(MNNBackendType.CPU, model.Backend);
                Assert.AreEqual(MNNBackendType.CPU, model.MediaBackend);
                Directory.CreateDirectory(MultimodalTestData.Artifacts);
                MultimodalTestData.SaveWave("omni-explicit-cpu.wav", result);
                using (var recognizer = MNNLlm.Load(MultimodalTestData.Model("LFM2.5-Audio-1.5B-MNN"), backendType: MNNBackendType.CPU))
                {
                    var transcript = recognizer.GenerateMultimodal("Transcribe the spoken sentence in English.", audioPath: Path.Combine(MultimodalTestData.Artifacts, "omni-explicit-cpu.wav"), maxNewTokens: 96);
                    StringAssert.Contains("hello", transcript.Text.ToLowerInvariant());
                }
            }
        }

        [TestCase(MNNPrecisionMode.Normal)]
        [TestCase(MNNPrecisionMode.Low)]
        [TestCase(MNNPrecisionMode.High)]
        public void SpeechKeepsMetalAcrossPrecisionUpgradeRepeatReloadAndStreaming(MNNPrecisionMode precision)
        {
            if (!MNNAcceleration.IsBackendAvailable(MNNBackendType.Metal))
                Assert.Ignore("Metal is unavailable on this device.");
            Directory.CreateDirectory(MultimodalTestData.Artifacts);
            for (int load = 0; load < 2; ++load)
            {
                using (var model = MNNLlm.Load(MultimodalTestData.Model("Qwen2.5-Omni-3B-MNN"), precisionMode: precision, backendType: MNNBackendType.Metal))
                {
                    // Exercise GPU media before selecting the speech processor runtime.
                    var image = model.GenerateMultimodal("What color is this image? Answer in one word.", imagePath: MultimodalTestData.Fixture("red.png"), maxNewTokens: 32);
                    StringAssert.Contains("red", image.Text.ToLowerInvariant());
                    Assert.AreEqual(MNNBackendType.Metal, model.MediaBackend);
                    for (int repeat = 0; repeat < 3; ++repeat)
                    {
                        const string hello = "Say hello in one short sentence.";
                        MNNMultimodalResult result;
                        if (repeat == 0)
                            result = model.GenerateMultimodal(hello, maxNewTokens: 32, generateSpeech: true, maxAudioTokens: 192);
                        else if (repeat == 1)
                            result = model.GenerateMultimodalAsync(hello, maxNewTokens: 32, generateSpeech: true, maxAudioTokens: 192).GetAwaiter().GetResult();
                        else
                        {
                            int updates = 0;
                            result = Task.Run(() => model.GenerateConversationStreaming(new[]{new MNNChatMessage(MNNChatRole.User, "Repeat exactly this sentence and nothing else: The capital of France is Paris.")}, _ => ++updates, maxNewTokens: 64, generateSpeech: true, maxAudioTokens: 256)).GetAwaiter().GetResult();
                            Assert.Greater(updates, 0);
                        }

                        Assert.AreEqual(MNNBackendType.Metal, model.Backend, "Speech must retain the GPU runtime.");
                        Assert.AreEqual(MNNBackendType.CPU, model.MediaBackend, "Waveform processing uses the compatible CPU runtime.");
                        string name = $"omni-hybrid-{precision}-{load}-{repeat}.wav";
                        MultimodalTestData.SaveWave(name, result);
                        using (var recognizer = MNNLlm.Load(MultimodalTestData.Model("LFM2.5-Audio-1.5B-MNN"), backendType: MNNBackendType.CPU))
                        {
                            string transcript = recognizer.GenerateMultimodal("Transcribe the spoken sentence in English.", audioPath: Path.Combine(MultimodalTestData.Artifacts, name), maxNewTokens: 96).Text;
                            TestContext.WriteLine($"precision={precision}; load={load}; repeat={repeat}; backend={model.Backend}; text={result.Text}; ASR={transcript}");
                            StringAssert.Contains(repeat < 2 ? "hello" : "paris", transcript.ToLowerInvariant());
                        }
                    }
                }
            }
        }
    }
}
