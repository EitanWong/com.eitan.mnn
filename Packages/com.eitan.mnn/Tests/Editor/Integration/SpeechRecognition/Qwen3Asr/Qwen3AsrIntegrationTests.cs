using System;
using System.IO;
using System.Linq;
using System.Threading;
using MNN.Unity.Editor;
using MNN.Unity.Interop;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [Category("Integration"), Category("Qwen3Asr")]
    public class Qwen3AsrIntegrationTests
    {
        [Serializable] private sealed class LanguageConfiguration
        {
            public string asr_language, sampler_type;
            public float repetition_penalty;
        }

        [TestCase("auto", ""), TestCase("en", "English")]
        public void ExplicitLanguageReachesTheOfficialRuntimeConfiguration(string language, string expected)
        {
            string model = Environment.GetEnvironmentVariable("SIRIAI_QWEN_MODEL");
            if (!Directory.Exists(model)) Assert.Ignore("Set SIRIAI_QWEN_MODEL to installed weights; no downloads.");
            string cache = Path.GetFullPath(Path.Combine("TestArtifacts~", "QwenLanguageConfiguration", language));
            string before = File.ReadAllText(Path.Combine(model, "llm_config.json"));
            using (var llm = MNNLlm.LoadForSpeechRecognition(model, 1, cache, MNNBackendType.CPU,
                MNNPrecisionMode.Normal, MNNQwen3AsrPrompt.Configuration(MNNQwen3AsrPrompt.NormalizeLanguage(language))))
            {
                var actual = UnityEngine.JsonUtility.FromJson<LanguageConfiguration>(llm.DumpConfiguration());
                Assert.AreEqual(expected, actual.asr_language);
                Assert.AreEqual("greedy", actual.sampler_type);
                Assert.AreEqual(1, actual.repetition_penalty);
            }
            Assert.AreEqual(before, File.ReadAllText(Path.Combine(model, "llm_config.json")), "Runtime overrides must not edit installed assets.");
        }

        [TestCase("auto", ""), TestCase("en", "language English<asr_text>")]
        public void LanguageTemplateRetainsOfficialRoleNewlines(string language, string suffix)
        {
            string model = Environment.GetEnvironmentVariable("SIRIAI_QWEN_MODEL");
            if (!Directory.Exists(model)) Assert.Ignore("Set SIRIAI_QWEN_MODEL to installed weights; no downloads.");
            var options = MNNModelOptions.Prepare(model, 1, false,
                Path.GetFullPath(Path.Combine("TestArtifacts~", "QwenLanguageTemplate", language)),
                MNNPrecisionMode.Normal, backendType: MNNBackendType.CPU, speechRecognition: true);
            string json = MNNQwen3AsrPrompt.Configuration(MNNQwen3AsrPrompt.NormalizeLanguage(language));
            options.Json = options.Json.Substring(0, options.Json.Length - 1) + "," + json.Substring(1);
            using (MNNInterop.BeginNativeCall())
            using (var handle = options.Load(MNNInterop.MNN_Llm_create))
            {
                var messages = new[] { new MNNChatMessage(MNNChatRole.User, "<audio>reference.wav</audio>") };
                string actual = MNNInterop.RenderConversation(handle, messages);
                Assert.AreEqual("<|im_start|>system\n<|im_end|>\n<|im_start|>user\n<audio>reference.wav</audio><|im_end|>\n<|im_start|>assistant\n" + suffix, actual);
            }
        }

        private string Root => Environment.GetEnvironmentVariable("MNN_TEST_QWEN3_ASR_ROOT");
        private string Model => Path.Combine(Root, MNNQwen3AsrModel.Repository.Replace("/", "--"));
        private string Wave(string name) => Path.Combine(Root, "..", "..", "Zipformer", "Audio", name);
        private void RequireModel()
        {
            if (Root == null) Assert.Ignore("Set MNN_TEST_QWEN3_ASR_ROOT with existing pinned weights; no downloads.");
            MNNQwen3AsrModel.Prepare(Model);
        }

        [TestCase(null), TestCase("auto"), TestCase("en")]
        public void ProfileFacadeHonorsLanguageAndReturnsOnlyTheEnglishTranscript(string language)
        {
            RequireModel();
            var pcm = MNNWaveAudio.Read(Wave("en-0.wav"), out int rate);
            using (var recognizer = MNNSpeechRecognizer.Load(Model, 4, MNNBackendType.CPU, language))
                AssertEnglish(recognizer.Transcribe(pcm, rate));
        }

        [Test]
        public void EnglishAndBilingualTranscriptsResetAndUseOwnedActualTokenIds()
        {
            RequireModel();
            using (var recognizer = MNNQwen3AsrRecognizer.Load(Model, cacheDirectory: Path.Combine(Root, "TestsCache"), backendType: MNNBackendType.CPU))
            {
                bool callbackChecked = false;
                var first = recognizer.RecognizeWaveFile(Wave("en-0.wav"), onProgress: update =>
                {
                    if (callbackChecked) return;
                    callbackChecked = true;
                    Assert.Throws<InvalidOperationException>(() => recognizer.Dispose());
                    Assert.Throws<InvalidOperationException>(() => recognizer.Recognize(new float[16000], 16000));
                });
                Assert.True(callbackChecked);
                AssertEnglish(first.Text);
                Assert.IsNotEmpty(first.TokenIds);
                var saved = first.TokenIds.ToArray();
                var bilingual = recognizer.RecognizeWaveFile(Wave("zh-en-0.wav"));
                Assert.AreEqual("昨天是 Monday。Today is 礼拜二。The day after tomorrow 是星期三。", bilingual.Text);
                Assert.IsEmpty(bilingual.Timestamps);
                var second = recognizer.RecognizeWaveFile(Wave("en-0.wav"));
                Assert.AreEqual(first.Text, second.Text);
                CollectionAssert.AreEqual(saved, first.TokenIds);
                CollectionAssert.AreEqual(first.TokenIds, second.TokenIds);
                Assert.AreEqual(MNNBackendType.CPU, recognizer.Backend);
                Assert.AreEqual(MNNBackendType.CPU, recognizer.MediaBackend);
                Assert.IsEmpty(Directory.GetFiles(Path.Combine(Root, "TestsCache"), "input-*.wav"));
            }
        }

        [Test]
        public void SilenceInvalidAudioAndCancellationRecoverWithoutLeakingTemporaryFiles()
        {
            RequireModel();
            using (var recognizer = MNNQwen3AsrRecognizer.Load(Model, cacheDirectory: Path.Combine(Root, "CancelCache"), backendType: MNNBackendType.CPU))
            {
                Assert.AreEqual("", recognizer.Recognize(new float[16000], 16000).Text);
                Assert.Throws<ArgumentException>(() => recognizer.Recognize(new float[1599], 16000));
                Assert.Throws<ArgumentException>(() => recognizer.Recognize(new float[480001], 16000));
                var invalid = new float[1600]; invalid[10] = float.NaN;
                Assert.Throws<ArgumentException>(() => recognizer.Recognize(invalid, 16000));
                Assert.Throws<OperationCanceledException>(() => recognizer.RecognizeWaveFile(Wave("en-0.wav"), new CancellationToken(true)));
                using (var cancellation = new CancellationTokenSource())
                {
                    int updates = 0;
                    Assert.Throws<OperationCanceledException>(() => recognizer.RecognizeWaveFile(Wave("en-0.wav"), cancellation.Token,
                        update => { ++updates; cancellation.Cancel(); }));
                    Assert.AreEqual(1, updates, "Cancellation is observed between tokens.");
                }
                Assert.IsEmpty(Directory.GetFiles(Path.Combine(Root, "CancelCache"), "input-*.wav"));
                AssertEnglish(recognizer.RecognizeWaveFile(Wave("en-0.wav")).Text);
            }
        }

        [Test]
        public void StudioRoutesPreparedAudioProfileAndReportsItsBackend()
        {
            RequireModel();
            var model = MNNStudioModel.Read(Model);
            Assert.True(model.HasSpeechRecognitionPipeline);
            Assert.True(MNNStudioTasks.Get((int)MNNStudioTask.SpeechRecognition).CanRun(model));
            Assert.False(MNNStudioTasks.Get((int)MNNStudioTask.Chat).CanRun(model));
            using (var backend = MNNStudioNativeBackend.Load(model, 4, false))
            {
                var result = backend.Run(new MNNStudioRequest {Audio = Wave("zh-en-0.wav")});
                Assert.AreEqual("昨天是 Monday。Today is 礼拜二。The day after tomorrow 是星期三。", result.Text);
                Assert.Greater(result.Tokens, 0);
                var expected = MNNAcceleration.IsBackendAvailable(MNNBackendType.Metal) ? MNNBackendType.Metal : MNNBackendType.CPU;
                Assert.That(result.BackendDescription, Does.Contain("Qwen3-ASR").And.Contain(expected + " / audio " + expected));
            }
        }

        private void AssertEnglish(string text)
        {
            string reference = File.ReadAllLines(Wave("en-trans.txt"))[0].Substring(6).Trim();
            string lexical = System.Text.RegularExpressions.Regex.Replace(text.ToUpperInvariant(), "[^A-Z]+", " ").Trim();
            Assert.AreEqual(reference, lexical, "Independent upstream word sequence.");
            TestContext.WriteLine(text);
        }
    }
}
