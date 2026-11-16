using System;
using System.Linq;
using System.Threading;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class SpeechTimingTests
    {
        private const int Rate = 24000;

        [TestCase(.75f)]
        [TestCase(1.15f)]
        [TestCase(1.5f)]
        [TestCase(2.3f)]
        public void TempoChangesDurationWhilePreservingPitchAndEnding(float speed)
        {
            var input = Tone(Rate, 220);
            // A different final tone detects accidental truncation of the end of speech.
            Array.Copy(Tone(Rate / 5, 440), 0, input, Rate * 4 / 5, Rate / 5);
            var output = MNNSpeechDsp.ChangeTempo(input, Rate, speed, default);
            Assert.AreEqual((int)Math.Round(input.Length / (double)speed), output.Length);
            Assert.AreEqual(220, Frequency(output, Rate / 10, Math.Min(output.Length / 2, Rate / 2)), 6);
            Assert.AreEqual(440, Frequency(output, output.Length - Rate / 20, output.Length - 1), 15);
            Assert.True(output.All(value => !float.IsNaN(value) && !float.IsInfinity(value) && Math.Abs(value) <= .301f));
        }

        [Test]
        public void UnityTempoPreservesEverySample()
        {
            var input = Tone(Rate, 173);
            CollectionAssert.AreEqual(input, MNNSpeechDsp.ChangeTempo(input, Rate, 1, default));
        }

        [TestCase(8, .5f)]
        [TestCase(8, 2.3f)]
        [TestCase(200, 1.15f)]
        public void ShortBuffersRetainTheirEndpoints(int length, float speed)
        {
            var input = Tone(length, 220);
            var output = MNNSpeechDsp.ChangeTempo(input, Rate, speed, default);
            Assert.AreEqual((int)Math.Round(input.Length / (double)speed), output.Length);
            Assert.AreEqual(input[0], output[0]);
            Assert.AreEqual(input[input.Length - 1], output[output.Length - 1]);
        }

        [Test]
        public void EdgeTrimmingPreservesQuietConsonantsAndInternalPauses()
        {
            var input = new float[Rate * 2];
            var speech = Tone(Rate / 5, 220);
            Array.Copy(speech, 0, input, Rate / 2, speech.Length);
            Array.Copy(speech, 0, input, Rate * 13 / 10, speech.Length);
            input[Rate * 16 / 10] = .004f;
            var output = MNNSpeechDsp.TrimSpeechEdges(input, Rate, default);
            Assert.AreEqual(Rate * 121 / 100, output.Length);
            CollectionAssert.AreEqual(input.Skip(Rate * 48 / 100).Take(output.Length), output);
        }

        [Test]
        public void QuietSpeechAndSilenceAreNotDeleted()
        {
            var quiet = Tone(Rate / 2, 220).Select(x => x * .01f).ToArray();
            CollectionAssert.AreEqual(quiet, MNNSpeechDsp.TrimSpeechEdges(quiet, Rate, default));
            var silence = new float[Rate / 10];
            CollectionAssert.AreEqual(silence, MNNSpeechDsp.TrimSpeechEdges(silence, Rate, default));
        }

        [Test]
        public void TimingOperationsHonorCancellation()
        {
            var token = new CancellationToken(true);
            Assert.Throws<OperationCanceledException>(() => MNNSpeechDsp.ChangeTempo(Tone(Rate, 220), Rate, 1.15f, token));
            Assert.Throws<OperationCanceledException>(() => MNNSpeechDsp.TrimSpeechEdges(Tone(Rate, 220), Rate, token));
        }

        private static float[] Tone(int length, double frequency)
            => Enumerable.Range(0, length).Select(i => (float)(.3 * Math.Sin(2 * Math.PI * frequency * i / Rate))).ToArray();

        private static double Frequency(float[] samples, int first, int last)
        {
            int crossings = 0;
            double firstCrossing = 0, lastCrossing = 0;
            for (int i = first + 1; i < last; ++i)
            {
                if (samples[i - 1] > 0 || samples[i] <= 0) continue;
                double crossing = i - 1 - samples[i - 1] / (double)(samples[i] - samples[i - 1]);
                if (crossings++ == 0) firstCrossing = crossing;
                lastCrossing = crossing;
            }
            Assert.Greater(crossings, 1);
            return (crossings - 1) * Rate / (lastCrossing - firstCrossing);
        }
    }
}
