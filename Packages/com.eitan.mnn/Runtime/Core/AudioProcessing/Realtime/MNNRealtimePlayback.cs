using System;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>
    /// Bounded PCM output shared by Omni and TTS. One enqueue producer, one render consumer,
    /// one receipt consumer. Control may invalidate generations without touching queue cursors.
    /// </summary>
    public sealed class MNNRealtimePlayback
    {
        private readonly MNNResponseGate _gate = new MNNResponseGate();
        private readonly MNNSpscPcmQueue<MNNSpeechChunkInfo> _queue;
        private readonly MNNSpscQueue<MNNPlaybackReceipt> _receipts;
        private readonly int _sliceSamples;
        private int _readOffset;
        private int _faulted;
        private long _completedResponseEpoch;
        private long _renderedFrames;
        private long _queuedFrames;
        private long _underrunFrames;
        private long _discardedChunks;
        public MNNPcmFormat Format { get; }
        public int MaximumSamples => _queue.MaximumSamples;
        public bool IsFaulted => Volatile.Read(ref _faulted) != 0;
        public long RenderedFrames => Interlocked.Read(ref _renderedFrames);
        public long QueuedFrames => Interlocked.Read(ref _queuedFrames);
        public double QueuedAudioMilliseconds => QueuedFrames * 1000.0 / Format.SampleRate;
        public long UnderrunFrames => Interlocked.Read(ref _underrunFrames);
        public long DiscardedChunks => Interlocked.Read(ref _discardedChunks);
        public MNNResponseId CurrentResponse => _gate.Current;

        public MNNRealtimePlayback(MNNPcmFormat format, int framesPerBlock, int blockCapacity = 6, int receiptCapacity = 256)
        {
            format.RequireValid();
            if (framesPerBlock < 1 || framesPerBlock > format.SampleRate)
                throw new ArgumentOutOfRangeException(nameof(framesPerBlock));
            Format = format;
            _queue = new MNNSpscPcmQueue<MNNSpeechChunkInfo>(blockCapacity, format.SamplesForFrames(framesPerBlock));
            _receipts = new MNNSpscQueue<MNNPlaybackReceipt>(receiptCapacity);
            _sliceSamples = Math.Max(1, format.SampleRate / 200) * format.Channels;
        }

        /// <summary>Control executor only. Caller must drain/cancel the old producer before starting a new one.</summary>
        public MNNResponseId BeginResponse()
        {
            if (IsFaulted)
                throw new InvalidOperationException("Receipt transport overflowed. Stop the device and recreate playback.");
            var response = _gate.Begin();
            Interlocked.Exchange(ref _completedResponseEpoch, 0);
            return response;
        }

        /// <summary>Immediate generation invalidation. Already submitted hardware audio cannot be recalled here.</summary>
        public void Interrupt() => _gate.Silence();

        /// <summary>Closes production after its worker has emitted final EOS; queued audio remains renderable.</summary>
        public bool CompleteResponse(MNNResponseId response)
        {
            if (!Accepts(response))
                return false;
            Interlocked.Exchange(ref _completedResponseEpoch, response.Epoch);
            return true;
        }

        public bool IsComplete(MNNResponseId response) => response.IsValid &&
            Interlocked.Read(ref _completedResponseEpoch) == response.Epoch;

        public bool HasPendingAudio => _queue.HasItems;
        public bool HasPendingReceipts => _receipts.HasItems;

        public bool Accepts(MNNResponseId response) => !IsFaulted && _gate.Accepts(response);

        /// <summary>Producer copies bounded finite normalized PCM. False means stale output or backpressure.</summary>
        public bool TryEnqueue(float[] samples, int offset, int count, MNNSpeechChunkInfo info)
        {
            MNNSpscPcmQueue<MNNSpeechChunkInfo>.ValidateRange(samples, offset, count);
            if (count < 1 || count > MaximumSamples || count % Format.Channels != 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (!Accepts(info.Response) || IsComplete(info.Response))
                return false;
            for (int i = offset; i < offset + count; ++i)
            {
                float value = samples[i];
                if (float.IsNaN(value) || float.IsInfinity(value) || value < -1 || value > 1)
                    throw new ArgumentException("Output must contain finite normalized PCM.", nameof(samples));
            }
            // Invalidation can race this publication; the consumer checks the immutable response again.
            long frames = count / Format.Channels;
            Interlocked.Add(ref _queuedFrames, frames);
            if (_queue.TryWrite(samples, offset, count, info))
                return true;
            Interlocked.Add(ref _queuedFrames, -frames);
            return false;
        }

        /// <summary>
        /// Device callback only. Fills every requested sample, including silence. No allocation or locks.
        /// The device must submit this buffer unchanged and acknowledge actual presentation separately.
        /// </summary>
        public void Render(float[] destination, int offset, int count)
        {
            MNNSpscPcmQueue<MNNSpeechChunkInfo>.ValidateRange(destination, offset, count);
            if (count % Format.Channels != 0)
                throw new ArgumentException("Render complete sample frames.", nameof(count));
            Array.Clear(destination, offset, count);
            int written = 0;
            long outputStart = _renderedFrames;
            while (written < count && !IsFaulted && _queue.TryPeek(out var info, out var pcm, out int length))
            {
                if (!_gate.Accepts(info.Response))
                {
                    DiscardCurrentChunk(length);
                    continue;
                }
                int take = Math.Min(Math.Min(length - _readOffset, count - written), _sliceSamples);
                Array.Copy(pcm, _readOffset, destination, offset + written, take);
                bool complete = _readOffset + take == length;
                if (!_gate.Accepts(info.Response))
                {
                    Array.Clear(destination, offset + written, take);
                    DiscardCurrentChunk(length);
                    continue;
                }
                var receipt = new MNNPlaybackReceipt(info, _readOffset / Format.Channels,
                    take / Format.Channels, outputStart + written / Format.Channels, complete, complete && info.EndsSegment);
                if (!_receipts.TryWrite(receipt))
                {
                    Array.Clear(destination, offset + written, take);
                    Volatile.Write(ref _faulted, 1);
                    _gate.Silence();
                    break;
                }
                written += take;
                _readOffset += take;
                Interlocked.Add(ref _queuedFrames, -take / Format.Channels);
                if (complete)
                {
                    _readOffset = 0;
                    _queue.Remove();
                }
            }
            var current = _gate.Current;
            if (written < count && current.IsValid && !IsFaulted && !IsComplete(current))
                Interlocked.Add(ref _underrunFrames, (count - written) / Format.Channels);
            Interlocked.Add(ref _renderedFrames, count / Format.Channels);
        }

        public bool TryReadReceipt(out MNNPlaybackReceipt receipt) => _receipts.TryRead(out receipt);

        // Render consumer only. Submitted slices have already left the queue watermark.
        private void DiscardCurrentChunk(int length)
        {
            int remainingFrames = (length - _readOffset) / Format.Channels;
            _readOffset = 0;
            _queue.Remove();
            Interlocked.Add(ref _queuedFrames, -remainingFrames);
            Interlocked.Increment(ref _discardedChunks);
        }
    }
}
