using System;
using System.Collections;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MNN.Unity.Tests
{
    [Category("RepresentativeModel")]
    public class StudioInferenceTests
    {
        private static MNNStudioNativeBackend Load(string name) => MNNStudioNativeBackend.Load(MNNStudioModel.Read(MultimodalTestData.Model(name)), 4, false);
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
        public void OmniService_ReturnsTextAndPlayableSpeech()
        {
            using (var model = Load("Qwen2.5-Omni-3B-MNN"))
            {
                var media = model.Run(new MNNStudioRequest{Conversation = new[]{new MNNChatMessage(MNNChatRole.User, "Name the color of the image, then name the city mentioned in the audio.")}, Image = MultimodalTestData.Fixture("blue.png"), Audio = MultimodalTestData.Fixture("speech.wav"), TokenLimit = 64});
                StringAssert.Contains("blue", media.Text.ToLowerInvariant());
                StringAssert.Contains("paris", media.Text.ToLowerInvariant());
                var result = model.Run(new MNNStudioRequest{Conversation = new[]{new MNNChatMessage(MNNChatRole.User, "Say hello in one short sentence.")}, TokenLimit = 32, Speech = true});
                StringAssert.Contains("hello", result.Text.ToLowerInvariant());
                Assert.AreEqual(24000, result.SampleRate);
                Assert.Greater(result.Waveform.Length, 1000);
                Assert.That(Array.Exists(result.Waveform, value => Math.Abs(value) > .001f));
                Assert.That(Array.TrueForAll(result.Waveform, value => !float.IsNaN(value) && !float.IsInfinity(value)));
                byte[] wav = MNNStudioAudio.EncodeWave(result.Waveform, result.SampleRate);
                Assert.AreEqual(44 + result.Waveform.Length * 2, wav.Length);
                TestContext.WriteLine("Studio Omni: " + media.Text + "\nVoice: " + result.Text + " (" + result.Waveform.Length + " samples)");
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
