using System;
using System.IO;
using System.Linq;
using System.Threading;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [Category("RepresentativeModel")]
    public class KokoroIntegrationTests
    {
        [Test]
        public void EveryExternalVoiceProducesReferenceSpeech()
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_KOKORO11_ROOT");
            using (var model = MNNKokoro.Load(root))
            using (var recognizer = MNNSenseVoiceRecognizer.Load(Environment.GetEnvironmentVariable("MNN_TEST_INDEPENDENT_CHINESE_ASR_ROOT")))
            {
                var failures = new System.Collections.Generic.List<string>();
                Assert.AreEqual(103, model.Voices.Length);
                foreach (string voice in model.Voices)
                {
                    try
                    {
                    bool chinese = voice.StartsWith("z", StringComparison.Ordinal);
                    string phrase = chinese ? "你好，这是语音合成测试。" : "The capital of France is Paris.";
                    var audio = model.Synthesize(phrase, voice);
                    string path = AdditionalTtsValidation.ValidateAndSave(audio, "kokoro11-all-" + voice + ".wav", 24000);
                    string transcript = recognizer.Recognize(audio.Waveform, audio.SampleRate).Text;
                    TestContext.WriteLine(voice + " => " + transcript);
                    if (chinese)
                    {
                        StringAssert.Contains("你好", transcript, voice);
                        StringAssert.Contains("语音合成", transcript, voice);
                    }
                    else
                        AdditionalTtsValidation.Transcribe(path, false);
                    }
                    catch (Exception exception)
                    {
                        failures.Add(voice + ": " + exception.Message);
                        TestContext.WriteLine("FAILED " + failures[failures.Count - 1]);
                    }
                }
                Assert.IsEmpty(failures, string.Join("\n", failures));
            }
        }

        [Test]
        public void ExternalGraphRejectsMissingOrTruncatedWeightsBeforeNativeLoading()
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_KOKORO11_ROOT");
            byte[] graph = File.ReadAllBytes(Path.Combine(root, "model.mnn"));
            string path = Path.Combine(AdditionalTtsValidation.Artifacts, "invalid-external-" + Guid.NewGuid().ToString("N"));
            Assert.Throws<FileNotFoundException>(() => MNNGraphExternalWeights.Bind(graph, path));
            try
            {
                File.WriteAllBytes(path, new byte[1]);
                Assert.Throws<InvalidDataException>(() => MNNGraphExternalWeights.Bind(graph, path));
            }
            finally { File.Delete(path); }
        }

        [Test, Explicit("Requires all expanded Kokoro voices and independent ASR models; writes per-voice evidence.")]
        public void ExpandedExportChecksEveryNamedVoiceAndRechecksTheSevenContentFailures()
        {
            string root = Path.Combine(Environment.GetEnvironmentVariable("MNN_TEST_LOCAL_MODEL_ROOT"), "starsharp06sharp--kokoro-v1_1");
            var report = new System.Collections.Generic.List<string>();
            var failures = new System.Collections.Generic.List<string>();
            string[] recheck = {"zf_046", "zf_079", "zm_030", "zm_035", "zm_080", "zm_096", "zm_100"};
            using (var model = MNNKokoro.Load(root))
            {
                Assert.AreEqual(103, model.Voices.Length);
                foreach (string voice in model.Voices)
                {
                    bool chinese = voice.StartsWith("z", StringComparison.Ordinal);
                    string path = AdditionalTtsValidation.ValidateAndSave(model.Synthesize(chinese ? "你好，这是语音合成测试。" : "Paris is the capital of France.", voice), "kokoro-expanded-all-" + voice + ".wav", 24000);
                    string content = "not checked";
                    if (recheck.Contains(voice) || !chinese)
                    {
                        try { AdditionalTtsValidation.Transcribe(path, chinese); content = "passed"; }
                        catch (AssertionException error) { content = "failed: " + error.Message.Replace("\n", " ").Replace("\r", " "); failures.Add(voice); }
                    }
                    report.Add(voice + "\twaveform passed\t" + content);
                    File.WriteAllLines(Path.Combine(AdditionalTtsValidation.Artifacts, "kokoro-expanded-all-voices.tsv"), report);
                }
            }
            Assert.IsEmpty(failures, "Content failures: " + string.Join(", ", failures));
        }

        [Test]
        public void ExpandedVoiceMetadataExceptionRequiresTheExactPublishedGraph()
        {
            string source = Path.Combine(Environment.GetEnvironmentVariable("MNN_TEST_LOCAL_MODEL_ROOT"), "starsharp06sharp--kokoro-v1_1");
            string temporary = Path.Combine(AdditionalTtsValidation.Artifacts, "graph-validation", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporary);
            try
            {
                byte[] graph = File.ReadAllBytes(Path.Combine(source, "model.mnn"));
                graph[graph.Length - 1] ^= 1;
                File.WriteAllBytes(Path.Combine(temporary, "model.mnn"), graph);
                using (var stream = File.Create(Path.Combine(temporary, "voices.bin"))) stream.SetLength(156L * 510 * 256 * 4);
                Assert.Throws<InvalidDataException>(() => MNNKokoroModel.SherpaVoices(temporary));
            }
            finally { Directory.Delete(temporary, true); }
        }

        [Test]
        public void ExpandedExportUsesOnlyItsNamedPrefixAndSynthesizesChineseAndEnglish()
        {
            string root = Path.Combine(Environment.GetEnvironmentVariable("MNN_TEST_LOCAL_MODEL_ROOT"), "starsharp06sharp--kokoro-v1_1");
            CollectionAssert.AreEqual(MNNKokoro.InstalledVoices(Environment.GetEnvironmentVariable("MNN_TEST_KOKORO11_ROOT")), MNNKokoro.InstalledVoices(root));
            using (var model = MNNKokoro.Load(root))
            {
                Assert.AreEqual(103, model.Voices.Length);
                foreach (string voice in new[]{"zf_001", "zm_009", "af_maple"})
                {
                    bool chinese = voice.StartsWith("z", StringComparison.Ordinal);
                    string path = AdditionalTtsValidation.ValidateAndSave(model.Synthesize(chinese ? "你好，这是语音合成测试。" : "Paris is the capital of France.", voice), "kokoro-expanded-" + voice + ".wav", 24000);
                    AdditionalTtsValidation.Transcribe(path, chinese);
                }
                Assert.Throws<ArgumentException>(() => model.Synthesize("你好。", "unnamed-155"));
            }
        }

        [Test]
        public void ExternalWeightsSynthesizeMandarinAndEnglishWithoutCopyingAssets()
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_KOKORO11_ROOT");
            Assert.That(root, Is.Not.Null.And.Not.Empty);
            string[] before = Directory.GetFiles(root, "*", SearchOption.AllDirectories).OrderBy(p => p).ToArray();
            using (var model = MNNKokoro.Load(root))
            {
                TestContext.WriteLine("external voices=" + string.Join(",", model.Voices));
                Assert.Throws<NotSupportedException>(() => model.Synthesize("呣"));
                foreach (string voice in new[]{"zf_001", "zm_009", "af_maple"})
                {
                    bool chinese = voice.StartsWith("z", StringComparison.Ordinal);
                    string phrase = chinese ? "你好，这是语音合成测试。" : "The capital of France is Paris.";
                    string path = AdditionalTtsValidation.ValidateAndSave(model.Synthesize(phrase, voice), "kokoro11-" + voice + ".wav", 24000);
                    AdditionalTtsValidation.Transcribe(path, chinese);
                }
                Assert.Greater(model.SynthesizeAsync("你好。", "zf_001", 1.2f).GetAwaiter().GetResult().Waveform.Length, 2400);
            }
            CollectionAssert.AreEqual(before, Directory.GetFiles(root, "*", SearchOption.AllDirectories).OrderBy(p => p).ToArray());
        }

        [TestCase(1)]
        [TestCase(4)]
        public void FixedInputsRemainStableAcrossThreadsShapesAndOtherModels(int threads)
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_KOKORO_ROOT");
            const string phrase = "你好，这是语音合成测试。";
            string[] voices = { "zf_xiaoxiao", "zm_yunxi" };
            using (var model = MNNKokoro.Load(root, threads))
            {
                var expected = voices.Select(voice => model.Synthesize(phrase, voice).Waveform).ToArray();
                using (var recognizer = MNNSenseVoiceRecognizer.Load(Environment.GetEnvironmentVariable("MNN_TEST_INDEPENDENT_CHINESE_ASR_ROOT")))
                    for (int i = 0; i < voices.Length; ++i)
                    {
                        string text = recognizer.Recognize(expected[i], 24000).Text;
                        TestContext.WriteLine("baseline " + voices[i] + ": " + text);
                        StringAssert.Contains("你好", text);
                        StringAssert.Contains("语音合成", text);
                    }
                for (int cycle = 0; cycle < 4; ++cycle)
                {
                    using (var indic = MNNIndicVits.Load(Environment.GetEnvironmentVariable("MNN_TEST_INDIC_VITS_ROOT")))
                        Assert.Greater(indic.Synthesize(MNNIndicVits.SpeakerExamples[18]).Waveform.Length, 2400);
                    Assert.Greater(model.SynthesizeAsync("你好。", voices[cycle % 2], 1.2f).GetAwaiter().GetResult().Waveform.Length, 2400);
                    for (int i = 0; i < voices.Length; ++i)
                    {
                        float[] actual = model.SynthesizeAsync(phrase, voices[i]).GetAwaiter().GetResult().Waveform;
                        string path = AdditionalTtsValidation.ValidateAndSave(new MNNGeneratedAudio(actual, 24000, phrase),
                            "kokoro-stability-" + threads + "-" + cycle + "-" + voices[i] + ".wav", 24000);
                        TestContext.WriteLine(path + " exact=" + actual.SequenceEqual(expected[i]) + " length=" + actual.Length + " expected=" + expected[i].Length);
                        CollectionAssert.AreEqual(expected[i], actual, "Fixed graph has no random operations: cycle=" + cycle + ", voice=" + voices[i]);
                    }
                }
            }
        }

        [Test]
        public void EmptyPublisherStylesResolveOnlyTheVerifiedSingleVoiceAndSynthesizeBothLanguages()
        {
            string root = Path.Combine(Environment.GetEnvironmentVariable("MNN_TEST_LOCAL_MODEL_ROOT"), "Jeryuk--kokoro-v1_0-MNN");
            CollectionAssert.AreEqual(new[]{"zf_xiaoxiao"}, MNNKokoroModel.ConstantVoiceNames(root));
            byte[] originalConfig = File.ReadAllBytes(Path.Combine(root, "tts_config.json"));
            float[] table = MNNKokoroModel.ConstantVoice(root, 0, 1);
            byte[] reference = File.ReadAllBytes(Path.Combine(Environment.GetEnvironmentVariable("MNN_TEST_KOKORO_ROOT"), "voices", "zf_xiaoxiao.bin"));
            byte[] actual = new byte[table.Length * 4];
            Buffer.BlockCopy(table, 0, actual, 0, actual.Length);
            CollectionAssert.AreEqual(reference, actual);
            string chinese, english;
            using (var model = MNNKokoro.Load(root))
            {
                chinese = AdditionalTtsValidation.ValidateAndSave(model.Synthesize("你好，这是语音合成测试。"), "kokoro-single-zh.wav", 24000);
                english = AdditionalTtsValidation.ValidateAndSave(model.Synthesize("Paris is the capital of France."), "kokoro-single-en.wav", 24000);
                Assert.Throws<ArgumentException>(() => model.Synthesize("hello", "af_heart"));
            }
            CollectionAssert.AreEqual(originalConfig, File.ReadAllBytes(Path.Combine(root, "tts_config.json")));
            AdditionalTtsValidation.Transcribe(chinese, true);
            AdditionalTtsValidation.Transcribe(english, false);
        }

        [Test]
        public void ConstantVoicesMatchTheVerifiedRawVoiceTables()
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_KOKORO_CONSTANT_ROOT");
            string raw = Environment.GetEnvironmentVariable("MNN_TEST_KOKORO_ROOT");
            string[] voices = MNNKokoroModel.ConstantVoiceNames(root);
            Assert.AreEqual(55, voices.Length);
            foreach (string name in new[]{"zf_xiaoxiao", "zm_yunxi"})
            {
                float[] table = MNNKokoroModel.ConstantVoice(root, Array.IndexOf(voices, name), voices.Length);
                byte[] bytes = File.ReadAllBytes(Path.Combine(raw, "voices", name + ".bin"));
                float[] expected = new float[bytes.Length / 4];
                Buffer.BlockCopy(bytes, 0, expected, 0, bytes.Length);
                CollectionAssert.AreEqual(expected, table);
            }
            using (var model = MNNKokoro.Load(root))
            {
                string path = AdditionalTtsValidation.ValidateAndSave(model.Synthesize("你好，这是语音合成测试。", "zf_xiaoxiao"), "kokoro-constant-zh.wav", 24000);
                AdditionalTtsValidation.Transcribe(path, true);
                path = AdditionalTtsValidation.ValidateAndSave(model.Synthesize("The capital of France is Paris.", "af_heart"), "kokoro-constant-en.wav", 24000);
                AdditionalTtsValidation.Transcribe(path, false);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MandarinVoicesSynthesizeRecoverReloadAndTranscribe(bool sherpa)
        {
            string root = Environment.GetEnvironmentVariable(sherpa ? "MNN_TEST_SHERPA_KOKORO_ROOT" : "MNN_TEST_KOKORO_ROOT");
            Assert.That(root, Is.Not.Null.And.Not.Empty);
            const string phrase = "你好，这是语音合成测试。";
            string[] paths;
            var model = MNNKokoro.Load(root);
            try
            {
                var studio = MNNStudioModel.Read(root);
                Assert.AreEqual(MNNStudioTask.SpeechSynthesis, studio.Task);
                Assert.IsTrue(studio.HasGenerationPipeline);
                CollectionAssert.AreEquivalent(model.Voices, studio.TtsVoices);
                Assert.Throws<OperationCanceledException>(() => model.Synthesize(phrase, cancellationToken: new CancellationToken(true)));
                Assert.Throws<NotSupportedException>(() => model.Synthesize("你好𠮷"));
                paths = model.Voices.Where(voice => !sherpa || voice.StartsWith("z", StringComparison.Ordinal)).Select(voice =>
                {
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    var audio = model.SynthesizeAsync(phrase, voice).GetAwaiter().GetResult();
                    TestContext.WriteLine(voice + " synthesis_seconds=" + watch.Elapsed.TotalSeconds + " audio_seconds=" + audio.Waveform.Length / 24000d);
                    return AdditionalTtsValidation.ValidateAndSave(audio, (sherpa ? "sherpa-kokoro-" : "kokoro-") + voice + ".wav", 24000);
                }).ToArray();
                for (int repeat = 0; repeat < 8; ++repeat)
                    Assert.Greater(model.Synthesize(phrase, "zf_xiaoxiao").Waveform.Length, 2400);
                Assert.Greater(model.Synthesize("你好。", "zf_xiaoxiao", 1.2f).Waveform.Length, 2400);
                Assert.Greater(model.Synthesize(phrase, "zf_xiaoxiao").Waveform.Length, 2400);
            }
            finally
            {
                model.Dispose();
                model.Dispose();
            }
            Assert.Throws<ObjectDisposedException>(() => model.Synthesize(phrase));
            using (var reload = MNNKokoro.Load(root))
                Assert.Greater(reload.Synthesize("你好。").Waveform.Length, 2400);
            foreach (string path in paths)
                AdditionalTtsValidation.Transcribe(path, true);
        }
    }
}
