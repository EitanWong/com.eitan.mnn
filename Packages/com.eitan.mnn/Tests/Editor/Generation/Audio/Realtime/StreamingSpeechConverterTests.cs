using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class StreamingSpeechConverterTests
    {
        [TestCase(16000, 16000, 1)]
        [TestCase(24000, 48000, 2)]
        [TestCase(44100, 24000, 1)]
        [TestCase(48000, 16000, 2)]
        public void LongQuietTailIsTrimmedAcrossBorrowedChunksWithOneFinalFrame(int inputRate, int outputRate, int channels)
        {
            Run(async () =>
            {
                var input = Join(Tone(inputRate / 5, inputRate), Quiet(inputRate / 2));
                var output = new List<float>();
                int finals = 0;
                var converter = Create(inputRate, outputRate, channels, output, () => ++finals);
                await AppendChunks(converter, input);
                Assert.Greater(output.Count, 0, "Speech must stream before the segment completes.");
                Assert.AreEqual(0, finals);
                await converter.CompleteAsync();
                Assert.AreEqual(1, finals);
                Assert.That(output.Count / channels, Is.InRange((int)(outputRate * .28) - 2, (int)(outputRate * .29) + 2));
                for (int i = 0; i < output.Count; i += channels)
                    for (int c = 1; c < channels; ++c) Assert.AreEqual(output[i], output[i + c]);
            });
        }

        [TestCase(0)]
        [TestCase(40)]
        [TestCase(150)]
        public void ModelsWithoutLongTailsKeepTheirPcm(int tailMilliseconds)
        {
            Run(async () =>
            {
                const int rate = 24000;
                var input = Join(Tone(rate / 5, rate), Quiet(rate * tailMilliseconds / 1000));
                var output = new List<float>();
                var converter = Create(rate, rate, 1, output);
                await AppendChunks(converter, input);
                await converter.CompleteAsync();
                CollectionAssert.AreEqual(input, output);
            });
        }

        [Test]
        public void LongInternalPauseAndLeadingSilenceArePreservedExactly()
        {
            Run(async () =>
            {
                const int rate = 16000;
                var prefix = Join(Quiet(rate / 3), Tone(rate / 5, rate), Quiet(rate * 2), Tone(rate / 5, rate));
                var output = new List<float>();
                var converter = Create(rate, rate, 1, output);
                await AppendChunks(converter, Join(prefix, Quiet(rate / 2)));
                await converter.CompleteAsync();
                CollectionAssert.AreEqual(prefix, output.GetRange(0, prefix.Length));
                Assert.That(output.Count, Is.InRange(prefix.Length + rate * 80 / 1000, prefix.Length + rate * 90 / 1000));
            });
        }

        [Test]
        public void SoftEndingAndBriefConsonantPeakAreKeptBeforeTrimming()
        {
            Run(async () =>
            {
                const int rate = 16000;
                var soft = Tone(rate / 10, rate, .002f);
                var consonant = Quiet(rate / 100);
                consonant[consonant.Length / 2] = .004f;
                var prefix = Join(Tone(rate / 5, rate), soft, Quiet(rate / 5), consonant);
                var output = new List<float>();
                var converter = Create(rate, rate, 1, output);
                await AppendChunks(converter, Join(prefix, Quiet(rate / 2)));
                await converter.CompleteAsync();
                CollectionAssert.AreEqual(prefix, output.GetRange(0, prefix.Length));
                Assert.AreEqual(prefix.Length + rate * 80 / 1000, output.Count);
            });
        }

        [TestCase(0f)]
        [TestCase(.0001f)]
        public void EntirelySilentSegmentStillEndsOnceWithoutALongPause(float level)
        {
            Run(async () =>
            {
                const int rate = 24000;
                var input = new float[rate * 3];
                for (int i = 0; i < input.Length; ++i) input[i] = level;
                var output = new List<float>();
                int finals = 0;
                var converter = Create(rate, rate, 1, output, () => ++finals);
                await AppendChunks(converter, input);
                await converter.CompleteAsync();
                Assert.AreEqual(rate * 80 / 1000, output.Count);
                Assert.AreEqual(1, finals);
            });
        }

        [Test]
        public void TrimmingIsOptInForExistingConverterUsers()
        {
            Run(async () =>
            {
                const int rate = 16000;
                var input = Join(Tone(rate / 5, rate), Quiet(rate));
                var output = new List<float>();
                var converter = new MNNStreamingSpeechConverter(rate, new MNNPcmFormat(rate, 1), (pcm, offset, count, final) =>
                {
                    for (int i = offset; i < offset + count; ++i) output.Add(pcm[i]);
                    return Task.CompletedTask;
                });
                await AppendChunks(converter, input);
                await converter.CompleteAsync();
                CollectionAssert.AreEqual(input, output);
            });
        }

        private static MNNStreamingSpeechConverter Create(int inputRate, int outputRate, int channels,
            List<float> output, Action onFinal = null)
        {
            return new MNNStreamingSpeechConverter(inputRate, new MNNPcmFormat(outputRate, channels), (pcm, offset, count, final) =>
            {
                Assert.Greater(count, 0);
                if (final)
                {
                    Assert.AreEqual(channels, count, "Only the retained final frame marks segment completion.");
                    onFinal?.Invoke();
                }
                for (int i = offset; i < offset + count; ++i) output.Add(pcm[i]);
                return Task.CompletedTask;
            }, trimTrailingSilence: true);
        }

        private static async Task AppendChunks(MNNStreamingSpeechConverter converter, float[] input)
        {
            var borrowed = new float[677]; // Deliberately crosses detector and resampler window boundaries.
            int offset = 0;
            while (offset < input.Length)
            {
                int count = Math.Min(borrowed.Length, input.Length - offset);
                Array.Copy(input, offset, borrowed, 0, count);
                await converter.AppendAsync(borrowed, count);
                Array.Clear(borrowed, 0, borrowed.Length);
                offset += count;
            }
        }

        private static float[] Tone(int count, int rate, float amplitude = .2f)
        {
            var result = new float[count];
            for (int i = 0; i < count; ++i) result[i] = amplitude * (float)Math.Sin(2 * Math.PI * 220 * i / rate);
            return result;
        }

        private static float[] Quiet(int count)
        {
            var result = new float[count];
            for (int i = 0; i < count; ++i) result[i] = i % 2 == 0 ? .0001f : -.0001f;
            return result;
        }

        private static float[] Join(params float[][] parts)
        {
            var result = new List<float>();
            foreach (var part in parts) result.AddRange(part);
            return result.ToArray();
        }

        private static void Run(Func<Task> test)
        {
            var task = Task.Run(test);
            Assert.AreSame(task, Task.WhenAny(task, Task.Delay(5000)).GetAwaiter().GetResult());
            task.GetAwaiter().GetResult();
        }
    }
}
