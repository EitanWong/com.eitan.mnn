using System;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>One capture producer and one processing consumer. Full buffers report a gap, never move the reader.</summary>
    public sealed class MNNAudioCaptureBuffer
    {
        private readonly MNNSpscPcmQueue<MNNAudioFrameInfo> _queue;
        private bool _gap;
        private long _droppedBlocks;
        public long DroppedBlocks => Interlocked.Read(ref _droppedBlocks);
        public int AvailableBlocks => _queue.AvailableBlocks;
        public bool CanWrite => _queue.CanWrite;
        public int MaximumSamples => _queue.MaximumSamples;
        public bool HasPendingFrames => _queue.HasItems;

        public MNNAudioCaptureBuffer(int capacity, int maximumSamples)
        {
            _queue = new MNNSpscPcmQueue<MNNAudioFrameInfo>(capacity, maximumSamples);
        }

        /// <summary>Copies input; the caller retains its array. No allocation or waiting for capacity.</summary>
        public bool TryWrite(float[] samples, int offset, int count, MNNAudioFrameInfo info)
        {
            info.Format.RequireValid();
            if (count % info.Format.Channels != 0)
                throw new ArgumentException("Supply complete interleaved sample frames.", nameof(count));
            if (_queue.TryWrite(samples, offset, count, _gap ? info.WithGap() : info))
            {
                _gap = false;
                return true;
            }
            _gap = true;
            Interlocked.Increment(ref _droppedBlocks);
            return false;
        }

        /// <summary>The processing consumer supplies reusable storage; never returns internal pooled arrays.</summary>
        public bool TryRead(float[] destination, int offset, out MNNAudioFrameInfo info, out int count)
        {
            MNNSpscPcmQueue<MNNAudioFrameInfo>.ValidateRange(destination, offset, 0);
            if (!_queue.TryPeek(out info, out var pcm, out count))
                return false;
            MNNSpscPcmQueue<MNNAudioFrameInfo>.ValidateRange(destination, offset, count);
            Array.Copy(pcm, 0, destination, offset, count);
            _queue.Remove();
            return true;
        }
    }
}
