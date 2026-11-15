using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class HojoSamplerTests
    {
        [Test]
        public void NucleusSelectionMatchesFullSortForSeededDistributions()
        {
            var input = new Random(319);
            foreach (int count in new[] { 1, 31, 17685 })
            foreach (float temperature in new[] { 0f, .8f, 2f })
            {
                var sampler = new MNNHojoSampler(count, 42);
                var expectedRandom = new Random(42);
                var history = new HashSet<int>();
                for (int step = 0; step < 24; ++step)
                {
                    var logits = Enumerable.Range(0, count).Select(_ => (float)(input.NextDouble() * 20 - 10)).ToArray();
                    var original = (float[])logits.Clone();
                    int expected = FullSort(logits, temperature, history, expectedRandom);
                    int actual = sampler.Sample(logits, temperature, history);
                    Assert.AreEqual(expected, actual, "count=" + count + ", step=" + step + ", temperature=" + temperature);
                    CollectionAssert.AreEqual(original, logits, "Selection must not modify native output values.");
                    history.Add(actual);
                }
            }
        }

        [Test]
        public void EqualScoresUseTokenOrderAndKeepTheTopPBoundary()
        {
            var scores = Enumerable.Repeat(1f, 100).ToArray();
            var sampler = new MNNHojoSampler(scores.Length, 42);
            var expected = new Random(42);
            for (int i = 0; i < 100; ++i)
                Assert.AreEqual((int)(expected.NextDouble() * 96), sampler.Sample(scores, .8f, new HashSet<int>()));
        }

        private static int FullSort(float[] logits, float temperature, HashSet<int> history, Random random)
        {
            var scores = (float[])logits.Clone();
            foreach (int id in history) scores[id] = scores[id] > 0 ? scores[id] / 1.1f : scores[id] * 1.1f;
            int best = 0;
            for (int i = 1; i < scores.Length; ++i) if (scores[i] > scores[best]) best = i;
            if (temperature == 0) return best;
            int[] order = Enumerable.Range(0, scores.Length).OrderByDescending(i => scores[i]).ThenBy(i => i).ToArray();
            double[] probabilities = order.Select(i => Math.Exp((scores[i] - scores[best]) / temperature)).ToArray();
            double total = probabilities.Sum(), retained = 0;
            int keep = 0;
            do { retained += probabilities[keep++]; } while (keep < probabilities.Length && retained <= total * .95);
            double draw = random.NextDouble() * retained;
            for (int i = 0; i < keep; ++i) if ((draw -= probabilities[i]) <= 0) return order[i];
            return order[keep - 1];
        }
    }
}
