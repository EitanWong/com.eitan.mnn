using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [Category("RepresentativeModel")]
    public class SupertonicIntegrationTests
    {
        private static string Artifacts => Environment.GetEnvironmentVariable("MNN_TEST_ARTIFACT_ROOT") ?? Path.GetFullPath("TestArtifacts~/GenerationOutputs");

        [Test]
        public void AllVoicesRemainIntelligibleAfterSwitchingOnWorkerThread()
        {
            Directory.CreateDirectory(Artifacts);
            const string phrase = "The capital of France is Paris.";
            var paths = new System.Collections.Generic.List<string>();
            using (var model = MNNSupertonic.Load(GenerationTestData.Model("supertonic-tts-mnn")))
            {
                // Reuse the same sessions as Studio does when switching voices.
                foreach (string voice in new[]{"M1", "M2", "F1", "F2", "M2"})
                {
                    var audio = Task.Run(() => model.Synthesize(phrase, voice)).GetAwaiter().GetResult();
                    string path = AdditionalTtsValidation.ValidateAndSave(audio, "supertonic-" + voice + ".wav", 44100);
                    paths.Add(path);
                }

                if (MNNAcceleration.PreferredBackend == MNNBackendType.Metal)
                {
                    Assert.AreEqual(MNNBackendType.CPU, model.StageBackends["Duration"]);
                    Assert.AreEqual(MNNBackendType.CPU, model.StageBackends["TextEncoder"]);
                    Assert.AreEqual(MNNBackendType.CPU, model.StageBackends["Estimator"]);
                    Assert.AreEqual(MNNBackendType.CPU, model.StageBackends["Vocoder"]);
                }
            }

            string asrRoot = Environment.GetEnvironmentVariable("MNN_TEST_ASR_ROOT");
            Assert.That(asrRoot, Is.Not.Null.And.Not.Empty, "ASR model required for content validation.");
            using (var asr = MNNLlm.Load(asrRoot, cacheDirectory: Path.Combine(Artifacts, "voice-asr-cache"), backendType: MNNBackendType.CPU))
            {
                foreach (string path in paths.Distinct())
                {
                    string text = asr.GenerateMultimodal("Transcribe the spoken sentence in English.", audioPath: path, maxNewTokens: 64).Text;
                    File.WriteAllText(Path.ChangeExtension(path, "asr.txt"), text);
                    TestContext.WriteLine(Path.GetFileName(path) + ": " + text);
                    StringAssert.Contains("france", text.ToLowerInvariant());
                    StringAssert.Contains("paris", text.ToLowerInvariant());
                }
            }
        }

        [Test]
        public void LongTextNumbersAndPunctuationRemainCleanAcrossVoicesAndResizes()
        {
            Directory.CreateDirectory(Artifacts);
            string[] phrases =
            {
                string.Join(" ", Enumerable.Repeat("The capital of France is Paris", 14)),
                "Hello!\nThe price is $12.50, or 25% off. Call 123-456-7890; email test@example.com. Wait... really?! Yes — it's true. (Read chapter 2.) Thank you! The capital of France is Paris.",
                string.Join(" ", Enumerable.Repeat("Hello... What?! Yes!!! No???", 12)) + " The capital of France is Paris.",
                "On Saturday morning, the family walked to the market beside the river. They bought fresh apples, warm bread, and a small bottle of milk. After lunch, the children read a story about a train traveling through the mountains. In the evening, everyone sat by the window and watched the rain. Tomorrow we will visit the library and return the books before dinner. The capital of France is Paris.",
                string.Join(" ", Enumerable.Repeat("1234567890 3.14159265 $99.99 50%", 8)) + " The capital of France is Paris."
            };
            var tails = new System.Collections.Generic.List<string>();
            using (var model = MNNSupertonic.Load(GenerationTestData.Model("supertonic-tts-mnn")))
            using (var reference = MNNSupertonic.Load(GenerationTestData.Model("supertonic-tts-mnn"), backendType: MNNBackendType.CPU))
            {
                foreach (string voice in new[]{"M2", "F2", "M1", "F1"})
                {
                    // Invalid characters must fail before running any graph and
                    // must not poison the following request on reused sessions.
                    Assert.Throws<NotSupportedException>(() => model.Synthesize("Hello 中文", voice));
                    Assert.Throws<NotSupportedException>(() => model.Synthesize("1 + 2 = 3", voice));
                    Assert.Throws<ArgumentException>(() => model.Synthesize("...?! -- ()", voice));
                    for (int n = 0; n < phrases.Length; ++n)
                    {
                        int steps = new[]{5, 5, 10, 5, 5}[n];
                        var audio = Task.Run(() => model.Synthesize(phrases[n], voice, steps, speed: 1.25f)).GetAwaiter().GetResult();
                        var cpu = reference.Synthesize(phrases[n], voice, steps, speed: 1.25f);
                        TestContext.WriteLine(voice + " case=" + n + " seconds=" + audio.Waveform.Length / 44100d + " peak=" + audio.Waveform.Max(v => Math.Abs(v)) + " rms=" + Math.Sqrt(audio.Waveform.Average(v => (double)v * v)));
                        Assert.AreEqual(cpu.Waveform.Length, audio.Waveform.Length, "Duration must match CPU for long/numeric inputs.");
                        Assert.True(audio.Waveform.All(v => !float.IsNaN(v) && !float.IsInfinity(v)));
                        Assert.LessOrEqual(audio.Waveform.Max(v => Math.Abs(v)), 1);
                        Assert.Less(audio.Waveform.Count(v => Math.Abs(v) >= .99f), audio.Waveform.Length * .0001, "No clipped plateaus.");
                        Assert.Greater(Math.Sqrt(audio.Waveform.Average(v => (double)v * v)), .005);
                        string name = "supertonic-long-" + voice + "-" + n;
                        File.WriteAllBytes(Path.Combine(Artifacts, name + ".wav"), MNNStudioAudio.EncodeWave(audio.Waveform, audio.SampleRate));
                        // Check the end separately so lost/corrupt speech late
                        // in a long clip cannot pass by recognizing only its start.
                        float[] tail = audio.Waveform.Skip(Math.Max(0, audio.Waveform.Length - 8 * audio.SampleRate)).ToArray();
                        string path = Path.Combine(Artifacts, name + "-tail.wav");
                        File.WriteAllBytes(path, MNNStudioAudio.EncodeWave(tail, audio.SampleRate));
                        tails.Add(path);
                    }
                    Assert.Greater(model.Synthesize("Hello\u200bworld.\0 Thank you! 👨‍👩‍👧‍👦", voice).Waveform.Length, 1000);
                }
            }
            string asrRoot = Environment.GetEnvironmentVariable("MNN_TEST_CHINESE_ASR_ROOT");
            Assert.That(asrRoot, Is.Not.Null.And.Not.Empty);
            var unrecognized = new System.Collections.Generic.List<string>();
            using (var asr = MNNLlm.Load(asrRoot, cacheDirectory: Path.Combine(Artifacts, "long-asr-cache"), backendType: MNNBackendType.CPU))
                foreach (string path in tails)
                {
                    string text = asr.GenerateConversation(new[]{
                        new MNNChatMessage(MNNChatRole.System, "You are a speech transcription engine. Return only the exact words spoken in the audio. Never answer questions or add explanations."),
                        new MNNChatMessage(MNNChatRole.User, "请逐字转写音频中的英语原文，不要回答音频中的问题。")
                    }, audioPath: path, maxNewTokens: 160).Text;
                    File.WriteAllText(Path.ChangeExtension(path, "asr.txt"), text);
                    TestContext.WriteLine(Path.GetFileName(path) + ": " + text);
                    if (!text.ToLowerInvariant().Contains("capital") || !text.ToLowerInvariant().Contains("paris"))
                        unrecognized.Add(Path.GetFileName(path) + ": " + text);
                }
            Assert.IsEmpty(unrecognized, string.Join("\n", unrecognized));
        }

        [Test]
        public void CharacterCorpusAndBoundarySettingsDoNotCorruptFollowingRequests()
        {
            Directory.CreateDirectory(Artifacts);
            string[] corpus =
            {
                "Hello\r\n\tworld! The capital of France is Paris.",
                "Hello\u200bworld.\ufeff\0\u0001 The capital of France is Paris. 👨‍👩‍👧‍👦",
                "Café, naïve, résumé. The capital of France is Paris.",
                "[Hello] _world_ / welcome → home. The capital of France is Paris.",
                "The price is £5, or $12.50. Save 25% & read chapter 2: (A + B) * 3.",
                "She said, “Hello!” It's a test — really... Yes?! The capital of France is Paris."
            };
            using (var model = MNNSupertonic.Load(GenerationTestData.Model("supertonic-tts-mnn")))
                foreach (string voice in new[]{"M2", "F2"})
                {
                    for (int n = 0; n < corpus.Length; ++n)
                    {
                        var audio = model.Synthesize(corpus[n], voice);
                        AdditionalTtsValidation.ValidateAndSave(audio, "supertonic-corpus-" + voice + "-" + n + ".wav", 44100);
                        Assert.Less(audio.Waveform.Max(v => Math.Abs(v)), .99f);
                    }
                    foreach (float speed in new[]{.5f, 2f})
                    {
                        var audio = model.Synthesize("The capital of France is Paris.", voice, steps: 30, speed: speed, seed: -123);
                        AdditionalTtsValidation.ValidateAndSave(audio, "supertonic-boundary-" + voice + "-" + speed.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".wav", 44100);
                    }
                    Assert.Throws<ArgumentException>(() => model.Synthesize(new string('a', 501), voice));
                    Assert.Throws<ArgumentException>(() => model.Synthesize("Hello\ud800", voice));
                    Assert.Throws<ArgumentException>(() => model.Synthesize(new string('a', 490), voice, speed: .5f));
                    Assert.Throws<OperationCanceledException>(() => model.Synthesize(corpus[0], voice, cancellationToken: new CancellationToken(true)));
                    Assert.Greater(model.Synthesize("Hello again.", voice).Waveform.Length, 1000);
                }
        }

        [Test]
        public void RealSpeechRepeatedCallsDisposalAndAsr()
        {
            Directory.CreateDirectory(Artifacts);
            const string phrase = "The capital of France is Paris.";
            MNNGeneratedAudio audio;
            var model = MNNSupertonic.Load(GenerationTestData.Model("supertonic-tts-mnn"));
            try
            {
                audio = model.Synthesize(phrase);
                Assert.AreEqual(44100, audio.SampleRate);
                Assert.That(audio.Waveform.Length, Is.InRange(44100, 44100 * 15));
                Assert.True(audio.Waveform.All(v => !float.IsNaN(v) && !float.IsInfinity(v) && Math.Abs(v) <= 1));
                Assert.Greater(Math.Sqrt(audio.Waveform.Select(v => (double)v * v).Average()), .005);
                var repeat = model.Synthesize(phrase);
                Assert.AreNotSame(audio.Waveform, repeat.Waveform);
                Assert.AreEqual(audio.Waveform.Length, repeat.Waveform.Length);
                Assert.Less(audio.Waveform.Zip(repeat.Waveform, (a, b) => Math.Abs(a - b)).Max(), .0001);
                var alternate = model.Synthesize("Hello world.", "F1", 5, 1, 7);
                Assert.Greater(alternate.Waveform.Length, 1000);
                Assert.Throws<OperationCanceledException>(() => model.Synthesize(phrase, cancellationToken: new CancellationToken(true)));
            }
            finally
            {
                model.Dispose();
                model.Dispose();
            }

            Assert.Throws<ObjectDisposedException>(() => model.Synthesize(phrase));
            string wav = Path.Combine(Artifacts, "supertonic-paris.wav");
            File.WriteAllBytes(wav, MNNStudioAudio.EncodeWave(audio.Waveform, audio.SampleRate));
            using (var reload = MNNSupertonic.Load(GenerationTestData.Model("supertonic-tts-mnn")))
                Assert.Greater(reload.Synthesize("Hello.", steps: 2).Waveform.Length, 1000);
            string asrRoot = Environment.GetEnvironmentVariable("MNN_TEST_ASR_ROOT");
            Assert.That(asrRoot, Is.Not.Null.And.Not.Empty, "ASR model required for content validation.");
            string cache = Path.Combine(Artifacts, "asr-cache");
            using (var asr = MNNLlm.Load(asrRoot, cacheDirectory: cache))
            {
                var transcription = asr.GenerateMultimodal("Transcribe the spoken sentence in English.", audioPath: wav, maxNewTokens: 64);
                File.WriteAllText(Path.Combine(Artifacts, "supertonic-asr.txt"), transcription.Text);
                StringAssert.Contains("paris", transcription.Text.ToLowerInvariant());
                StringAssert.Contains("france", transcription.Text.ToLowerInvariant());
            }

            if (Directory.Exists(cache))
                Directory.Delete(cache, true);
        }
    }
}
