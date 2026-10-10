using System;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [Category("RepresentativeModel")]
    public class StreamingInferenceTests
    {
        [Test]
        public void Model_CreatesTitleUsingOfficialInferenceWithoutChangingChatHistory()
        {
            var chat = MNN.Unity.Editor.MNNStudioConversation.Capture("title", "model", new[]{new MNN.Unity.Editor.MNNStudioMessage(true, "请解释彩虹是怎样形成的。"), new MNN.Unity.Editor.MNNStudioMessage(false, "彩虹来自阳光在水滴中的折射和反射。")}, "", null, null);
            using (var model = MNNLlm.Load(MultimodalTestData.Model("Qwen3.5-0.8B-MNN")))
            {
                var result = model.GenerateConversationStreaming(MNN.Unity.Editor.MNNStudioNaming.Prompt(chat), _ =>
                {
                }, 32);
                string title = MNN.Unity.Editor.MNNStudioNaming.CleanTitle(result.Text);
                Assert.IsNotEmpty(title);
                Assert.LessOrEqual(title.Length, 60);
                StringAssert.Contains("彩虹", title);
                Assert.AreEqual(2, chat.Messages.Count);
                TestContext.WriteLine("Generated title: " + title);
            }
        }

        [Test]
        public void Text_StreamsBeforeCompletionAndMatchesNonStreamingDecode()
        {
            var turns = new[]{new MNNChatMessage(MNNChatRole.User, "Explain what a rainbow is in one short sentence.")};
            using (var model = MNNLlm.Load(MultimodalTestData.Model("Qwen3.5-0.8B-MNN")))
            {
                var expected = model.GenerateConversation(turns, 40);
                var snapshots = new List<MNNGenerationUpdate>();
                bool returned = false;
                var actual = model.GenerateConversationStreaming(turns, update =>
                {
                    Assert.IsFalse(returned);
                    snapshots.Add(update);
                }, 40);
                returned = true;
                Assert.AreEqual(expected.Text, actual.Text);
                Assert.AreEqual(expected.GeneratedTokens, actual.GeneratedTokens);
                Assert.Greater(snapshots.Count, 2);
                for (int i = 0; i < snapshots.Count; ++i)
                {
                    Assert.IsTrue(actual.Text.StartsWith(snapshots[i].Text, StringComparison.Ordinal));
                    if (i > 0)
                        Assert.Greater(snapshots[i].GeneratedTokens, snapshots[i - 1].GeneratedTokens);
                }

                Assert.AreEqual(actual.Text, snapshots[snapshots.Count - 1].Text);
            }
        }

        [Test]
        public void Stop_PreservesPartialReplyAndAllowsNextInference()
        {
            using (var model = MNNLlm.Load(MultimodalTestData.Model("Qwen3.5-0.8B-MNN")))
            using (var cancel = new CancellationTokenSource())
            {
                int count = 0;
                var stopped = model.GenerateConversationStreaming(new[]{new MNNChatMessage(MNNChatRole.User, "Explain gravity in detail.")}, update =>
                {
                    if (++count == 3)
                        cancel.Cancel();
                }, 64, cancellationToken: cancel.Token);
                Assert.IsTrue(stopped.Cancelled);
                Assert.IsNotEmpty(stopped.Text);
                Assert.Less(stopped.GeneratedTokens, 64);
                var next = model.GenerateConversationStreaming(new[]{new MNNChatMessage(MNNChatRole.User, "What is 1 + 1? Answer only the number.")}, _ =>
                {
                }, 24);
                Assert.IsFalse(next.Cancelled);
                StringAssert.Contains("2", next.Text);
            }
        }

        [TestCase("SmolVLM-256M-Instruct-MNN", "red.png", null, "What is the main color in the image? Answer one word.", "red", false)]
        [TestCase("LFM2.5-Audio-1.5B-MNN", null, "speech.wav", "Transcribe the spoken sentence in English.", "paris", false)]
        [TestCase("Qwen2.5-Omni-3B-MNN", "blue.png", "speech.wav", "Name the color in the image and city mentioned in the audio.", "blue", false)]
        [TestCase("Qwen2.5-Omni-3B-MNN", null, null, "Say hello in one short sentence.", "hello", true)]
        public void Media_StreamsActualModelOutput(string name, string image, string audio, string prompt, string word, bool speech)
        {
            using (var model = MNNLlm.Load(MultimodalTestData.Model(name)))
            {
                int snapshots = 0;
                var result = model.GenerateConversationStreaming(new[]{new MNNChatMessage(MNNChatRole.User, prompt)}, update => ++snapshots, 64, image == null ? null : MultimodalTestData.Fixture(image), audio == null ? null : MultimodalTestData.Fixture(audio), speech);
                StringAssert.Contains(word, result.Text.ToLowerInvariant());
                Assert.Greater(snapshots, 0);
                if (image != null)
                    Assert.Greater(result.VisionMicroseconds, 0);
                if (audio != null)
                    Assert.Greater(result.AudioMicroseconds, 0);
                if (speech)
                {
                    Assert.AreEqual(24000, result.SampleRate);
                    Assert.Greater(result.Waveform.Length, 1000);
                }
            }
        }
    }
}
