using System;
using System.IO;
using System.Threading;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [Category("RepresentativeModel")]
    public class SpeechLanguageIntegrationTests
    {
        [Test]
        public void SupertonicUsesItsJapaneseLanguageTagAutomatically()
        {
            using (var model = MNNSupertonic.Load(GenerationTestData.Model("Supertonic-3-INT8-MNN"), precision: "int8"))
            {
                const string text = "こんにちは、これは音声合成のテストです。";
                var automatic = model.Synthesize(text, seed: 42);
                var explicitLanguage = model.Synthesize(text, seed: 42, language: "ja");
                Assert.AreEqual(explicitLanguage.Waveform.Length, automatic.Waveform.Length);
                for (int i = 0; i < automatic.Waveform.Length; ++i)
                    Assert.That(automatic.Waveform[i], Is.EqualTo(explicitLanguage.Waveform[i]).Within(.00001));
                AdditionalTtsValidation.ValidateAndSave(automatic, "supertonic-auto-ja.wav", 44100);
            }
        }

        [Test]
        public void EnglishPiperKeepsJapaneseKanaAndKanjiOnOneInstalledModel()
        {
            using (var piper = MNNPiper.Load(GenerationTestData.Model("PiperLessac")))
            {
                var audio = piper.Synthesize("こんにちは、これは音声合成のテストです。");
                StringAssert.Contains("Supertonic", piper.LastSynthesisModelDirectory);
                AdditionalTtsValidation.ValidateAndSave(audio, "piper-auto-ja.wav", 22050);
            }
        }

        [Test]
        public void PiperAutomaticallySpeaksChineseAndMixedTextThenReturnsToEnglish()
        {
            string root = GenerationTestData.Model("PiperLessac");
            using (var piper = MNNPiper.Load(root))
            using (var recognizer = MNNSenseVoiceRecognizer.Load(GenerationTestData.Model("wo105090--SenseVoiceSmall-MNN")))
            {
                CollectionAssert.AreEqual(new[]{"en"}, piper.Languages);
                var chinese = piper.Synthesize("你好，这是语音合成测试。");
                string selected = piper.LastSynthesisModelDirectory;
                Assert.AreNotEqual(root, selected);
                TestContext.WriteLine("Automatic fallback: " + selected);
                string path = AdditionalTtsValidation.ValidateAndSave(chinese, "piper-auto-zh.wav", 22050);
                string heard = recognizer.Recognize(chinese.Waveform, chinese.SampleRate).Text;
                File.WriteAllText(Path.ChangeExtension(path, "asr.txt"), heard);
                StringAssert.Contains("你好", heard);
                StringAssert.Contains("语音合成", heard);
                var mixed = piper.Synthesize("你好，hello world，这是语音合成测试。");
                path = AdditionalTtsValidation.ValidateAndSave(mixed, "piper-auto-mixed.wav", 22050);
                heard = recognizer.Recognize(mixed.Waveform, mixed.SampleRate).Text;
                File.WriteAllText(Path.ChangeExtension(path, "asr.txt"), heard);
                StringAssert.Contains("你好", heard);
                StringAssert.Contains("hello", heard.ToLowerInvariant());
                StringAssert.Contains("world", heard.ToLowerInvariant());
                StringAssert.Contains("语音合成", heard);
                AdditionalTtsValidation.ValidateAndSave(piper.Synthesize("The capital of France is Paris."), "piper-auto-return-en.wav", 22050);
                Assert.AreEqual(root, piper.LastSynthesisModelDirectory);
                var pause = piper.Synthesize("。");
                Assert.Greater(pause.Waveform.Length, 0);
                Assert.Throws<OperationCanceledException>(() => piper.Synthesize("你好", cancellationToken: new CancellationToken(true)));
            }
        }
    }
}
