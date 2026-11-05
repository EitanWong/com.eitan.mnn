using System;
using System.Collections;
using System.IO;
using System.Text.RegularExpressions;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MNN.Unity.Tests
{
    [Category("RepresentativeModel")]
    public class StudioInferenceTests
    {
        private static MNNStudioNativeBackend Load(string name) => MNNStudioNativeBackend.Load(MNNStudioModel.Read(MultimodalTestData.Model(name)), 4, false);

        [Test, Explicit("Uses the locally prepared official Hunyuan MNN repository; no download.")]
        public void OfficialHunyuanRepositoryLoadsAndRunsThroughStudioChat()
        {
            string directory = Environment.GetEnvironmentVariable("MNN_TEST_GENERIC_LLM_ROOT");
            if (string.IsNullOrWhiteSpace(directory) || !File.Exists(Path.Combine(directory, "llm.mnn")))
                Assert.Ignore("Set MNN_TEST_GENERIC_LLM_ROOT to a prepared MNN LLM repository.");
            var model = MNNStudioModel.Read(directory);
            Assert.AreEqual(MNNStudioTask.Chat, model.Task);
            Assert.True(MNNStudioTasks.Get((int)MNNStudioTask.Chat).CanRun(model));
            using (var backend = MNNStudioNativeBackend.Load(model, 4, false))
            {
                var timer = System.Diagnostics.Stopwatch.StartNew();
                var result = backend.Run(new MNNStudioRequest
                {
                    Conversation = new[]{new MNNChatMessage(MNNChatRole.User, "中国的首都是哪里？只回答城市名称。")},
                    TokenLimit = 64
                });
                TestContext.WriteLine("Studio Hunyuan: " + result.Text + "; tokens=" + result.Tokens + "; seconds=" + timer.Elapsed.TotalSeconds);
                Assert.IsNotEmpty(result.Text);
                StringAssert.Contains("北京", result.Text);
                Assert.That(result.Tokens, Is.InRange(1, 64));
                Assert.False(result.Limited);
            }
        }

        [Test, Explicit("Uses a locally prepared ModelScope MobileLLM MNN profile; no download.")]
        public void OfficialMobileLlmRepositoryLoadsAndRunsThroughStudioChat()
        {
            string directory = Environment.GetEnvironmentVariable("MNN_TEST_MOBILE_LLM_ROOT");
            if (string.IsNullOrWhiteSpace(directory) || !File.Exists(Path.Combine(directory, "llm.mnn")))
                Assert.Ignore("Set MNN_TEST_MOBILE_LLM_ROOT to the prepared MNN/MobileLLM-125M-MNN directory.");
            var model = MNNStudioModel.Read(directory);
            Assert.AreEqual(MNNStudioTask.Chat, model.Task);
            Assert.AreEqual(MNNModelCategory.LargeLanguageModel.ToString(), model.Category);
            Assert.True(MNNStudioTasks.Get((int)MNNStudioTask.Chat).CanRun(model));
            using (var backend = MNNStudioNativeBackend.Load(model, 4, false))
            {
                var timer = System.Diagnostics.Stopwatch.StartNew();
                var result = backend.Run(new MNNStudioRequest
                {
                    Conversation = new[]{new MNNChatMessage(MNNChatRole.User, "用一句话介绍你自己。")},
                    TokenLimit = 48
                });
                TestContext.WriteLine("Studio MobileLLM: " + result.Text + "; tokens=" + result.Tokens + "; seconds=" + timer.Elapsed.TotalSeconds);
                Assert.IsNotEmpty(result.Text);
                Assert.That(result.Tokens, Is.InRange(1, 48));
            }
        }

        [Test, Explicit("Probes raw Studio text generation for a locally prepared SmolLM2 profile; no download.")]
        public void OfficialSmolLlmRepositoryAnswersThroughStudioTextGeneration()
        {
            string directory = Environment.GetEnvironmentVariable("MNN_TEST_SMOL_LLM_ROOT");
            if (string.IsNullOrWhiteSpace(directory) || !File.Exists(Path.Combine(directory, "llm.mnn")))
                Assert.Ignore("Set MNN_TEST_SMOL_LLM_ROOT to the prepared MNN/SmolLM2-135M-Instruct-MNN directory.");
            var model = MNNStudioModel.Read(directory);
            Assert.AreEqual(MNNStudioTask.Chat, model.Task);
            Assert.AreEqual(MNNModelCategory.LargeLanguageModel.ToString(), model.Category);
            Assert.True(MNNStudioTasks.Get((int)MNNStudioTask.Chat).CanRun(model));
            using (var backend = MNNStudioNativeBackend.Load(model, 4, false))
            {
                var timer = System.Diagnostics.Stopwatch.StartNew();
                var result = backend.Run(new MNNStudioRequest
                {
                    Prompt = "What is 2 + 2? Reply with one digit.",
                    TokenLimit = 16
                });
                TestContext.WriteLine("Studio SmolLM2: " + result.Text + "; tokens=" + result.Tokens + "; seconds=" + timer.Elapsed.TotalSeconds);
                Assert.IsNotEmpty(result.Text);
                Assert.IsTrue(IsFourArithmeticAnswer(result.Text), "Expected an arithmetic answer of 4 or four; actual: " + result.Text);
                Assert.That(result.Tokens, Is.InRange(1, 16));
            }
        }

        [TestCase("4", true)]
        [TestCase("Four.", true)]
        [TestCase("Two plus two equals four.", true)]
        [TestCase("2 + 2 = 4", true)]
        [TestCase("The answer is FOUR.", true)]
        [TestCase("", false)]
        [TestCase("5", false)]
        [TestCase("14", false)]
        [TestCase("fourteen", false)]
        [TestCase("4.5", false)]
        [TestCase("Two plus two equals five.", false)]
        [TestCase("It is not four.", false)]
        public void ArithmeticAnswerRequiresFourInDigitsOrWords(string answer, bool expected)
        {
            Assert.AreEqual(expected, IsFourArithmeticAnswer(answer));
        }

        private static bool IsFourArithmeticAnswer(string answer) => answer != null && Regex.IsMatch(answer,
            @"^\s*(?:(?:two\s+plus\s+two|2\s*\+\s*2)\s*(?:equals?|is|=)\s*|the\s+answer\s+is\s+)?(?:4|four)\s*[.!]?\s*$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        [Test, Explicit("Uses a locally prepared official Qwen2.5-0.5B MNN profile; no download.")]
        public void OfficialQwen25SmallAnswersThroughStudioChat()
        {
            string directory = Environment.GetEnvironmentVariable("MNN_TEST_QWEN25_LLM_ROOT");
            if (string.IsNullOrWhiteSpace(directory) || !File.Exists(Path.Combine(directory, "llm.mnn")))
                Assert.Ignore("Set MNN_TEST_QWEN25_LLM_ROOT to the prepared MNN/Qwen2.5-0.5B-Instruct-MNN directory.");
            var model = MNNStudioModel.Read(directory);
            Assert.AreEqual(MNNStudioTask.Chat, model.Task);
            Assert.AreEqual(MNNModelCategory.LargeLanguageModel.ToString(), model.Category);
            Assert.True(MNNStudioTasks.Get((int)MNNStudioTask.Chat).CanRun(model));
            using (var backend = MNNStudioNativeBackend.Load(model, 4, false))
            {
                var timer = System.Diagnostics.Stopwatch.StartNew();
                var result = backend.Run(new MNNStudioRequest
                {
                    Conversation = new[]{new MNNChatMessage(MNNChatRole.User, "What is the capital of France? Reply with only the city name.")},
                    TokenLimit = 24
                });
                TestContext.WriteLine("Studio Qwen2.5-0.5B: " + result.Text + "; tokens=" + result.Tokens + "; seconds=" + timer.Elapsed.TotalSeconds);
                Assert.IsNotEmpty(result.Text);
                StringAssert.Contains("Paris", result.Text);
                Assert.That(result.Tokens, Is.InRange(1, 24));
            }
        }

        [Test, Explicit("Compares automatic accelerator selection with CPU on the locally prepared Qwen2.5-0.5B profile.")]
        public void OfficialQwen25SmallMeasuresAutomaticAndCpuGeneration()
        {
            string directory = Environment.GetEnvironmentVariable("MNN_TEST_QWEN25_LLM_ROOT");
            if (string.IsNullOrWhiteSpace(directory) || !File.Exists(Path.Combine(directory, "llm.mnn")))
                Assert.Ignore("Set MNN_TEST_QWEN25_LLM_ROOT to the prepared MNN/Qwen2.5-0.5B-Instruct-MNN directory.");
            foreach (var requested in new[]{MNNBackendType.Auto, MNNBackendType.CPU})
            {
                var timer = System.Diagnostics.Stopwatch.StartNew();
                using (var model = MNNLlm.Load(directory, threadCount: 4, backendType: requested))
                {
                    long load = timer.ElapsedMilliseconds;
                    timer.Restart();
                    var first = model.Generate("The capital of France is", 16);
                    long firstRun = timer.ElapsedMilliseconds;
                    timer.Restart();
                    var warm = model.Generate("The capital of France is", 16);
                    long warmRun = timer.ElapsedMilliseconds;
                    TestContext.WriteLine($"Qwen2.5 requested={requested}, actual={model.Backend}, load_ms={load}, first_ms={firstRun}, warm_ms={warmRun}, warm_tokens={warm.GeneratedTokens}, output={warm.Text}");
                    Assert.IsNotEmpty(first.Text);
                    Assert.IsNotEmpty(warm.Text);
                    Assert.Greater(warm.GeneratedTokens, 0);
                }
            }
        }

        [Test, Explicit("Uses a locally prepared official Qwen3-0.6B MNN profile; no download.")]
        public void OfficialQwen3SmallAnswersThroughStudioChat()
        {
            string directory = Environment.GetEnvironmentVariable("MNN_TEST_QWEN3_LLM_ROOT");
            if (string.IsNullOrWhiteSpace(directory) || !File.Exists(Path.Combine(directory, "llm.mnn")))
                Assert.Ignore("Set MNN_TEST_QWEN3_LLM_ROOT to the prepared MNN/Qwen3-0.6B-MNN directory.");
            var model = MNNStudioModel.Read(directory);
            Assert.AreEqual(MNNStudioTask.Chat, model.Task);
            Assert.AreEqual(MNNModelCategory.LargeLanguageModel.ToString(), model.Category);
            Assert.True(MNNStudioTasks.Get((int)MNNStudioTask.Chat).CanRun(model));
            using (var backend = MNNStudioNativeBackend.Load(model, 4, false))
            {
                var timer = System.Diagnostics.Stopwatch.StartNew();
                var result = backend.Run(new MNNStudioRequest
                {
                    Conversation = new[]{new MNNChatMessage(MNNChatRole.User, "What is the capital of France? Reply with only the city name.")},
                    TokenLimit = 32
                });
                TestContext.WriteLine("Studio Qwen3-0.6B: " + result.Text + "; tokens=" + result.Tokens + "; seconds=" + timer.Elapsed.TotalSeconds);
                Assert.IsNotEmpty(result.Text);
                StringAssert.Contains("Paris", result.Text);
                Assert.That(result.Tokens, Is.InRange(1, 32));
            }
        }

        [UnityTest]
        public IEnumerator ConversationService_UsesRealWorkerInference()
        {
            using (var session = new MNNStudioSession(Load("Qwen3.5-0.8B-MNN")))
            {
                session.Send(new MNNStudioRequest{Conversation = new[]{new MNNChatMessage(MNNChatRole.User, "What is 1 + 1? Answer with just the number.")}, TokenLimit = 24});
                var timer = System.Diagnostics.Stopwatch.StartNew();
                while (session.Busy && timer.Elapsed.TotalSeconds < 120)
                {
                    MNNStudioJobs.Update();
                    yield return null;
                }

                Assert.IsFalse(session.Busy);
                Assert.IsNull(session.Error);
                Assert.IsNotNull(session.Result);
                StringAssert.Contains("2", session.Result.Text);
                Assert.Greater(session.Result.Tokens, 0);
                TestContext.WriteLine("Studio text: " + session.Result.Text);
            }
        }

        [Test]
        public void ConversationService_PreservesSeparateRolesAndRemembersEarlierTurn()
        {
            using (var model = Load("Qwen3.5-0.8B-MNN"))
            {
                var user = new MNNChatMessage(MNNChatRole.User, "My name is Alice. Reply only OK.");
                var first = model.Run(new MNNStudioRequest{Conversation = new[]{user}, TokenLimit = 12});
                var second = model.Run(new MNNStudioRequest{Conversation = new[]{user, new MNNChatMessage(MNNChatRole.Assistant, first.Text), new MNNChatMessage(MNNChatRole.User, "What is my name? Answer with the name only.")}, TokenLimit = 24});
                StringAssert.Contains("alice", second.Text.ToLowerInvariant());
                TestContext.WriteLine("Studio second turn: " + second.Text);
            }
        }

        [Test]
        public void ConversationService_CompressesOldTurnsAndRetainsImportantFacts()
        {
            var messages = new System.Collections.Generic.List<MNNStudioMessage>();
            for (int i = 0; i < 12; ++i)
            {
                string fact = i == 0 ? "My name is Ada. Remember this important fact. " : "Historical detail " + i + ". ";
                messages.Add(new MNNStudioMessage(true, fact + new string ('q', 850)));
                messages.Add(new MNNStudioMessage(false, "Acknowledged. " + new string ('a', 850)));
            }

            using (var backend = MNNStudioNativeBackend.Load(MNNStudioModel.Read(MultimodalTestData.Model("Qwen3.5-0.8B-MNN")), 4, false))
            {
                var result = backend.Run(new MNNStudioRequest{Prompt = "What is my name? Answer with the name only.", History = messages.ToArray(), TokenLimit = 32});
                Assert.IsNotEmpty(result.ContextSummary);
                StringAssert.Contains("ada", result.ContextSummary.ToLowerInvariant());
                StringAssert.Contains("ada", result.Text.ToLowerInvariant());
            }
        }

        [Test]
        public void ConversationService_UnloadReloadRetainsMemoryAcrossMultipleTurnsWithoutLooping()
        {
            var history = new System.Collections.Generic.List<MNNStudioMessage>();
            const string introduction = "My name is Ada. My favorite color is blue. Reply only OK.";
            using (var first = Load("Qwen3.5-0.8B-MNN"))
            {
                var response = first.Run(new MNNStudioRequest{Prompt = introduction, History = history.ToArray(), TokenLimit = 24});
                history.Add(new MNNStudioMessage(true, introduction));
                history.Add(new MNNStudioMessage(false, response.Text));
            }

            using (var reloaded = Load("Qwen3.5-0.8B-MNN"))
            {
                foreach (string question in new[]{"What is my name? Answer with the name only.", "What is my favorite color? Answer with the color only.", "What is my name and favorite color? Answer briefly."})
                {
                    var answer = reloaded.Run(new MNNStudioRequest{Prompt = question, History = history.ToArray(), TokenLimit = 48});
                    Assert.IsFalse(answer.RepetitionStopped);
                    Assert.IsFalse(MNNStudioRepetition.TryTrim(answer.Text, out _));
                    StringAssert.Contains(question.Contains("color only") ? "blue" : "ada", answer.Text.ToLowerInvariant());
                    history.Add(new MNNStudioMessage(true, question));
                    history.Add(new MNNStudioMessage(false, answer.Text));
                    TestContext.WriteLine("Reloaded multi-turn: " + answer.Text);
                }
            }
        }

        [Test]
        public void ConversationApi_RejectsInvalidHistoryAndSupportsReload()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MNNChatMessage((MNNChatRole)99, "invalid"));
            Assert.Throws<ArgumentNullException>(() => new MNNChatMessage(MNNChatRole.User, null));
            using (var model = MNNLlm.Load(MultimodalTestData.Model("Qwen3.5-0.8B-MNN"), cacheDirectory: MultimodalTestData.Cache("studio-contract")))
            {
                var valid = new[]{new MNNChatMessage(MNNChatRole.User, "Say hello.")};
                Assert.Throws<ArgumentException>(() => model.GenerateConversation(Array.Empty<MNNChatMessage>()));
                Assert.Throws<ArgumentException>(() => model.GenerateConversation(new MNNChatMessage[]{null}));
                Assert.Throws<ArgumentException>(() => model.GenerateConversation(new[]{new MNNChatMessage(MNNChatRole.Assistant, "no user")}));
                Assert.Throws<ArgumentException>(() => model.GenerateConversation(new[]{valid[0], new MNNChatMessage(MNNChatRole.System, "misplaced"), valid[0]}));
                Assert.Throws<ArgumentOutOfRangeException>(() => model.GenerateConversation(valid, 0));
                model.Dispose();
                Assert.Throws<ObjectDisposedException>(() => model.GenerateConversation(valid));
            }

            MultimodalTestData.CleanCache("studio-contract");
        }

        [Test]
        public void VisionService_ProcessesAttachedImage()
        {
            using (var model = Load("SmolVLM-256M-Instruct-MNN"))
            {
                var result = model.Run(new MNNStudioRequest{Conversation = new[]{new MNNChatMessage(MNNChatRole.User, "What is the main color in this image? Answer in one word.")}, Image = MultimodalTestData.Fixture("red.png"), TokenLimit = 24});
                StringAssert.Contains("red", result.Text.ToLowerInvariant());
                TestContext.WriteLine("Studio vision: " + result.Text);
            }
        }

        [Test]
        public void AudioService_TranscribesAttachedWave()
        {
            using (var model = Load("LFM2.5-Audio-1.5B-MNN"))
            {
                var result = model.Run(new MNNStudioRequest{Conversation = new[]{new MNNChatMessage(MNNChatRole.User, "Transcribe the spoken sentence in English.")}, Audio = MultimodalTestData.Fixture("speech.wav"), TokenLimit = 64});
                StringAssert.Contains("paris", result.Text.ToLowerInvariant());
                TestContext.WriteLine("Studio audio: " + result.Text);
            }
        }

        [Test]
        public void OmniService_ReturnsMediaTextAndRejectsSpeech()
        {
            using (var model = Load("Qwen2.5-Omni-3B-MNN"))
            {
                var media = model.Run(new MNNStudioRequest{Conversation = new[]{new MNNChatMessage(MNNChatRole.User, "Name the color of the image, then name the city mentioned in the audio.")}, Image = MultimodalTestData.Fixture("blue.png"), Audio = MultimodalTestData.Fixture("speech.wav"), TokenLimit = 64});
                StringAssert.Contains("blue", media.Text.ToLowerInvariant());
                StringAssert.Contains("paris", media.Text.ToLowerInvariant());
                Assert.Throws<PlatformNotSupportedException>(() => model.Run(new MNNStudioRequest{Conversation = new[]{new MNNChatMessage(MNNChatRole.User, "Say hello in one short sentence.")}, TokenLimit = 32, Speech = true}));
                TestContext.WriteLine("Studio Omni: " + media.Text);
            }
        }

        [Test]
        public void EmbeddingService_ComparesActualVectors()
        {
            using (var model = Load("Qwen3-Embedding-0.6B-MNN"))
            {
                const string query = "Instruct: Given a query, retrieve relevant passages.\nQuery: What is the capital of France?";
                var related = model.Run(new MNNStudioRequest{Prompt = query, Document = "Paris is the capital of France."});
                var unrelated = model.Run(new MNNStudioRequest{Prompt = query, Document = "Bananas grow in tropical climates."});
                Assert.AreEqual(1024, related.Vector.Length);
                Assert.Greater(related.Score.Value, unrelated.Score.Value + .1f);
                TestContext.WriteLine("Studio embedding: " + related.Score + " / " + unrelated.Score);
            }
        }

        [Test]
        public void RerankingService_ScoresRealDocuments()
        {
            using (var model = Load("Qwen3-Reranker-0.6B-MNN"))
            {
                const string query = "What is the capital of France?";
                var related = model.Run(new MNNStudioRequest{Prompt = query, Document = "Paris is the capital of France."});
                var unrelated = model.Run(new MNNStudioRequest{Prompt = query, Document = "Bananas grow in tropical climates."});
                Assert.Greater(related.Score.Value, unrelated.Score.Value + .5f);
                TestContext.WriteLine("Studio reranking: " + related.Score + " / " + unrelated.Score);
            }
        }
    }
}
