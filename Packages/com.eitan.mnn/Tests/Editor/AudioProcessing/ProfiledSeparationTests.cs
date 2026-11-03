using System;
using System.Linq;
using System.Threading;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class ProfiledSeparationTests
    {
        [TestCase(16000, 160003)]
        [TestCase(8000, 24001)]
        [TestCase(16000, 1)]
        public void BoundedWindowsKeepSourceSlotsWhenRunnerReversesThem(int rate, int length)
        {
            int calls = 0;
            var input = Enumerable.Range(0, length).Select(i => .2f * (float)Math.Sin(i * .17)).ToArray();
            using (var model = new MNNSpeechSeparator(null, (block, sampleRate, token) =>
            {
                Assert.That(block.Length, Is.InRange(rate / 10, rate * 3));
                var positive = block.Select(value => value + 1).ToArray();
                var negative = block.Select(value => value - 1).ToArray();
                return ++calls % 2 == 0 ? new MNNSeparatedAudio(negative, positive, sampleRate) :
                    new MNNSeparatedAudio(positive, negative, sampleRate);
            }))
            {
                var result = model.Separate(input, rate);
                Assert.That(result.SampleRate, Is.EqualTo(rate));
                Assert.That(result.Speaker0.Length, Is.EqualTo(length));
                Assert.That(result.Speaker1.Length, Is.EqualTo(length));
                for (int i = 0; i < length; ++i)
                {
                    Assert.That(result.Speaker0[i], Is.EqualTo(input[i] + 1).Within(.00001));
                    Assert.That(result.Speaker1[i], Is.EqualTo(input[i] - 1).Within(.00001));
                }
                Assert.That(calls, length > rate * 3 ? Is.GreaterThan(1) : Is.EqualTo(1));
            }
        }
        [Test]
        public void CancellationStopsBeforeStartingAnotherWindow()
        {
            using (var cancellation = new CancellationTokenSource())
            using (var model = new MNNSpeechSeparator(null, (block, rate, token) =>
            {
                cancellation.Cancel();
                return new MNNSeparatedAudio(block, block, rate);
            }))
                Assert.Throws<OperationCanceledException>(() => model.Separate(new float[160000], 16000, cancellation.Token));
        }
    }
}
