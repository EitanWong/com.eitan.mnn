using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    public sealed partial class MNNRealtimeSpeechStream
    {
        /// <summary>
        /// Async single-owner equivalent of Produce for network synthesizers. Callbacks are awaited in order;
        /// buffers are borrowed until the callback completes. No Unity or device callback is blocked.
        /// RTF includes synthesis/network wait and excludes text wait and playback backpressure.
        /// </summary>
        public async Task<bool> ProduceAsync(MNNRealtimeSpeechTextChannel textChannel, MNNPcmFormat format,
            Func<string, Func<float[], int, int, bool, Task>, CancellationToken, Task> synthesize,
            CancellationToken cancellationToken = default)
        {
            if (textChannel == null) throw new ArgumentNullException(nameof(textChannel));
            if (synthesize == null) throw new ArgumentNullException(nameof(synthesize));
            format.RequireValid();
            if (!format.Equals(_output.Format) || textChannel.Response != Response)
                throw new ArgumentException("Text identity and PCM format must match the output pipeline.");
            if (Interlocked.CompareExchange(ref _productionStarted, 1, 0) != 0 ||
                _responseCompleted || _hasActiveSegment || _hasPublishedAudio)
                throw new InvalidOperationException("Start one production loop on a new speech stream.");
            try
            {
                while (true)
                {
                    RequireCurrent();
                    if (!textChannel.TryRead(out var segment))
                    {
                        if (!textChannel.IsCompleted)
                        {
                            await textChannel.WaitForDataAsync(cancellationToken).ConfigureAwait(false);
                            continue;
                        }
                        if (!textChannel.TryRead(out segment)) return CompleteResponse();
                    }
                    if (!BeginSegment(segment)) throw new InvalidOperationException("Speech ledger capacity exhausted.");
                    bool ended = false;
                    bool accepting = true;
                    int publishing = 0;
                    long workStarted = Stopwatch.GetTimestamp();
                    try { await synthesize(segment.Text, PublishAsync, cancellationToken).ConfigureAwait(false); }
                    finally { accepting = false; }
                    RequireCurrent();
                    if (!ended) throw new InvalidOperationException("Synthesizer returned without final PCM.");

                    async Task PublishAsync(float[] samples, int offset, int count, bool final)
                    {
                        if (Interlocked.Exchange(ref publishing, 1) != 0)
                            throw new InvalidOperationException("Await each PCM callback before publishing another block.");
                        try
                        {
                            RequireCurrent();
                            if (!accepting || ended) throw new InvalidOperationException("PCM arrived outside synthesis lifetime.");
                            MNNSpscPcmQueue<MNNSpeechChunkInfo>.ValidateRange(samples, offset, count);
                            if (count < 1 || count % format.Channels != 0) throw new ArgumentOutOfRangeException(nameof(count));
                            for (int i = offset; i < offset + count; ++i)
                                if (float.IsNaN(samples[i]) || float.IsInfinity(samples[i]) || Math.Abs(samples[i]) > 1)
                                    throw new ArgumentException("Supply finite normalized PCM.");
                            RecordSynthesisWork(Stopwatch.GetTimestamp() - workStarted, count / format.Channels);
                            int remaining = count;
                            long blockedAt = -1;
                            long lastWaitTimestamp = -1;
                            while (remaining > 0)
                            {
                                RequireCurrent();
                                if (blockedAt >= 0)
                                {
                                    long now = ReadWaitClock();
                                    if (now < lastWaitTimestamp)
                                        throw new InvalidOperationException("The speech backpressure clock moved backwards.");
                                    if (_maximumBackpressureTicks >= 0 && now - blockedAt > _maximumBackpressureTicks)
                                        throw new TimeoutException("PCM publication exceeded the speech backpressure budget.");
                                    lastWaitTimestamp = now;
                                }
                                int take = Math.Min(remaining, _output.MaximumSamplesPerChunk);
                                if (!TryPublishPcm(samples, offset, take, final && remaining == take))
                                {
                                    if (blockedAt < 0) blockedAt = lastWaitTimestamp = ReadWaitClock();
                                    await Task.Delay(1, cancellationToken).ConfigureAwait(false);
                                    continue;
                                }
                                blockedAt = -1;
                                lastWaitTimestamp = -1;
                                offset += take;
                                remaining -= take;
                            }
                            ended = final;
                            workStarted = Stopwatch.GetTimestamp();
                        }
                        finally { Volatile.Write(ref publishing, 0); }
                    }
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && !IsCurrent) { return false; }

            void RequireCurrent()
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsCurrent) throw new OperationCanceledException("Speech response is stale.");
            }
        }
    }
}
