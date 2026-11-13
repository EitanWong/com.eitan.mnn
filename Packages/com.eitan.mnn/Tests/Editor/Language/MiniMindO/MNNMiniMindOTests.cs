using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class MNNMiniMindOTests
    {
        [Test]
        public void SamplingPreservesSeededReleasedVoiceCodesAndTieOrder()
        {
            var source = new Random(321);
            var logits = Enumerable.Range(0, 8 * 2112).Select(_ => (float)(source.NextDouble() * 20 - 10)).ToArray();
            var random = new Random(42);
            var selected = new int[4096];
            for (int i = 0; i < selected.Length; ++i)
                selected[i] = MNNMiniMindOSampling.Sample(logits, i % 8 * 2112, 2112, .2f, 1, 50, random, new[] { 7, 7, 12 });
            var bytes = new byte[selected.Length * 4];
            Buffer.BlockCopy(selected, 0, bytes, 0, bytes.Length);
            using (var sha = System.Security.Cryptography.SHA256.Create())
                Assert.AreEqual("f0daf87c4131121d56bb65a70263eb13a1803b0106bd453cafbdf7817fac3030",
                    BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant());
            for (int i = 0; i < 100; ++i)
            {
                Assert.AreEqual(0, MNNMiniMindOSampling.Sample(new float[2112], 0, 2112, .2f, 1, 1, random));
                Assert.AreEqual(0, MNNMiniMindOSampling.Sample(new[] { 1f, 1f, 0f }, 0, 3, 0, 1, 0, random));
                Assert.That(MNNMiniMindOSampling.Sample(new float[2112], 0, 2112, .2f, 1, 3, random), Is.InRange(0, 2));
            }
        }

        [Test]
        public void SamplingPreservesSoftmaxProbabilitiesAndNucleusBoundary()
        {
            var random = new Random(42);
            int first = 0;
            for (int i = 0; i < 4000; ++i)
                if (MNNMiniMindOSampling.Sample(new[] { 10f, 9f }, 0, 2, 1, 1, 0, random) == 0) ++first;
            Assert.That(first / 4000.0, Is.InRange(0.70, 0.76));
            for (int i = 0; i < 100; ++i)
                Assert.AreEqual(0, MNNMiniMindOSampling.Sample(new[] { 10f, 9f }, 0, 2, 1, .5f, 0, random));
        }

        [Test]
        public void AudioScheduleAlignsEightDelayedCodebooksAndStopsBeforeSpecialCodes()
        {
            var codes = Enumerable.Range(0, 8).Select(i => Enumerable.Range(0, 20).Select(t => i * 100 + t).ToList()).ToArray();
            var stops = Enumerable.Repeat(-1, 8).ToArray();
            Assert.IsNull(MNNMiniMindO.AlignedFrame(codes, stops, 7));
            CollectionAssert.AreEqual(new[] { 1, 102, 203, 304, 405, 506, 607, 708 }, MNNMiniMindO.AlignedFrame(codes, stops, 8));
            stops[4] = 5;
            Assert.IsNull(MNNMiniMindO.AlignedFrame(codes, stops, 8));
        }

        [Test]
        public void PromptPreservesReleasedHistoryAndClosedThinkingTemplate()
        {
            var history = new[] { new MNNChatMessage(MNNChatRole.System, "简短回答。"),
                new MNNChatMessage(MNNChatRole.User, "你好"), new MNNChatMessage(MNNChatRole.Assistant, "你好！") };
            Assert.AreEqual("<|im_start|>system\n简短回答。<|im_end|>\n<|im_start|>user\n你好<|im_end|>\n" +
                "<|im_start|>assistant\n<think>\n\n</think>\n\n你好！<|im_end|>\n" +
                "<|im_start|>user\n你是谁？<|im_end|>\n<|im_start|>assistant\n<think>\n\n</think>\n\n",
                MNNMiniMindOTokenizer.Prompt("你是谁？", history));
        }

        [Test]
        public void InvalidGenerationOptionsFailBeforeInference()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MNNMiniMindOOptions { MaxNewTokens = 0 }.Snapshot());
            Assert.Throws<ArgumentOutOfRangeException>(() => new MNNMiniMindOOptions { Temperature = float.NaN }.Snapshot());
            Assert.Throws<ArgumentOutOfRangeException>(() => new MNNMiniMindOOptions { TopP = 0 }.Snapshot());
            Assert.False(MNNMiniMindO.IsComplete(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MNNGenerationGraph("missing", 4, memoryMode: (MNNMemoryMode)999));
        }
    }
}
