using System;
using System.IO;
using System.Threading;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    // Optional local fixtures: never download or put large models into a test
    // Player. The runner requires all six tests to pass when fixtures are supplied.
    [Category("RepresentativePlayer")]
    public class PlayerRepresentativeTests
    {
        private string _root, _fixtures, _cache;
        [SetUp]
        public void RequireLocalFixtures()
        {
            _root = Environment.GetEnvironmentVariable("MNN_PLAYER_MODEL_ROOT");
            _fixtures = Environment.GetEnvironmentVariable("MNN_PLAYER_FIXTURES");
            if (string.IsNullOrEmpty(_root) || string.IsNullOrEmpty(_fixtures))
                Assert.Ignore("Provide the runner's --model-root and --fixtures for six real model tests.");
            Assert.IsTrue(MNNPlatformSupport.IsSupported, MNNPlatformSupport.UnsupportedReason);
            _cache = Path.Combine(Environment.GetEnvironmentVariable("MNN_PLAYER_ARTIFACTS"), "Cache", TestContext.CurrentContext.Test.ID);
        }

        [TearDown]
        public void CleanCache()
        {
            if (_cache != null && Directory.Exists(_cache))
                Directory.Delete(_cache, true);
        }

        private string Model(string name)
        {
            string path = Path.Combine(_root, name);
            Assert.IsTrue(File.Exists(Path.Combine(path, "config.json")), "Missing local model: " + path);
            return path;
        }

        private string Fixture(string name)
        {
            string path = Path.Combine(_fixtures, name);
            Assert.IsTrue(File.Exists(path), "Missing local input: " + path);
            return path;
        }

        [Test]
        public void Qwen_TextAndTokenizer_ReturnOwnedResults()
        {
            using (var model = MNNLlm.Load(Model("Qwen3.5-0.8B-MNN"), cacheDirectory: _cache))
            {
                string text = "Tokenizer ownership: 中文与 Emoji 😀, long enough to allocate native storage.";
                int count = model.CountTokens(text);
                Assert.Greater(count, 0);
                Assert.AreEqual(count, model.CountTokens(text));
                GC.Collect();
                GC.WaitForPendingFinalizers();
                var result = model.Generate("只回答中国的首都是哪个城市，不要解释。", 32);
                TestContext.WriteLine("Qwen: " + result.Text);
                StringAssert.Contains("北京", result.Text);
                var user = new MNNChatMessage(MNNChatRole.User, "My name is Alice. Reply only OK.");
                var first = model.GenerateConversation(new[]{user}, 12);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                int updates = 0;
                var second = model.GenerateConversationStreaming(new[]{user, new MNNChatMessage(MNNChatRole.Assistant, first.Text), new MNNChatMessage(MNNChatRole.User, "What is my name? Answer with the name only.")}, update => ++updates, 24);
                TestContext.WriteLine("Conversation second turn: " + second.Text);
                StringAssert.Contains("alice", second.Text.ToLowerInvariant());
                Assert.Greater(updates, 0);
                using (var cancel = new CancellationTokenSource())
                {
                    var stopped = model.GenerateConversationStreaming(new[]{new MNNChatMessage(MNNChatRole.User, "Explain gravity in detail.")}, update => cancel.Cancel(), 64, cancellationToken: cancel.Token);
                    Assert.IsTrue(stopped.Cancelled);
                    Assert.IsNotEmpty(stopped.Text);
                    Assert.Less(stopped.GeneratedTokens, 64);
                }

                Assert.IsNotEmpty(model.GenerateConversation(new[]{new MNNChatMessage(MNNChatRole.User, "Say hello.")}, 16).Text);
            }
        }

        [Test]
        public void SmolVlm_ImageChangesColorAnswer()
        {
            using (var model = MNNLlm.Load(Model("SmolVLM-256M-Instruct-MNN"), cacheDirectory: _cache))
            {
                foreach (string color in new[]{"red", "blue"})
                {
                    var result = model.GenerateMultimodal("What is the main color in this image? Answer in one word.", imagePath: Fixture(color + ".png"), maxNewTokens: 24);
                    TestContext.WriteLine("SmolVLM " + color + ": " + result.Text);
                    StringAssert.Contains(color, result.Text.ToLowerInvariant());
                    Assert.Greater(result.VisionMicroseconds, 0);
                }
            }
        }

        [Test]
        public void Omni_CombinedInputsAndSpeechCallbackAfterGc()
        {
            using (var model = MNNLlm.Load(Model("Qwen2.5-Omni-3B-MNN"), cacheDirectory: _cache))
            {
                int updates = 0;
                var combined = model.GenerateConversationStreaming(new[]{new MNNChatMessage(MNNChatRole.User, "Name the color of the image, then name the city mentioned in the audio.")}, update => ++updates, imagePath: Fixture("blue.png"), audioPath: Fixture("speech.wav"), maxNewTokens: 64);
                TestContext.WriteLine("Omni combined: " + combined.Text);
                StringAssert.Contains("blue", combined.Text.ToLowerInvariant());
                StringAssert.Contains("paris", combined.Text.ToLowerInvariant());
                Assert.Greater(combined.AudioMicroseconds, 0);
                Assert.Greater(combined.VisionMicroseconds, 0);
                Assert.Greater(updates, 0);
                float[] previous = null, copy = null;
                for (int repeat = 0; repeat < 2; repeat++)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    var speech = model.GenerateConversationStreaming(new[]{new MNNChatMessage(MNNChatRole.User, "Say hello in one short sentence.")}, update => ++updates, maxNewTokens: 32, generateSpeech: true, maxAudioTokens: 192);
                    TestContext.WriteLine("Omni speech: " + speech.Text + "; samples=" + speech.Waveform.Length);
                    Assert.AreEqual(MNNAcceleration.PreferredBackend, model.Backend);
                    Assert.AreEqual(MNNBackendType.CPU, model.MediaBackend);
                    StringAssert.Contains("hello", speech.Text.ToLowerInvariant());
                    Assert.AreEqual(24000, speech.SampleRate);
                    Assert.Greater(speech.Waveform.Length, 2400);
                    double energy = 0;
                    foreach (float sample in speech.Waveform)
                    {
                        Assert.IsFalse(float.IsNaN(sample) || float.IsInfinity(sample));
                        energy += sample * sample;
                    }

                    Assert.Greater(energy / speech.Waveform.Length, 1e-8);
                    string wav = Path.Combine(Environment.GetEnvironmentVariable("MNN_PLAYER_ARTIFACTS"), "omni-hybrid-" + repeat + ".wav");
                    AudioFixture.SaveWave(wav, speech.Waveform, speech.SampleRate);
                    using (var recognizer = MNNLlm.Load(Model("LFM2.5-Audio-1.5B-MNN"), cacheDirectory: Path.Combine(_cache, "Asr"), backendType: MNNBackendType.CPU))
                    {
                        string transcript = recognizer.GenerateMultimodal("Transcribe the spoken sentence in English.", audioPath: wav, maxNewTokens: 96).Text;
                        TestContext.WriteLine("Omni backend=" + model.Backend + "; media=" + model.MediaBackend + "; CPU ASR=" + transcript);
                        StringAssert.Contains("hello", transcript.ToLowerInvariant());
                    }

                    if (previous != null)
                    {
                        Assert.AreNotSame(previous, speech.Waveform);
                        CollectionAssert.AreEqual(copy, previous);
                    }

                    previous = speech.Waveform;
                    copy = (float[])previous.Clone();
                }

                Assert.IsNotEmpty(model.Generate("Say hello.", 32).Text);
            }
        }

        [Test]
        public void Lfm_AudioTranscribesParis()
        {
            using (var model = MNNLlm.Load(Model("LFM2.5-Audio-1.5B-MNN"), cacheDirectory: _cache))
            {
                var result = model.GenerateMultimodal("Transcribe the spoken sentence in English.", audioPath: Fixture("speech.wav"), maxNewTokens: 64);
                TestContext.WriteLine("LFM: " + result.Text);
                StringAssert.Contains("paris", result.Text.ToLowerInvariant());
                Assert.Greater(result.AudioMicroseconds, 0);
            }
        }

        [Test]
        public void Embedding_RetrievesRelevantDocument()
        {
            using (var model = MNNEmbedding.Load(Model("Qwen3-Embedding-0.6B-MNN"), cacheDirectory: _cache))
            {
                var query = model.Encode("Instruct: Given a web search query, retrieve relevant passages that answer the query\nQuery: What is the capital of France?");
                var good = model.Encode("Paris is the capital city of France.");
                var bad = model.Encode("Bananas are yellow tropical fruit.");
                Assert.AreEqual(1024, query.Length);
                double relevant = Cosine(query, good), unrelated = Cosine(query, bad);
                TestContext.WriteLine($"Embedding: relevant={relevant}; unrelated={unrelated}");
                Assert.Greater(relevant, unrelated + 0.05);
            }
        }

        [Test]
        public void Reranker_RanksRelevantDocument()
        {
            using (var model = MNNReranker.Load(Model("Qwen3-Reranker-0.6B-MNN"), cacheDirectory: _cache))
            {
                float good = model.Score("What is the capital of France?", "Paris is the capital of France.");
                float bad = model.Score("What is the capital of France?", "Bananas are yellow tropical fruit.");
                TestContext.WriteLine($"Reranker: relevant={good}; unrelated={bad}");
                Assert.That(good, Is.InRange(0f, 1f));
                Assert.That(bad, Is.InRange(0f, 1f));
                Assert.Greater(good, bad + 0.1f);
            }
        }

        private static double Cosine(float[] a, float[] b)
        {
            Assert.AreEqual(a.Length, b.Length);
            double dot = 0, aa = 0, bb = 0;
            for (int i = 0; i < a.Length; i++)
            {
                dot += a[i] * b[i];
                aa += a[i] * a[i];
                bb += b[i] * b[i];
            }

            Assert.Greater(aa, 0);
            Assert.Greater(bb, 0);
            return dot / Math.Sqrt(aa * bb);
        }
    }
}
