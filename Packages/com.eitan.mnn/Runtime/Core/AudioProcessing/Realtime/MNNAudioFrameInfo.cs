using System;

namespace MNN.Unity
{
    /// <summary>Capture position and monotonic timestamp, independent of Unity frames and reply generations.</summary>
    public readonly struct MNNAudioFrameInfo
    {
        public long StreamId { get; }
        public long FormatEpoch { get; }
        public long Sequence { get; }
        public long StartFrame { get; }
        /// <summary>
        /// First sample frame's capture time in current-process Stopwatch ticks, not enqueue time.
        /// Map hardware timestamps into this clock before publishing live audio.
        /// </summary>
        public long MonotonicTicks { get; }
        public MNNPcmFormat Format { get; }
        public bool Discontinuity { get; }

        public MNNAudioFrameInfo(long streamId, long formatEpoch, long sequence, long startFrame,
            long monotonicTicks, MNNPcmFormat format, bool discontinuity = false)
        {
            format.RequireValid();
            if (streamId < 1 || formatEpoch < 1 || sequence < 0 || startFrame < 0 || monotonicTicks < 0)
                throw new ArgumentOutOfRangeException(nameof(streamId), "Frame identifiers and positions must be valid.");
            StreamId = streamId;
            FormatEpoch = formatEpoch;
            Sequence = sequence;
            StartFrame = startFrame;
            MonotonicTicks = monotonicTicks;
            Format = format;
            Discontinuity = discontinuity;
        }

        internal MNNAudioFrameInfo WithGap() => new MNNAudioFrameInfo(StreamId, FormatEpoch, Sequence,
            StartFrame, MonotonicTicks, Format, true);
    }
}
