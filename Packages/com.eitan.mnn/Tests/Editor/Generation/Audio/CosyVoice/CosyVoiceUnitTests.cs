using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public class CosyVoiceUnitTests
    {
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase("！！！")]
        [TestCase("😀")]
        [TestCase("<|eos1|>")]
        [TestCase("hello\nworld")]
        [TestCase("hello\0world")]
        public void InvalidTextFailsBeforeNativeExecution(string text) => Assert.Throws<ArgumentException>(() => MNNCosyVoice.ValidateText(text));
        [Test]
        public void TextLimitAndMissingDirectoryAreValidated()
        {
            MNNCosyVoice.ValidateText("你好，欢迎使用语音合成。");
            MNNCosyVoice.ValidateText(new string ('a', 200));
            Assert.Throws<ArgumentException>(() => MNNCosyVoice.ValidateText(new string ('a', 201)));
            Assert.Throws<ArgumentNullException>(() => MNNCosyVoice.Load(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => MNNCosyVoice.Load("missing", 0));
            Assert.Throws<InvalidDataException>(() => MNNCosyVoice.Load("missing"));
            Assert.IsFalse(MNNCosyVoiceModel.IsComplete(null));
        }

        private static float[] Logits()
        {
            var logits = Enumerable.Repeat(-1000f, 158720).ToArray();
            logits[MNNCosyVoiceSampler.Offset] = 10;
            logits[MNNCosyVoiceSampler.Offset + 1] = 9;
            logits[MNNCosyVoiceSampler.Eos] = 100;
            return logits;
        }

        [TestCase("  Hello world  ", "Hello world.")]
        [TestCase("你好", "你好。")]
        [TestCase("Hello!", "Hello!")]
        [TestCase("\"Hello\"", "\"Hello.\"")]
        [TestCase("\"Hello!\"", "\"Hello!\"")]
        [TestCase("the doer first,", "the doer first.")]
        public void TextFrontendSuppliesMissingSentenceBoundary(string text, string expected) => Assert.AreEqual(expected, MNNCosyVoice.NormalizeEnding(text));

        [TestCase("Every standard statement in English places the doer first, the action second, and the receiver third")]
        [TestCase("今天我们先去图书馆看书，然后去公园散步，最后回家准备晚饭。明天早上还要按时出发，请记得带好雨伞。")]
        [TestCase("The measurement is 123,456.78 meters. Please read the final sentence and finish with the words receiver third.")]
        public void LongTextSegmentsPreserveAllWordsAndSentenceOrder(string text)
        {
            var chunks = MNNCosyVoice.SplitText(text);
            Assert.Greater(chunks.Length, 1);
            Assert.IsTrue(chunks.All(chunk => !string.IsNullOrWhiteSpace(chunk)));
            string original = new string(text.Where(c => !char.IsWhiteSpace(c)).ToArray());
            string joined = new string(string.Concat(chunks).Where(c => !char.IsWhiteSpace(c)).ToArray());
            Assert.AreEqual(original, joined, "No text may be lost at chunk boundaries.");
            if (text.Contains("123,456.78"))
                Assert.IsTrue(chunks.Any(chunk => chunk.Contains("123,456.78")), "Do not split numbers.");
            if (text.Contains("receiver third"))
                StringAssert.Contains("receiver third", chunks.Last());
        }

        [Test]
        public void SegmentationPreservesUnbrokenWordsAndUnicodeTextElements()
        {
            string word = new string('a', 200);
            CollectionAssert.AreEqual(new[]{word}, MNNCosyVoice.SplitText(word));
            string combined = string.Concat(Enumerable.Repeat("e\u0301", 100));
            CollectionAssert.AreEqual(new[]{combined}, MNNCosyVoice.SplitText(combined));
            string cjk = new string('好', 200);
            var chunks = MNNCosyVoice.SplitText(cjk);
            Assert.Greater(chunks.Length, 1);
            Assert.AreEqual(cjk, string.Concat(chunks));
        }

        [Test]
        public void FftVocoderMatchesIndependentDirectTransforms()
        {
            var random = new Random(731);
            var source = Enumerable.Range(0, 960).Select(_ => (float)(random.NextDouble() * 2 - 1)).ToArray();
            var actual = MNNCosyVoiceVocoder.Stft(source, default);
            int frames = source.Length / 4 + 1;
            for (int f = 0; f < frames; ++f)
            for (int b = 0; b < 9; ++b)
            {
                double re = 0, im = 0;
                for (int n = 0; n < 16; ++n)
                {
                    int index = f * 4 - 8 + n;
                    if (index < 0) index = -index;
                    if (index >= source.Length) index = 2 * source.Length - 2 - index;
                    float value = source[index] * (float)(.5 - .5 * Math.Cos(2 * Math.PI * n / 16));
                    re += value * Math.Cos(2 * Math.PI * b * n / 16);
                    im -= value * Math.Sin(2 * Math.PI * b * n / 16);
                }
                Assert.AreEqual(re, actual[b * frames + f], 1e-6);
                Assert.AreEqual(im, actual[(b + 9) * frames + f], 1e-6);
            }

            var spectrum = Enumerable.Range(0, 18 * frames).Select(_ => (float)(random.NextDouble() * 8 - 4)).ToArray();
            var pcm = MNNCosyVoiceVocoder.Istft(spectrum, frames, default);
            var sum = new double[pcm.Length + 16];
            var envelope = new double[sum.Length];
            for (int f = 0; f < frames; ++f)
            for (int n = 0; n < 16; ++n)
            {
                double value = 0;
                for (int b = 0; b < 9; ++b)
                {
                    double magnitude = Math.Min(100, Math.Exp(spectrum[b * frames + f]));
                    double phase = Math.Sin(spectrum[(b + 9) * frames + f]);
                    double angle = 2 * Math.PI * b * n / 16;
                    value += (b == 0 || b == 8 ? 1 : 2) * magnitude * Math.Cos(phase + angle);
                }
                float window = (float)(.5 - .5 * Math.Cos(2 * Math.PI * n / 16));
                sum[f * 4 + n] += value / 16 * window;
                envelope[f * 4 + n] += (double)window * window;
            }
            for (int i = 0; i < pcm.Length; ++i)
            {
                int j = i + 8;
                double expected = Math.Max(-.99, Math.Min(.99, envelope[j] > 1e-11 ? sum[j] / envelope[j] : sum[j]));
                Assert.AreEqual(expected, pcm[i], 1e-6);
            }
        }

        [Test]
        public void SamplerMasksTextAndEarlyEosAndUsesStableSoftmax()
        {
            var logits = Logits();
            logits[0] = 10000;
            logits[MNNCosyVoiceSampler.Offset] = 1000;
            for (int seed = 0; seed < 20; ++seed)
                Assert.AreEqual(MNNCosyVoiceSampler.Offset, MNNCosyVoiceSampler.Sample(logits, new List<int>(), 2, new Random(seed)));
            logits[MNNCosyVoiceSampler.Offset] = 10;
            Assert.AreEqual(MNNCosyVoiceSampler.Eos, MNNCosyVoiceSampler.Sample(logits, new[]{MNNCosyVoiceSampler.Offset, MNNCosyVoiceSampler.Offset + 1}, 2, new Random(42)));
        }

        [Test]
        public void RasResamplesRepeatedTokenFromFullRange()
        {
            var logits = Logits();
            logits[MNNCosyVoiceSampler.Offset] = 100;
            // Top-k/top-p select only the dominant token, then RAS excludes it.
            Assert.AreEqual(MNNCosyVoiceSampler.Offset + 1, MNNCosyVoiceSampler.Sample(logits, new[]{MNNCosyVoiceSampler.Offset}, 2, new Random(42)));
        }

        [Test]
        public void RasHistoryWindowDoesNotPenalizeOldTokens()
        {
            var logits = Logits();
            logits[MNNCosyVoiceSampler.Offset] = 100;
            var history = new[]{MNNCosyVoiceSampler.Offset}.Concat(Enumerable.Repeat(MNNCosyVoiceSampler.Offset + 2, 10)).ToArray();
            Assert.AreEqual(MNNCosyVoiceSampler.Offset, MNNCosyVoiceSampler.Sample(logits, history, 100, new Random(42)));
        }

        [Test]
        public void SamplerRejectsWrongShapeAndNonFiniteLogits()
        {
            Assert.Throws<InvalidDataException>(() => MNNCosyVoiceSampler.Sample(new float[10], new int[0], 0, new Random()));
            var logits = Logits();
            logits[MNNCosyVoiceSampler.Offset] = float.NaN;
            Assert.Throws<InvalidDataException>(() => MNNCosyVoiceSampler.Sample(logits, new int[0], 0, new Random()));
        }

        [Test]
        public void OptimizedSamplerMatchesStableSortForFullAndCompactLogits()
        {
            var random = new Random(907);
            var workspace = new MNNCosyVoiceSampler.Workspace();
            for (int scenario = 0; scenario < 80; ++scenario)
            {
                var full = new float[MNNCosyVoiceSampler.Vocabulary * 2];
                int row = MNNCosyVoiceSampler.Vocabulary;
                for (int i = MNNCosyVoiceSampler.Offset; i <= MNNCosyVoiceSampler.Eos; ++i)
                    full[row + i] = scenario % 2 == 0 ? random.Next(12) : (float)(random.NextDouble() * 30 - 15);
                var history = Enumerable.Range(0, scenario % 16).Select(_ => MNNCosyVoiceSampler.Offset + random.Next(MNNCosyVoiceSampler.Count)).ToList();
                if (scenario % 3 == 0)
                {
                    // Force the RAS full-range fallback, including tied logits.
                    int dominant = MNNCosyVoiceSampler.Offset + random.Next(MNNCosyVoiceSampler.Count);
                    full[row + dominant] = 1000;
                    history.Add(dominant);
                }
                var compact = full.Skip(row + MNNCosyVoiceSampler.Offset).Take(MNNCosyVoiceSampler.CompactSize).ToArray();
                for (int seed = 0; seed < 8; ++seed)
                {
                    int minimum = scenario % 2 == 0 ? 0 : 50;
                    int expected = ReferenceSample(full, history, minimum, new Random(seed));
                    Assert.AreEqual(expected, MNNCosyVoiceSampler.Sample(full, history, minimum, new Random(seed), workspace));
                    Assert.AreEqual(expected, MNNCosyVoiceSampler.Sample(compact, history, minimum, new Random(seed), workspace));
                }
            }
        }

        // Frozen pre-optimization sampler: an independent full stable sort and
        // allocated candidate lists guard probabilities, ties and RNG order.
        private static int ReferenceSample(float[] logits, IList<int> history, int minimum, Random random)
        {
            int start = logits.Length - MNNCosyVoiceSampler.Vocabulary;
            var allowed = Enumerable.Range(MNNCosyVoiceSampler.Offset, MNNCosyVoiceSampler.Count).ToList();
            if (history.Count >= minimum)
                allowed.Add(MNNCosyVoiceSampler.Eos);
            var top = allowed.OrderByDescending(id => logits[start + id]).Take(25).ToArray();
            double max = logits[start + top[0]], sum = top.Sum(id => Math.Exp(logits[start + id] - max));
            double cumulative = 0;
            int keep = 0;
            do { cumulative += Math.Exp(logits[start + top[keep++]] - max) / sum; }
            while (keep < top.Length && cumulative < .8);
            int selected = ReferenceChoose(top.Take(keep).ToArray(), logits, start, random);
            for (int i = Math.Max(0, history.Count - 10); i < history.Count; ++i)
                if (history[i] == selected)
                    return ReferenceChoose(allowed.Where(id => id != selected).ToArray(), logits, start, random);
            return selected;
        }

        private static int ReferenceChoose(int[] ids, float[] logits, int start, Random random)
        {
            double max = ids.Max(id => logits[start + id]);
            double threshold = random.NextDouble() * ids.Sum(id => Math.Exp(logits[start + id] - max));
            foreach (int id in ids)
            {
                threshold -= Math.Exp(logits[start + id] - max);
                if (threshold <= 0)
                    return id;
            }
            return ids[ids.Length - 1];
        }

        [Test]
        public void VocoderRejectsInvalidShapesAndCancellation()
        {
            Assert.Throws<InvalidDataException>(() => MNNCosyVoiceVocoder.Stft(new float[4], default));
            Assert.Throws<InvalidDataException>(() => MNNCosyVoiceVocoder.Istft(new float[18], 2, default));
            Assert.Throws<InvalidDataException>(() => MNNCosyVoiceVocoder.Source(new float[0], new float[9], 0, default));
            var bad = new float[36];
            bad[0] = float.NaN;
            Assert.Throws<InvalidDataException>(() => MNNCosyVoiceVocoder.Istft(bad, 2, default));
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                Assert.Throws<OperationCanceledException>(() => MNNCosyVoiceVocoder.Stft(new float[480], cancellation.Token));
                Assert.Throws<OperationCanceledException>(() => MNNCosyVoiceVocoder.Istft(new float[36], 2, cancellation.Token));
            }
        }

        [Test]
        public void ConstantSignalStftHasKnownDcAndHannBins()
        {
            var result = MNNCosyVoiceVocoder.Stft(Enumerable.Repeat(.5f, 480).ToArray(), default);
            int frames = 121;
            for (int frame = 0; frame < frames; ++frame)
            {
                Assert.AreEqual(4, result[frame], 1e-5);
                Assert.AreEqual(-2, result[frames + frame], 1e-5);
                for (int bin = 2; bin < 9; ++bin)
                    Assert.AreEqual(0, result[bin * frames + frame], 1e-5);
                for (int bin = 9; bin < 18; ++bin)
                    Assert.AreEqual(0, result[bin * frames + frame], 1e-5);
            }
        }

        [Test]
        public void SourceGenerationIsRepeatableAndBounded()
        {
            var first = MNNCosyVoiceVocoder.Source(new[]{0f, 120f}, Enumerable.Repeat(1f, 9).ToArray(), 0, default);
            CollectionAssert.AreEqual(first, MNNCosyVoiceVocoder.Source(new[]{0f, 120f}, Enumerable.Repeat(1f, 9).ToArray(), 0, default));
            Assert.AreEqual(960, first.Length);
            Assert.IsTrue(first.All(value => !float.IsNaN(value) && Math.Abs(value) <= 1));
        }
    }
}
