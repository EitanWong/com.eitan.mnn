using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MNN.Unity.Tests
{
    public class StudioTests
    {
        private string _root;
        [SetUp]
        public void Setup()
        {
            _root = Path.GetFullPath(Path.Combine("Library", "MNN", "StudioTests", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void Cleanup()
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, true);
            MNNStudioJobs.Update();
        }

        private string Model(string name, string config)
        {
            string path = Path.Combine(_root, name);
            Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(path, "config.json"), "{}");
            File.WriteAllText(Path.Combine(path, "llm_config.json"), config);
            return path;
        }

        [Test]
        public void Discovery_UsesConfigCapabilitiesRatherThanModelName()
        {
            Model("Qwen3.5-0.8B-MNN", "{\"is_visual\":true}");
            var models = MNNStudioModel.Discover(new[]{_root, _root});
            Assert.AreEqual(1, models.Count);
            Assert.That(models[0].Supports(MNNModelCapabilities.Vision));
            Assert.AreEqual("Vision + text", models[0].CapabilityLabel);
        }

        [Test]
        public void Discovery_CorruptMetadataDoesNotHideOtherModels()
        {
            Model("broken", "{invalid");
            Model("valid", "{}");
            Directory.CreateDirectory(Path.Combine(_root, "incomplete"));
            var errors = new List<string>();
            var models = MNNStudioModel.Discover(new[]{_root, Path.Combine(_root, "missing")}, errors);
            Assert.AreEqual(1, models.Count);
            Assert.AreEqual("valid", models[0].Name);
            Assert.AreEqual(1, errors.Count);
        }

        [TestCase("Qwen3-Embedding-MNN", 1)]
        [TestCase("Qwen3-Reranker-MNN", 2)]
        [TestCase("CustomLanguageModel", 0)]
        [TestCase("bge-reranker-v2-MNN", 2)]
        public void TaskClassification_HasNoUnknownCategory(string name, int expected)
        {
            Assert.AreEqual((MNNStudioTask)expected, MNNStudioModel.Read(Model(name, "{}")).Task);
        }

        [Test]
        public void SpeechAvailability_RequiresVerifiedTalkerContract()
        {
            var supported = MNNStudioModel.Read(Model("custom", "{\"is_visual\":true,\"is_audio\":true,\"has_talker\":true,\"image_pad\":151655,\"hidden_size\":2048}"));
            Assert.AreEqual("Omni", supported.CapabilityLabel);
            Assert.That(supported.Supports(MNNModelCapabilities.SpeechOutput));
            var future = MNNStudioModel.Read(Model("Qwen-New-Omni", "{\"is_visual\":true,\"is_audio\":true,\"has_talker\":true,\"talker_type\":\"new\"}"));
            Assert.That(!future.Supports(MNNModelCapabilities.SpeechOutput));
        }

        [Test]
        public void Prompt_IndependentFirstTurnAndBoundedCompleteHistory()
        {
            Assert.AreEqual("Hello", MNNStudioPrompt.Build(null, "Hello")[0].Content);
            var messages = new List<MNNStudioMessage>();
            for (int i = 0; i < 30; ++i)
            {
                messages.Add(new MNNStudioMessage(true, "question-" + i + new string ('x', 900)));
                messages.Add(new MNNStudioMessage(false, "answer-" + i + new string ('y', 900)));
            }

            var prompt = MNNStudioPrompt.Build(messages, "Follow up");
            int length = 0;
            foreach (var message in prompt)
                length += message.Content.Length;
            Assert.LessOrEqual(length, MNNStudioPrompt.MaximumCharacters);
            Assert.AreEqual(MNNChatRole.User, prompt[0].Role);
            StringAssert.StartsWith("question-", prompt[0].Content);
            StringAssert.StartsWith("answer-29", prompt[prompt.Length - 2].Content);
            Assert.AreEqual("Follow up", prompt[prompt.Length - 1].Content);
            Assert.AreEqual(MNNChatRole.User, prompt[prompt.Length - 1].Role);
            Assert.Throws<ArgumentException>(() => MNNStudioPrompt.Build(messages, " "));
            Assert.Throws<ArgumentException>(() => MNNStudioPrompt.Build(messages, new string ('x', 16001)));
        }

        [Test]
        public void PromptPlan_CompressesOldTurnsAndKeepsRecentRolesAndSummary()
        {
            var messages = new List<MNNStudioMessage>();
            for (int i = 0; i < 12; ++i)
            {
                messages.Add(new MNNStudioMessage(true, "question-" + i + new string ('q', 900)));
                messages.Add(new MNNStudioMessage(false, "answer-" + i + new string ('a', 900)));
            }

            var prompts = new List<MNNChatMessage[]>();
            var plan = MNNStudioContext.Prepare(messages, "latest question", "Earlier fact: name is Alice.", 0, 8192, 256, 0, text => text.Length / 4, prompt =>
            {
                prompts.Add(prompt);
                return "Alice prefers short answers.";
            }, null, default);
            Assert.Greater(prompts.Count, 1);
            StringAssert.Contains("question-0", prompts[0][1].Content);
            Assert.That(prompts.TrueForAll(prompt => !prompt[1].Content.Contains("question-11")));
            Assert.AreEqual(16, plan.Covered);
            Assert.AreEqual("latest question", plan.Conversation[plan.Conversation.Length - 1].Content);
            StringAssert.Contains("Alice", plan.Conversation[0].Content);
            Assert.AreEqual("Answer without repeating.\nKeep the choice.", MNNStudioPrompt.CleanSummary("<think>reasoning</think>\nAnswer without repeating.\nKeep the choice."));
            Assert.AreEqual("", MNNStudioPrompt.CleanSummary("<think>unfinished"));
        }

        [Test]
        public void Prompt_MergesConsecutiveUsersAfterCancelledOrFailedTurn()
        {
            var pending = new List<MNNStudioMessage>{new MNNStudioMessage(true, "First unanswered question")};
            var prompt = MNNStudioPrompt.Build(pending, "Follow-up after cancellation");
            Assert.AreEqual(1, prompt.Length);
            Assert.AreEqual(MNNChatRole.User, prompt[0].Role);
            StringAssert.Contains("First unanswered question", prompt[0].Content);
            StringAssert.Contains("Follow-up after cancellation", prompt[0].Content);
        }

        [Test]
        public void ModelOptions_OnlyStudioChatEnablesRepetitionPenaltyAtLoad()
        {
            string model = Model("sampling", "{}");
            File.WriteAllText(Path.Combine(model, "config.json"), "{\"sampler_type\":\"mixed\",\"mixed_samplers\":[\"topK\",\"topP\"],\"topK\":20}");
            var runtime = MNN.Unity.MNNModelOptions.Prepare(model, 4, false, Path.Combine(_root, "runtime"), MNN.Unity.MNNPrecisionMode.Normal);
            var studio = MNN.Unity.MNNModelOptions.Prepare(model, 4, false, Path.Combine(_root, "studio"), MNN.Unity.MNNPrecisionMode.Normal, true);
            StringAssert.Contains("greedy", runtime.Json);
            StringAssert.Contains("penalty", studio.Json);
            StringAssert.Contains("repetition_penalty", studio.Json);
            StringAssert.Contains("1.1", studio.Json);
            StringAssert.Contains("topK", studio.Json);
            StringAssert.Contains("topP", studio.Json);
            StringAssert.DoesNotContain("mixed_samplers:null", studio.Json);
        }

        [Test]
        public void WaveEncoding_HasValidHeaderClippedSamplesAndExactDuration()
        {
            byte[] wave = MNNStudioAudio.EncodeWave(new[]{-2f, -.5f, 0, .5f, 2f}, 16000);
            Assert.AreEqual(54, wave.Length);
            using (var reader = new BinaryReader(new MemoryStream(wave)))
            {
                Assert.AreEqual("RIFF", new string (reader.ReadChars(4)));
                Assert.AreEqual(46, reader.ReadInt32());
                Assert.AreEqual("WAVEfmt ", new string (reader.ReadChars(8)));
                Assert.AreEqual(16, reader.ReadInt32());
                Assert.AreEqual(1, reader.ReadInt16());
                Assert.AreEqual(1, reader.ReadInt16());
                Assert.AreEqual(16000, reader.ReadInt32());
                Assert.AreEqual(32000, reader.ReadInt32());
                Assert.AreEqual(2, reader.ReadInt16());
                Assert.AreEqual(16, reader.ReadInt16());
                Assert.AreEqual("data", new string (reader.ReadChars(4)));
                Assert.AreEqual(10, reader.ReadInt32());
                CollectionAssert.AreEqual(new short[]{-32767, -16383, 0, 16383, 32767}, new[]{reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16()});
            }
        }

        [Test]
        public void WaveEncoding_InvalidInputsFail()
        {
            Assert.Throws<ArgumentNullException>(() => MNNStudioAudio.EncodeWave(null, 16000));
            Assert.Throws<ArgumentException>(() => MNNStudioAudio.EncodeWave(Array.Empty<float>(), 16000));
            Assert.Throws<ArgumentOutOfRangeException>(() => MNNStudioAudio.EncodeWave(new[]{0f}, 1));
            Assert.Throws<ArgumentException>(() => MNNStudioAudio.EncodeWave(new[]{float.NaN}, 16000));
            Assert.Throws<ArgumentException>(() => MNNStudioAudio.EncodeWave(new[]{float.PositiveInfinity}, 16000));
        }

        [Test]
        public void Cosine_HandlesOrthogonalOppositeAndLargeVectors()
        {
            Assert.AreEqual(0, MNNStudioPrompt.Cosine(new[]{1f, 0}, new[]{0f, 1}));
            Assert.AreEqual(-1, MNNStudioPrompt.Cosine(new[]{1f}, new[]{-1f}));
            Assert.AreEqual(1, MNNStudioPrompt.Cosine(new[]{float.MaxValue}, new[]{float.MaxValue}));
            Assert.Throws<ArgumentException>(() => MNNStudioPrompt.Cosine(new[]{0f}, new[]{1f}));
            Assert.Throws<ArgumentException>(() => MNNStudioPrompt.Cosine(new[]{float.NaN}, new[]{1f}));
            Assert.Throws<ArgumentException>(() => MNNStudioPrompt.Cosine(new[]{1f}, new[]{1f, 2f}));
        }

        private sealed class ControlledBackend : IMNNStudioBackend
        {
            internal readonly ManualResetEventSlim Gate = new ManualResetEventSlim(false);
            internal int DisposalCount, InferenceThread;
            internal bool Fail;
            public MNNStudioResult Run(MNNStudioRequest request)
            {
                InferenceThread = Thread.CurrentThread.ManagedThreadId;
                if (!Gate.Wait(TimeSpan.FromSeconds(10)))
                    throw new TimeoutException("Test worker timed out.");
                if (Fail)
                    throw new InvalidOperationException("Inference failed");
                return new MNNStudioResult{Text = request.Prompt};
            }

            public void Dispose()
            {
                ++DisposalCount;
            }
        }

        private static IEnumerator Drain(MNNStudioSession session)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            while (session.Busy && timer.Elapsed.TotalSeconds < 10)
            {
                MNNStudioJobs.Update();
                yield return null;
            }

            Assert.IsFalse(session.Busy, "The worker must complete.");
        }

        [UnityTest]
        public IEnumerator Session_CloseDuringInferenceDefersDisposalAndSuppressesDelivery()
        {
            var backend = new ControlledBackend();
            var session = new MNNStudioSession(backend);
            int deliveries = 0, releases = 0;
            session.Completed += () => ++deliveries;
            session.Released += () => ++releases;
            try
            {
                session.Send(new MNNStudioRequest{Prompt = "hello"});
                Assert.Throws<InvalidOperationException>(() => session.Send(new MNNStudioRequest{Prompt = "overlap"}));
                session.Dispose();
                session.Dispose();
                Assert.AreEqual(0, backend.DisposalCount);
                backend.Gate.Set();
                yield return Drain(session);
                Assert.AreEqual(1, backend.DisposalCount);
                Assert.AreEqual(1, releases);
                Assert.AreEqual(0, deliveries);
                Assert.IsNull(session.Result);
                Assert.Throws<ObjectDisposedException>(() => session.Send(new MNNStudioRequest{Prompt = "closed"}));
            }
            finally
            {
                backend.Gate.Set();
                session.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator Session_DeliversOnEditorThreadAndRecoversFromFailure()
        {
            int editorThread = Thread.CurrentThread.ManagedThreadId, deliveryThread = 0;
            var backend = new ControlledBackend{Fail = true};
            var session = new MNNStudioSession(backend);
            session.Completed += () => deliveryThread = Thread.CurrentThread.ManagedThreadId;
            try
            {
                backend.Gate.Set();
                session.Send(new MNNStudioRequest{Prompt = "first"});
                yield return Drain(session);
                Assert.AreEqual("Inference failed", session.Error);
                Assert.IsNull(session.Result);
                backend.Fail = false;
                session.Send(new MNNStudioRequest{Prompt = "second"});
                yield return Drain(session);
                Assert.IsNull(session.Error);
                Assert.AreEqual("second", session.Result.Text);
                Assert.AreEqual(editorThread, deliveryThread);
                Assert.AreNotEqual(editorThread, backend.InferenceThread);
            }
            finally
            {
                session.Dispose();
                backend.Gate.Set();
            }

            Assert.AreEqual(1, backend.DisposalCount);
        }

        [UnityTest]
        public IEnumerator Session_DiscardFinishesSafelyAndAllowsNextRequest()
        {
            var backend = new ControlledBackend();
            var session = new MNNStudioSession(backend);
            try
            {
                session.Send(new MNNStudioRequest{Prompt = "discard"});
                session.DiscardReply();
                Assert.IsTrue(session.Busy);
                backend.Gate.Set();
                yield return Drain(session);
                Assert.IsNull(session.Result);
                session.Send(new MNNStudioRequest{Prompt = "keep"});
                yield return Drain(session);
                Assert.AreEqual("keep", session.Result.Text);
            }
            finally
            {
                backend.Gate.Set();
                session.Dispose();
            }
        }
    }
}
