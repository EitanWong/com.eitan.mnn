using System;
using System.Diagnostics;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>
    /// Bridges a streaming TTS producer to the shared live output pipeline. A producer may publish
    /// each PCM block as soon as it is decoded; it must retain and retry a block after backpressure.
    /// All methods except immutable properties belong to one TTS producer thread.
    /// </summary>
    public sealed partial class MNNRealtimeSpeechStream
    {
        private readonly MNNRealtimeOutputPipeline _output;
        private readonly MNNRealtimeSynthesisMeter _meter;
        private readonly Func<long> _monotonicClock;
        private readonly Action _waitForRetry;
        private readonly long _maximumBackpressureTicks;
        private readonly long _startedAt;
        private long _firstChunkTimestamp = -1;
        private long _activeSegmentId;
        private int _nextChunkSequence;
        private bool _hasActiveSegment;
        private bool _hasPublishedAudio;
        private bool _responseCompleted;
        private int _productionStarted;

        public MNNResponseId Response { get; }
        public bool IsCurrent => _output.Accepts(Response);
        public MNNRealtimeSynthesisMeter SynthesisMeter => _meter;
        public TimeSpan MaximumBackpressureWait { get; }
        public double FirstChunkLatencyMilliseconds
        {
            get
            {
                long firstChunkTimestamp = Interlocked.Read(ref _firstChunkTimestamp);
                return firstChunkTimestamp < 0 ? double.NaN :
                    (firstChunkTimestamp - _startedAt) * 1000.0 / Stopwatch.Frequency;
            }
        }

        public MNNRealtimeSpeechStream(MNNRealtimeOutputPipeline output, MNNResponseId response,
            MNNRealtimeSynthesisMeter synthesisMeter = null)
            : this(output, response, TimeSpan.FromMilliseconds(300), synthesisMeter, null, null)
        {
        }

        /// <summary>
        /// Limits each continuous PCM publication stall. Timeout.InfiniteTimeSpan is for offline processing.
        /// This does not bound synthesis calls, idle text waits, or audio already submitted to a device.
        /// </summary>
        public MNNRealtimeSpeechStream(MNNRealtimeOutputPipeline output, MNNResponseId response,
            TimeSpan maximumBackpressureWait, MNNRealtimeSynthesisMeter synthesisMeter = null)
            : this(output, response, maximumBackpressureWait, synthesisMeter, null, null)
        {
        }

        internal MNNRealtimeSpeechStream(MNNRealtimeOutputPipeline output, MNNResponseId response,
            TimeSpan maximumBackpressureWait, MNNRealtimeSynthesisMeter synthesisMeter,
            Func<long> monotonicClock, Action waitForRetry)
        {
            _output = output ?? throw new ArgumentNullException(nameof(output));
            if (!response.IsValid || !output.Accepts(response)) throw new ArgumentException("Response is not active on this output pipeline.", nameof(response));
            if (maximumBackpressureWait != Timeout.InfiniteTimeSpan &&
                (maximumBackpressureWait <= TimeSpan.Zero ||
                 maximumBackpressureWait.TotalSeconds * Stopwatch.Frequency >= long.MaxValue))
                throw new ArgumentOutOfRangeException(nameof(maximumBackpressureWait));
            MaximumBackpressureWait = maximumBackpressureWait;
            _maximumBackpressureTicks = maximumBackpressureWait == Timeout.InfiniteTimeSpan
                ? -1 : Math.Max(1, (long)(maximumBackpressureWait.TotalSeconds * Stopwatch.Frequency));
            _monotonicClock = monotonicClock ?? new Func<long>(Stopwatch.GetTimestamp);
            _waitForRetry = waitForRetry ?? new Action(() => Thread.Sleep(1));
            Response = response;
            _meter = synthesisMeter ?? new MNNRealtimeSynthesisMeter();
            _startedAt = Stopwatch.GetTimestamp();
        }

        /// <summary>
        /// Blocking, single-use TTS owner loop. Call on a worker, never a Unity or audio callback thread.
        /// The synthesizer must synchronously emit borrowed PCM, mark its last block and honor cancellation;
        /// it must not retain the callback. Format must match playback; resample upstream if necessary.
        /// Splits blocks without accumulating a waveform and retries queue backpressure with short polling waits.
        /// Returns false when interrupted or output faults; cancellation and backend failures propagate to the control runtime,
        /// which must invalidate output and await quiescence before disposing or reusing the backend.
        /// Throws TimeoutException when PCM publication stays blocked beyond MaximumBackpressureWait.
        /// </summary>
        public bool Produce(MNNRealtimeSpeechTextChannel textChannel, MNNPcmFormat synthesisFormat,
            Action<string, Action<float[], int, int, bool>, CancellationToken> synthesize,
            CancellationToken cancellationToken = default)
        {
            if (textChannel == null) throw new ArgumentNullException(nameof(textChannel));
            if (synthesize == null) throw new ArgumentNullException(nameof(synthesize));
            synthesisFormat.RequireValid();
            if (!synthesisFormat.Equals(_output.Format))
                throw new ArgumentException("Synthesis PCM format must match the output pipeline.", nameof(synthesisFormat));
            if (textChannel.Response != Response)
                throw new ArgumentException("Text must belong to this output pipeline and response.", nameof(textChannel));
            if (Interlocked.CompareExchange(ref _productionStarted, 1, 0) != 0)
                throw new InvalidOperationException("A speech stream can run one production loop only.");
            if (_responseCompleted || _hasActiveSegment || _hasPublishedAudio)
                throw new InvalidOperationException("Start production on a new speech stream.");

            try
            {
                while (true)
                {
                    RequireCurrent();
                    if (!textChannel.TryRead(out var segment))
                    {
                        // Read again after acquiring EOS: the writer may have published its last
                        // segment between our empty read and its release-store of completion.
                        if (!textChannel.IsCompleted)
                        {
                            textChannel.WaitForData(cancellationToken);
                            continue;
                        }
                        if (!textChannel.TryRead(out segment))
                            return CompleteResponse();
                    }
                    if (!BeginSegment(segment))
                    {
                        RequireCurrent();
                        throw new InvalidOperationException("Spoken segment registration failed; check ledger capacity and segment IDs.");
                    }
                    bool ended = false;
                    bool acceptingPcm = true;
                    int ownerThread = Thread.CurrentThread.ManagedThreadId;
                    long workStartedAt = Stopwatch.GetTimestamp();
                    try
                    {
                        synthesize(segment.Text, Publish, cancellationToken);
                    }
                    finally
                    {
                        acceptingPcm = false;
                    }
                    RequireCurrent();
                    if (!ended)
                        throw new InvalidOperationException("Synthesizer returned without a final PCM block.");

                    void Publish(float[] samples, int offset, int count, bool endsSegment)
                    {
                        if (Thread.CurrentThread.ManagedThreadId != ownerThread || !acceptingPcm)
                            throw new InvalidOperationException("PCM callbacks must run synchronously on the synthesis owner thread.");
                        RequireCurrent();
                        if (ended)
                            throw new InvalidOperationException("Synthesizer emitted PCM after its final block.");
                        MNNSpscPcmQueue<MNNSpeechChunkInfo>.ValidateRange(samples, offset, count);
                        if (count < 1 || count % synthesisFormat.Channels != 0)
                            throw new ArgumentOutOfRangeException(nameof(count), "Publish nonempty, complete PCM frames.");
                        // Validate the whole borrowed block before any prefix can be played.
                        for (int i = offset; i < offset + count; ++i)
                        {
                            float value = samples[i];
                            if (float.IsNaN(value) || float.IsInfinity(value) || value < -1 || value > 1)
                                throw new ArgumentException("Synthesis must emit finite normalized PCM.", nameof(samples));
                        }
                        RecordSynthesisWork(Stopwatch.GetTimestamp() - workStartedAt, count / synthesisFormat.Channels);
                        int remaining = count;
                        long blockedSince = -1;
                        long lastWaitTimestamp = -1;
                        while (remaining > 0)
                        {
                            RequireCurrent();
                            // Check before retrying so newly freed capacity cannot admit overdue audio.
                            if (blockedSince >= 0)
                            {
                                long now = ReadWaitClock();
                                if (now < lastWaitTimestamp)
                                    throw new InvalidOperationException("The speech backpressure clock moved backwards.");
                                if (now - blockedSince > _maximumBackpressureTicks)
                                    throw new TimeoutException("PCM publication exceeded the speech backpressure wait budget.");
                                lastWaitTimestamp = now;
                            }
                            int take = Math.Min(remaining, _output.MaximumSamplesPerChunk);
                            if (!TryPublishPcm(samples, offset, take, endsSegment && take == remaining))
                            {
                                RequireCurrent();
                                if (blockedSince < 0 && _maximumBackpressureTicks >= 0)
                                    blockedSince = lastWaitTimestamp = ReadWaitClock();
                                _waitForRetry();
                                continue;
                            }
                            blockedSince = -1;
                            offset += take;
                            remaining -= take;
                        }
                        ended = endsSegment;
                        // Queue waiting is not synthesis compute and must not be counted again
                        // on every retry or charged to the next generated block's RTF.
                        workStartedAt = Stopwatch.GetTimestamp();
                    }
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && !IsCurrent)
            {
                return false;
            }

            void RequireCurrent()
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsCurrent)
                    throw new OperationCanceledException("Speech response has been interrupted or output has faulted.");
            }
        }

        private long ReadWaitClock()
        {
            long timestamp = _monotonicClock();
            if (timestamp < 0)
                throw new InvalidOperationException("Speech backpressure requires nonnegative Stopwatch timestamps.");
            return timestamp;
        }

        /// <summary>Register immutable spoken text before the first audio block for this segment.</summary>
        public bool BeginSegment(long segmentId, string stableSpokenText)
        {
            if (_responseCompleted || _hasActiveSegment) throw new InvalidOperationException("Finish the active speech segment before starting another.");
            if (segmentId < 0) throw new ArgumentOutOfRangeException(nameof(segmentId));
            if (!_output.TryRegisterSegment(Response, segmentId, stableSpokenText)) return false;
            _activeSegmentId = segmentId;
            _nextChunkSequence = 0;
            _hasActiveSegment = true;
            return true;
        }

        /// <summary>Starts a segment read from the response's bounded model-text channel.</summary>
        public bool BeginSegment(MNNRealtimeSpeechTextSegment segment)
        {
            if (segment.Response != Response || !IsCurrent)
                return false;
            return BeginSegment(segment.SegmentId, segment.Text);
        }

        /// <summary>Record decoder work once per generated block; generatedFrames uses the playback sample rate.</summary>
        public void RecordSynthesisWork(long computeTicks, int generatedFrames)
        {
            if (_responseCompleted) throw new InvalidOperationException("Response is complete.");
            _meter.Record(computeTicks, generatedFrames, _output.Format.SampleRate);
        }

        /// <summary>
        /// Publish a block now. On false, retry only while IsCurrent remains true; otherwise abandon this response.
        /// A race with interruption is resolved by the output response gate.
        /// </summary>
        public bool TryPublishPcm(float[] samples, int offset, int count, bool endsSegment)
        {
            if (_responseCompleted || !_hasActiveSegment) throw new InvalidOperationException("Begin a stable-text segment before publishing PCM.");
            var info = new MNNSpeechChunkInfo(Response, _activeSegmentId, _nextChunkSequence, endsSegment);
            if (!_output.TryEnqueue(samples, offset, count, info)) return false;
            ++_nextChunkSequence;
            if (!_hasPublishedAudio)
            {
                _hasPublishedAudio = true;
                Interlocked.CompareExchange(ref _firstChunkTimestamp, Stopwatch.GetTimestamp(), -1);
            }
            if (endsSegment) _hasActiveSegment = false;
            return true;
        }

        /// <summary>Closes production after all segment producers have emitted their final PCM blocks.</summary>
        public bool CompleteResponse()
        {
            if (_hasActiveSegment) throw new InvalidOperationException("Finish the active segment before completing the response.");
            if (_responseCompleted) return false;
            _responseCompleted = _output.CompleteResponse(Response);
            return _responseCompleted;
        }
    }
}
