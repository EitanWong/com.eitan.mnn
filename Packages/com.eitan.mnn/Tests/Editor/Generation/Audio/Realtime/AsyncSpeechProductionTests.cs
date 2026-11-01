using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class AsyncSpeechProductionTests
    {
        [TestCase(16000, 24000)]
        [TestCase(24000, 48000)]
        [TestCase(24000, 16000)]
        public void ConverterPublishesBeforeCompletionWithOneFinalFrameAndPreservesStereo(int inputRate, int outputRate)
        {
            Run(async () =>
            {
                int frames = 0, finals = 0;
                var converter = new MNNStreamingSpeechConverter(inputRate, new MNNPcmFormat(outputRate, 2), (pcm, offset, count, final) =>
                {
                    Assert.AreEqual(0, count % 2);
                    for (int i = offset; i < offset + count; i += 2) Assert.AreEqual(pcm[i], pcm[i + 1]);
                    frames += count / 2;
                    if (final) { ++finals; Assert.AreEqual(2, count); }
                    return Task.CompletedTask;
                });
                var block = new float[inputRate / 50];
                for (int i = 0; i < block.Length; ++i) block[i] = .2f;
                for (int i = 0; i < 10; ++i) await converter.AppendAsync(block, block.Length);
                Assert.Greater(frames, 0, "Audio must be published while synthesis is active.");
                Assert.AreEqual(0, finals);
                await converter.CompleteAsync();
                Assert.AreEqual(1, finals);
                Assert.That(frames, Is.InRange(outputRate / 5 - 1, outputRate / 5 + 1));
            });
        }
        [Test]
        public void AsyncProducerStopsWhileAwaitingItsFirstTextSegment()
        {
            Run(async () =>
            {
                var output = new MNNRealtimeOutputPipeline(new MNNPcmFormat(16000, 1), 160);
                var response = output.BeginResponse();
                var channel = new MNNRealtimeSpeechTextChannel(output, response);
                var stream = new MNNRealtimeSpeechStream(output, response);
                using var cleanup = new CancellationTokenSource();
                int synthesized = 0;
                var production = stream.ProduceAsync(channel, output.Format, (_, publish, token) =>
                {
                    ++synthesized;
                    return publish(new float[160], 0, 160, true);
                }, cleanup.Token);
                try
                {
                    Assert.False(production.IsCompleted);
                    output.Interrupt();
                    var next = output.BeginResponse();
                    Assert.AreSame(production, await Task.WhenAny(production, Task.Delay(3000)));
                    Assert.False(await production);
                    Assert.AreEqual(0, synthesized);
                    Assert.True(output.Accepts(next));
                    Assert.False(output.CanBeginNextResponse(next));
                }
                finally
                {
                    cleanup.Cancel();
                    try { await production; }
                    catch (OperationCanceledException) { }
                }
            });
        }

        [Test]
        public void AsyncProducerStopsWhileAwaitingTextAfterPlayingASegment()
        {
            Run(async () =>
            {
                var output = new MNNRealtimeOutputPipeline(new MNNPcmFormat(16000, 1), 160);
                var response = output.BeginResponse();
                var channel = new MNNRealtimeSpeechTextChannel(output, response);
                Assert.True(channel.Append("First. "));
                var stream = new MNNRealtimeSpeechStream(output, response);
                using var cleanup = new CancellationTokenSource();
                var production = stream.ProduceAsync(channel, output.Format, (_, publish, token) =>
                    publish(new float[160], 0, 160, true), cleanup.Token);
                try
                {
                    Assert.AreEqual(160, output.QueuedFrames);
                    Assert.False(production.IsCompleted);
                    output.Interrupt();
                    var next = output.BeginResponse();
                    Assert.AreSame(production, await Task.WhenAny(production, Task.Delay(3000)));
                    Assert.False(await production);
                    Assert.True(output.Accepts(next));
                    Assert.False(output.CanBeginNextResponse(next));
                    var rendered = new float[160];
                    output.Render(rendered, 0, rendered.Length);
                    Assert.That(rendered, Is.All.Zero);
                    Assert.AreEqual(0, output.QueuedFrames);
                }
                finally
                {
                    cleanup.Cancel();
                    try { await production; }
                    catch (OperationCanceledException) { }
                }
            });
        }

        [Test]
        public void AsyncProducerCanBeInterruptedWhilePlaybackIsBackpressured()
        {
            Run(async () =>
            {
                var output = new MNNRealtimeOutputPipeline(new MNNPcmFormat(16000, 1), 160, 1);
                var response = output.BeginResponse();
                var channel = new MNNRealtimeSpeechTextChannel(output, response);
                Assert.True(channel.Append("Hello.")); Assert.True(channel.Complete());
                var stream = new MNNRealtimeSpeechStream(output, response);
                var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var production = stream.ProduceAsync(channel, output.Format, async (_, publish, token) =>
                {
                    entered.TrySetResult(true);
                    await publish(new float[1600], 0, 1600, true);
                });
                await entered.Task;
                Assert.False(production.IsCompleted);
                output.Interrupt();
                Assert.False(await production);
                var rendered = new float[160];
                output.Render(rendered, 0, rendered.Length);
                Assert.That(rendered, Is.All.EqualTo(0), "The render owner discards invalidated PCM without a cross-thread queue clear.");
                Assert.AreEqual(0, output.QueuedFrames);
            });
        }

        [Test]
        public void AsyncBackpressureRejectsRegressingClockWithoutPublishingBlockedPcm()
        {
            Run(async () =>
            {
                var format = new MNNPcmFormat(16000, 1);
                var output = new MNNRealtimeOutputPipeline(format, 160, blockCapacity: 1);
                var response = output.BeginResponse();
                Assert.True(output.TryRegisterSegment(response, 99, "Queued."));
                Assert.True(output.TryEnqueue(new float[160], 0, 160,
                    new MNNSpeechChunkInfo(response, 99, 0, true)));

                var channel = new MNNRealtimeSpeechTextChannel(output, response);
                Assert.True(channel.Append("New speech."));
                Assert.True(channel.Complete());
                var timestamps = new Queue<long>(new[] { 10L, 9L });
                var stream = new MNNRealtimeSpeechStream(output, response, TimeSpan.FromSeconds(1), null,
                    () => timestamps.Dequeue(), null);

                Exception failure = null;
                try
                {
                    await stream.ProduceAsync(channel, format, async (_, publish, _) =>
                        await publish(new float[160], 0, 160, true));
                }
                catch (Exception error) { failure = error; }

                Assert.IsInstanceOf<InvalidOperationException>(failure);
                Assert.That(failure.Message, Does.Contain("clock moved backwards"));
                Assert.AreEqual(160, output.QueuedFrames, "Backpressured PCM must not overwrite queued audio.");
            });
        }

        private static void Run(Func<Task> test)
        {
            var task = Task.Run(test);
            Assert.AreSame(task, Task.WhenAny(task, Task.Delay(5000)).GetAwaiter().GetResult());
            task.GetAwaiter().GetResult();
        }
    }
}
