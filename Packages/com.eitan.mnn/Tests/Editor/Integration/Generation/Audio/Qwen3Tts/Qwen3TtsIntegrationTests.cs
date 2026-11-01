using System;
using System.IO;
using System.Threading;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public class Qwen3TtsIntegrationTests
    {
        [TestCase("INT8")]
        [TestCase("FP16")]
        public void SynthesizesChineseAndEnglishWithOfficialTalker(string precision)
        {
            Assert.Ignore("Qwen3-TTS waveform callback ABI is not implemented; historical diagnostics are not current inference validation.");
            string root = Environment.GetEnvironmentVariable("MNN_TEST_QWEN3_TTS_ROOT");
            Assert.That(root, Is.Not.Null.And.Not.Empty);
            using (var model = MNNQwen3Tts.Load(Path.Combine(root, "Qwen3-TTS-" + precision)))
            {
                string reference = Path.Combine(AdditionalTtsValidation.Artifacts, "sherpa-vits-suyingxue.wav");
                Assert.Throws<ArgumentException>(() => model.Synthesize("hello", reference, "unknown"));
                Assert.Throws<OperationCanceledException>(() => model.Synthesize("你好", reference, cancellationToken: new CancellationToken(true)));
                var chinese = model.Synthesize("你好，这是语音合成测试。", reference, "chinese", maxFrames: 128);
                string path = AdditionalTtsValidation.ValidateAndSave(chinese, "qwen3-tts-" + precision + "-zh.wav", 24000);
                AdditionalTtsValidation.Transcribe(path, true);
                var english = model.SynthesizeAsync("Paris is the capital of France.", reference, "english", maxFrames: 128).GetAwaiter().GetResult();
                path = AdditionalTtsValidation.ValidateAndSave(english, "qwen3-tts-" + precision + "-en.wav", 24000);
                AdditionalTtsValidation.Transcribe(path, false);
            }
        }
    }
}
