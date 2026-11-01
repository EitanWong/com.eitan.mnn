using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public class SpeechLanguageTests
    {
        [TestCase("Adreno (TM) 740|driver", "int8", MNNBackendType.Auto, MNNBackendType.CPU)]
        [TestCase("Mali-G57 MC2|driver", "int8", MNNBackendType.Auto, MNNBackendType.CPU)]
        [TestCase("Adreno (TM) 750|driver", "int8", MNNBackendType.Auto, MNNBackendType.Auto)]
        [TestCase("Adreno (TM) 740|driver", "fp16", MNNBackendType.Auto, MNNBackendType.Auto)]
        [TestCase("Adreno (TM) 740|driver", "int8", MNNBackendType.OpenCL, MNNBackendType.OpenCL)]
        public void AndroidInt8SpeechAutoAvoidsTheObservedMappingCrash(string gpu, string precision,
            MNNBackendType requested, MNNBackendType expected)
        {
            Assert.AreEqual(expected, MNNSupertonic.ForAndroidDevice(requested, precision, gpu));
        }


        [TestCase("你好，Hello world！", "zh,en", true)]
        [TestCase("你好", "en", false)]
        [TestCase("你好", "zh", true)]
        [TestCase("こんにちは世界", "ja", true)]
        [TestCase("こんにちは世界", "zh,en", false)]
        [TestCase("안녕하세요", "ko", true)]
        [TestCase("مرحبا", "ar", true)]
        [TestCase("Привет", "ru", true)]
        [TestCase("नमस्ते", "hi", true)]
        [TestCase("γειά", "el", true)]
        [TestCase("שלום", "en", false)]
        [TestCase("Bonjour", "fr", true)]
        [TestCase("𠀀", "zh", true)]
        public void ScriptCoverageKeepsMixedLanguages(string text, string languages, bool supported)
        {
            Assert.AreEqual(supported, MNNSpeechLanguages.SupportsText(text, languages.Split(',')));
        }

        [TestCase("こんにちは", "ja")]
        [TestCase("안녕하세요", "ko")]
        [TestCase("Привет", "ru")]
        [TestCase("مرحبا", "ar")]
        [TestCase("नमस्ते", "hi")]
        [TestCase("Hola", "en")]
        public void AutomaticLanguageUsesDistinctScripts(string text, string expected)
        {
            Assert.AreEqual(expected, MNNSpeechLanguages.Select(text, MNNSupertonic.LanguagesForVersion("v1.7.3")));
        }

        [Test]
        public void LatinLanguagePreferenceIsRetainedRatherThanGuessed()
        {
            Assert.AreEqual("es", MNNSpeechLanguages.Select("Hola", new[]{"en", "es"}, "es-MX"));
            Assert.IsNull(MNNSpeechLanguages.Select("你好", new[]{"en", "ja"}));
        }

        [Test]
        public void VoiceSpecificPhonemeMapSupportsNonEnglishAndMultiIdSymbols()
        {
            var map = MNNPiperPhonemes.ReadMap(new Dictionary<string, object>{
                {"_", new List<object>{9d}}, {"^", new List<object>{8d}}, {"$", new List<object>{7d}},
                {" ", new List<object>{6d}}, {"ʁ", new List<object>{5d, 4d}}, {"a", new List<object>{3d}}});
            CollectionAssert.AreEqual(new[]{8,9,5,4,9,3,9,7}, MNNPiperPhonemes.Encode("ʁ‍a", map));
            Assert.Throws<NotSupportedException>(() => MNNPiperPhonemes.Encode("ɐ", map));
            Assert.Throws<InvalidDataException>(() => MNNPiperPhonemes.ReadMap(new Dictionary<string, object>{
                {"_", new List<object>{0d}}, {"^", new List<object>{1d}}, {"$", new List<object>{2d}},
                {" ", new List<object>{3d}}, {"a", new List<object>{256d}}}, 256));
        }

        [TestCase("你好，hello world！再见。")]
        [TestCase("こんにちは world。안녕하세요!")]
        [TestCase("𠀀Hello")]
        public void ScriptRunsPreserveEveryCharacter(string text)
        {
            Assert.AreEqual(text, string.Concat(MNNSpeechLanguages.SplitScriptRuns(text)));
        }

        [Test]
        public void StandardPiperLanguageComesFromMetadata()
        {
            string root = Path.GetFullPath("TestArtifacts~/TtsLanguages/Metadata/" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "assets"));
            try
            {
                File.WriteAllText(Path.Combine(root, "config.json"), "{\"model_path\":\"voice.mnn\"}");
                File.WriteAllText(Path.Combine(root, "assets/voice.onnx.json"), "{\"espeak\":{\"voice\":\"fr-fr\"}}");
                CollectionAssert.AreEqual(new[]{"fr"}, MNNPiper.ReadLanguages(root));
            }
            finally { Directory.Delete(root, true); }
        }

        [Test]
        public void NoInstalledLanguageIsRecoverableAndNeverDropsChinese()
        {
            using (var fallback = new MNNLocalSpeechFallback("TestArtifacts~/TtsLanguages/no-model", 22050, modelRoots: Array.Empty<string>()))
            {
                bool called = false;
                Assert.Throws<MNNSpeechLanguageException>(() => fallback.Synthesize("你好 Hello", new[]{"en"}, () => { called = true; return null; }));
                Assert.False(called, "Do not run the English model on the remaining English words.");
                var valid = new MNNGeneratedAudio(new float[2205], 22050, "Hello");
                Assert.AreSame(valid, fallback.Synthesize("Hello", new[]{"en"}, () => valid));
                var silence = fallback.Synthesize("。！？", new[]{"en"}, () => throw new Exception("No speech"));
                Assert.AreEqual(22050, silence.SampleRate);
                Assert.Throws<OperationCanceledException>(() => fallback.Synthesize("你好", new[]{"en"}, () => null, new CancellationToken(true)));
            }
        }
    }
}
