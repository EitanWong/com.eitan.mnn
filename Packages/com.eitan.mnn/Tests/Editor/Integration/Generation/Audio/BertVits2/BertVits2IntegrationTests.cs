using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [Category("RepresentativeModel")]
    public class BertVits2IntegrationTests
    {
        [Test]
        public void ChineseSpeechRepeatReloadDisposeAndAsr()
        {
            const string phrase = "你好，欢迎使用语音合成。";
            string wav;
            var model = MNNBertVits2.Load(GenerationTestData.Model("bert-vits2-MNN"));
            try
            {
                wav = AdditionalTtsValidation.ValidateAndSave(model.Synthesize(phrase), "bertvits2-chinese.wav", 44100);
                var repeat = model.Synthesize(phrase);
                AdditionalTtsValidation.ValidateAndSave(repeat, "bertvits2-repeat.wav", 44100);
                AdditionalTtsValidation.ValidateAndSave(model.Synthesize("今天是二十三号。"), "bertvits2-number.wav", 44100);
                Assert.Throws<NotSupportedException>(() => model.Synthesize("😀"));
                Assert.Throws<OperationCanceledException>(() => model.Synthesize(phrase, new CancellationToken(true)));
            }
            finally
            {
                model.Dispose();
                model.Dispose();
            }

            Assert.Throws<ObjectDisposedException>(() => model.Synthesize(phrase));
            using (var reload = MNNBertVits2.Load(GenerationTestData.Model("bert-vits2-MNN")))
                Assert.Greater(reload.Synthesize("你好。").Waveform.Length, 1000);
            AdditionalTtsValidation.Transcribe(wav, true);
        }

        [Test]
        public void EnglishAndMixedSpeechOnStudioWorkerIsRecognizable()
        {
            string english, mixed;
            using (var model = MNNBertVits2.Load(GenerationTestData.Model("bert-vits2-MNN")))
            {
                english = AdditionalTtsValidation.ValidateAndSave(Task.Run(() => model.Synthesize("The capital of France is Paris.")).GetAwaiter().GetResult(), "bertvits2-english.wav", 44100);
                mixed = AdditionalTtsValidation.ValidateAndSave(Task.Run(() => model.Synthesize("你好，Hello world，欢迎使用语音合成。")).GetAwaiter().GetResult(), "bertvits2-mixed.wav", 44100);
            }

            AdditionalTtsValidation.Transcribe(english, false);
            string asrRoot = Environment.GetEnvironmentVariable("MNN_TEST_CHINESE_ASR_ROOT");
            Assert.That(asrRoot, Is.Not.Null.And.Not.Empty, "Multilingual ASR model required for mixed speech validation.");
            using (var asr = MNNLlm.Load(asrRoot, cacheDirectory: Path.Combine(AdditionalTtsValidation.Artifacts, "mixed-asr-cache"), backendType: MNNBackendType.CPU))
            {
                string text = asr.GenerateMultimodal("逐字转写音频，保留中文和英文原文。", audioPath: mixed, maxNewTokens: 96).Text;
                File.WriteAllText(Path.ChangeExtension(mixed, "asr.txt"), text);
                TestContext.WriteLine(text);
                StringAssert.Contains("你好", text);
                StringAssert.Contains("hello", text.ToLowerInvariant());
                StringAssert.Contains("world", text.ToLowerInvariant());
                StringAssert.Contains("语音合成", text);
            }
        }
    }
}
