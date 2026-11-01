using System;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    /// <summary>
    /// Per-response bounded SPSC channel from append-only model text to one TTS owner.
    /// The model owner writes; the TTS owner reads; neither may share ownership of the segmenter.
    /// </summary>
    public sealed class MNNRealtimeSpeechTextChannel
    {
        private readonly MNNRealtimeOutputPipeline _output;
        private readonly MNNStableSpeechTextSegmenter _segmenter;
        private readonly MNNSpscQueue<MNNRealtimeSpeechTextSegment> _queue;
        private readonly SemaphoreSlim _available = new SemaphoreSlim(0);
        private readonly Func<string, bool> _segmentWriter;
        private long _nextSegmentId;
        private long _backpressureCount;
        private long _staleAppendCount;
        private bool _completed;

        public MNNResponseId Response { get; }
        public bool IsCurrent => _output.Accepts(Response);
        public bool HasQueuedSegments => _queue.HasItems;
        public bool IsCompleted => Volatile.Read(ref _completed);
        public long BackpressureCount => Interlocked.Read(ref _backpressureCount);
        public long StaleAppendCount => Interlocked.Read(ref _staleAppendCount);

        public MNNRealtimeSpeechTextChannel(MNNRealtimeOutputPipeline output, MNNResponseId response,
            int capacity = 2, int maximumSegmentCharacters = 180)
        {
            _output = output ?? throw new ArgumentNullException(nameof(output));
            if (!response.IsValid || !output.Accepts(response))
                throw new ArgumentException("Response is not active on this output pipeline.", nameof(response));
            if (capacity < 1 || capacity > 256) throw new ArgumentOutOfRangeException(nameof(capacity));
            Response = response;
            _segmenter = new MNNStableSpeechTextSegmenter(maximumSegmentCharacters);
            _queue = new MNNSpscQueue<MNNRealtimeSpeechTextSegment>(capacity);
            _segmentWriter = EnqueueStableSegment;
        }

        /// <summary>
        /// Model owner only. The delta is consumed exactly once even when false is returned; pause text
        /// generation and call DrainPending after the TTS owner frees queue capacity. If IsCurrent is false,
        /// abandon this channel instead of retrying a stale response.
        /// </summary>
        public bool Append(string textDelta)
        {
            if (_completed) throw new InvalidOperationException("The text channel is complete.");
            if (!_output.Accepts(Response))
            {
                Interlocked.Increment(ref _staleAppendCount);
                return false;
            }
            bool drained = _segmenter.Append(textDelta, _segmentWriter);
            if (!drained) Interlocked.Increment(ref _backpressureCount);
            return drained;
        }

        /// <summary>
        /// Model owner only. Appends text in bounded deltas and awaits consumer backpressure.
        /// Returns false if this response becomes stale. Await each call before appending again.
        /// </summary>
        public async Task<bool> AppendAsync(string text, CancellationToken cancellationToken = default)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            if (_completed) throw new InvalidOperationException("The text channel is complete.");
            cancellationToken.ThrowIfCancellationRequested();
            for (int offset = 0; offset < text.Length;)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsCurrent) return false;
                int count = Math.Min(MNNStableSpeechTextSegmenter.MaximumInputDeltaCharacters, text.Length - offset);
                if (offset + count < text.Length && char.IsHighSurrogate(text[offset + count - 1]) && char.IsLowSurrogate(text[offset + count])) --count;
                if (!Append(text.Substring(offset, count)))
                {
                    while (IsCurrent && !DrainPending())
                        await Task.Delay(1, cancellationToken).ConfigureAwait(false);
                }
                offset += count;
            }
            return IsCurrent;
        }

        /// <summary>Model owner only. Retry stable text retained after queue backpressure without re-appending model text.</summary>
        public bool DrainPending()
        {
            if (_completed) return true;
            if (!_output.Accepts(Response))
            {
                Interlocked.Increment(ref _staleAppendCount);
                return false;
            }
            bool drained = _segmenter.DrainStableSegments(_segmentWriter);
            if (!drained) Interlocked.Increment(ref _backpressureCount);
            return drained;
        }

        /// <summary>Model owner only. EOS may be retried after backpressure; no text is silently discarded.</summary>
        public bool Complete()
        {
            if (_completed) return true;
            if (!_output.Accepts(Response))
            {
                Interlocked.Increment(ref _staleAppendCount);
                return false;
            }
            bool completed = _segmenter.Complete(_segmentWriter);
            Volatile.Write(ref _completed, completed);
            if (completed) _available.Release();
            if (!completed) Interlocked.Increment(ref _backpressureCount);
            return completed;
        }

        /// <summary>TTS owner only. Old-response segments are returned with their immutable response ID for gate checks.</summary>
        public bool TryRead(out MNNRealtimeSpeechTextSegment segment)
        {
            if (!_queue.TryRead(out segment)) return false;
            _available.Wait(0);
            return true;
        }

        /// <summary>Wait without polling until text, completion, cancellation or response interruption.</summary>
        public async Task WaitForDataAsync(CancellationToken cancellationToken = default)
        {
            _output.ResponseInterrupted += OnResponseInterrupted;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                // Register before checking the gate so an interruption cannot be lost between
                // the producer's empty read and entering the semaphore wait.
                if (!IsCurrent || IsCompleted || HasQueuedSegments) return;
                await _available.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _output.ResponseInterrupted -= OnResponseInterrupted;
            }
        }

        /// <summary>Blocking equivalent for a dedicated worker, never an audio callback.</summary>
        public void WaitForData(CancellationToken cancellationToken = default)
        {
            _output.ResponseInterrupted += OnResponseInterrupted;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsCurrent || IsCompleted || HasQueuedSegments) return;
                _available.Wait(cancellationToken);
            }
            finally
            {
                _output.ResponseInterrupted -= OnResponseInterrupted;
            }
        }

        private void OnResponseInterrupted(MNNResponseId response)
        {
            if (response == Response) _available.Release();
        }

        private bool EnqueueStableSegment(string text)
        {
            if (!_output.Accepts(Response))
            {
                Interlocked.Increment(ref _staleAppendCount);
                return false;
            }
            if (_nextSegmentId == long.MaxValue)
                throw new InvalidOperationException("Speech segment ID space is exhausted for this response.");
            long segmentId = _nextSegmentId;
            if (!_queue.TryWrite(new MNNRealtimeSpeechTextSegment(Response, segmentId, text)))
                return false;
            _available.Release();
            _nextSegmentId = segmentId + 1;
            return true;
        }
    }
}
