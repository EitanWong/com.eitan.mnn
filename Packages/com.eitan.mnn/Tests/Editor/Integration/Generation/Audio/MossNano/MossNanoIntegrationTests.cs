using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public class MossNanoIntegrationTests
    {
        private static string Root => Path.Combine(Environment.GetEnvironmentVariable("MNN_TEST_QWEN3_TTS_ROOT"), "MossNano");
        [TestCase("tokenizer_mnn.txt")]
        [TestCase("tokenizer.model")]
        public void TokenizerMatchesBothPublisherFixtures(string filename)
        {
            var tokenizer = new MNNMossTokenizer(Path.Combine(Root, filename));
            var json = MNNModelData.Read(Path.Combine(Root, "browser_poc_manifest.json"));
            foreach (var sample in MNNModelData.Array(MNNModelData.Get(json, "text_samples")))
            {
                string text = (string)MNNModelData.Get(sample, "text");
                var expected = MNNModelData.Array(MNNModelData.Get(sample, "text_token_ids")).Select(MNNModelData.Integer).ToArray();
                CollectionAssert.AreEqual(expected, tokenizer.Encode(text), text);
            }
        }

        [Test]
        public void EveryBuiltinVoiceProducesReferenceSpeech()
            => AuditVoices(Root, "moss-nano-");

        [Test]
        public void EveryCachedLayoutVoiceProducesReferenceSpeech()
            => AuditVoices(Path.Combine(Environment.GetEnvironmentVariable("MNN_TEST_QWEN3_TTS_ROOT"), "MossNanoCached"), "moss-nano-cached-");

        // Independent SentencePiece 0.2.1 output using the publisher's tokenizer.model.
        [TestCase("tokenizer_mnn.txt")]
        [TestCase("tokenizer.model")]
        public void JapaneseTokenizerMatchesSentencePiece(string filename)
        {
            var tokenizer = new MNNMossTokenizer(Path.Combine(Root, filename));
            CollectionAssert.AreEqual(new[]{10356, 13051, 12770, 12823, 13832, 12962, 10508, 13051, 13299, 12962, 10892, 10638, 10621, 10485, 12321, 15115, 13938, 14110, 12574, 12874, 10382},
                tokenizer.Encode("こんにちは、これは音声合成のテストです。"));
            CollectionAssert.AreEqual(new[]{10356, 14075, 12962, 13710, 12591, 14778, 16061, 12276, 12889, 12874, 10382},
                tokenizer.Encode("おはようございます。"));
        }

        private static void AuditVoices(string root, string artifactPrefix)
        {
            var failures = new List<string>();
            using (var model = MNNMossNano.Load(root))
            foreach (string voice in model.Voices)
            {
                int index = Array.IndexOf(model.Voices, voice);
                bool english = index >= 6 && index < 11, japanese = index >= 11;
                string text = english ? "Paris is the capital of France." : japanese ? "こんにちは、これは音声合成のテストです。" : "你好，这是语音合成测试。";
                try
                {
                    var audio = model.Synthesize(text, voice, maxFrames: 128);
                    string path = AdditionalTtsValidation.ValidateAndSave(audio, artifactPrefix + voice + ".wav", 48000);
                    if (japanese)
                    {
                        using (var asr = MNNSenseVoiceRecognizer.Load(Environment.GetEnvironmentVariable("MNN_TEST_INDEPENDENT_CHINESE_ASR_ROOT")))
                        {
                            string result = asr.RecognizeWaveFile(path).Text;
                            TestContext.WriteLine(voice + ": " + result);
                            File.WriteAllText(Path.ChangeExtension(path, "asr.txt"), result);
                            StringAssert.Contains("こんにちは", result);
                            StringAssert.Contains("音声合成", result);
                        }
                    }
                    else AdditionalTtsValidation.Transcribe(path, !english);
                }
                catch (Exception error) { failures.Add(voice + ": " + error.Message); TestContext.WriteLine(failures.Last()); }
            }
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        [Test]
        public void CachedLayoutSynthesizesChineseAndEnglish()
        {
            string root = Path.Combine(Environment.GetEnvironmentVariable("MNN_TEST_QWEN3_TTS_ROOT"), "MossNanoCached");
            using (var model = MNNMossNano.Load(root))
            {
                var audio = model.Synthesize("你好，这是语音合成测试。", maxFrames: 128);
                string path = AdditionalTtsValidation.ValidateAndSave(audio, "moss-nano-cached-zh.wav", 48000);
                AdditionalTtsValidation.Transcribe(path, true);
                audio = model.Synthesize("Paris is the capital of France.", "Ava", maxFrames: 128);
                path = AdditionalTtsValidation.ValidateAndSave(audio, "moss-nano-cached-en.wav", 48000);
                AdditionalTtsValidation.Transcribe(path, false);
            }
        }

        [Test, Explicit("Runs a second recognizer on saved failures; execution success is not a voice quality pass.")]
        public void DiagnoseSavedVoiceContentDisagreements()
        {
            using (var asr = MNNWhisperRecognizer.Load(Environment.GetEnvironmentVariable("MNN_TEST_TAMIL_ASR_ROOT")))
            foreach (string voice in new[]{"Adam", "Soyo", "Saki", "Mortis", "Umiri", "Mei", "Anon", "Arisa"})
            {
                asr.Language = "en";
                if (voice != "Adam")
                {
                    // Diagnostic-only Japanese token prefix from Whisper's public tokenizer.
                    // It does not enable an unvalidated language in the package's ASR API.
                    typeof(MNNWhisperRecognizer).GetField("_initialTokens", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                        .SetValue(asr, new[]{50258, 50266, 50359, 50363});
                }
                string path = Path.Combine(AdditionalTtsValidation.Artifacts, "moss-nano-" + voice + ".wav");
                float[] pcm = MNNWaveAudio.Read(path, out int rate);
                string text = asr.Recognize(pcm, rate).Text;
                File.WriteAllText(Path.ChangeExtension(path, "whisper-asr.txt"), text);
                TestContext.WriteLine(voice + ": " + text);
            }
        }

        [Test]
        public void SynthesizesChineseWithCompleteAutoregressivePipeline()
        {
            using (var model = MNNMossNano.Load(Root))
            {
                Assert.AreEqual(18, model.Voices.Length);
                var audio = model.Synthesize("你好，这是语音合成测试。", maxFrames: 128);
                string path = AdditionalTtsValidation.ValidateAndSave(audio, "moss-nano-zh.wav", 48000);
                AdditionalTtsValidation.Transcribe(path, true);
            }
        }
    }
}
