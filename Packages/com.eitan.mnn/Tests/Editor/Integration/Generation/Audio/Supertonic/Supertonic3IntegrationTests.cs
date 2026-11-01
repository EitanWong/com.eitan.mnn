using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [Category("RepresentativeModel")]
    public class Supertonic3IntegrationTests
    {
        [TestCase(false, "int8")]
        [TestCase(true, "int8")]
        [TestCase(true, "fp16")]
        public void AllTenVoicesSynthesizeWithLanguageTagsAndTranscribe(bool version2, string precision)
        {
            string root = Environment.GetEnvironmentVariable(version2 ? "MNN_TEST_SUPERTONIC2_ROOT" : "MNN_TEST_SUPERTONIC3_ROOT");
            Assert.That(root, Is.Not.Null.And.Not.Empty);
            const string phrase = "The capital of France is Paris.";
            var paths = new List<string>();
            using (var model = MNNSupertonic.Load(root, precision: precision))
            {
                Assert.AreEqual(version2 ? "v1.6.0" : "v1.7.3", model.Version);
                Assert.AreEqual(version2 ? 5 : 31, model.Languages.Length);
                Assert.AreEqual(10, model.Voices.Length);
                var studio = MNNStudioModel.Read(root);
                Assert.IsTrue(studio.HasGenerationPipeline);
                Assert.AreEqual(MNNStudioTask.SpeechSynthesis, studio.Task);
                CollectionAssert.AreEquivalent(model.Voices, studio.TtsVoices);
                Assert.Throws<NotSupportedException>(() => model.Synthesize("你好。", language: "zh"));
                Assert.Throws<ArgumentException>(() => model.Synthesize("<en>Hello</en>"));
                Assert.Throws<OperationCanceledException>(() => model.Synthesize(phrase, cancellationToken: new CancellationToken(true)));
                foreach (string voice in model.Voices)
                {
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    var audio = model.SynthesizeAsync(phrase, voice, steps: 5).GetAwaiter().GetResult();
                    TestContext.WriteLine(voice + " synthesis_seconds=" + watch.Elapsed.TotalSeconds + " audio_seconds=" + audio.Waveform.Length / 44100d);
                    paths.Add(AdditionalTtsValidation.ValidateAndSave(audio, (version2 ? "supertonic2-" : "supertonic3-") + precision + "-" + voice + ".wav", 44100));
                }
                Assert.Greater(model.Synthesize("Bonjour, bienvenue.", language: "fr").Waveform.Length, 4410);
                Assert.Greater(model.Synthesize(phrase, "M1", speed: 1.2f).Waveform.Length, 4410);
            }
            string asrRoot = Environment.GetEnvironmentVariable("MNN_TEST_ASR_ROOT");
            var failures = new List<string>();
            using (var asr = MNNLlm.Load(asrRoot, cacheDirectory: Path.Combine(AdditionalTtsValidation.Artifacts, "supertonic3-asr"), backendType: MNNBackendType.CPU))
                foreach (string path in paths)
                {
                    string text = asr.GenerateMultimodal("Transcribe the spoken sentence in English.", audioPath: path, maxNewTokens: 64).Text;
                    File.WriteAllText(Path.ChangeExtension(path, "asr.txt"), text);
                    TestContext.WriteLine(Path.GetFileName(path) + ": " + text);
                    if (!text.ToLowerInvariant().Contains("france") || !text.ToLowerInvariant().Contains("paris"))
                    {
                        // A second recognizer distinguishes synthesis errors from
                        // accent-dependent errors in the first ASR model.
                        using (var independent = MNNLlm.Load(Environment.GetEnvironmentVariable("MNN_TEST_CHINESE_ASR_ROOT"),
                            cacheDirectory: Path.Combine(AdditionalTtsValidation.Artifacts, "supertonic2-independent-asr"), backendType: MNNBackendType.CPU))
                        {
                            string second = independent.GenerateConversation(new[]{
                                new MNNChatMessage(MNNChatRole.System, "You are a speech transcription engine. Return only the exact words spoken in the audio. Never answer questions or add explanations."),
                                new MNNChatMessage(MNNChatRole.User, "Transcribe the English speech exactly.")
                            }, audioPath: path, maxNewTokens: 96).Text;
                            File.WriteAllText(Path.ChangeExtension(path, "independent-asr.txt"), second);
                            TestContext.WriteLine(Path.GetFileName(path) + " independent: " + second);
                            if (!second.ToLowerInvariant().Contains("france") || !second.ToLowerInvariant().Contains("paris"))
                                failures.Add(Path.GetFileName(path) + ": " + text + " / independent: " + second);
                        }
                    }
                }
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }
    }
}
