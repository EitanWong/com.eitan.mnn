using System;

namespace MNN.Unity
{
    /// <summary>Interleaved float PCM. Counts in this API are sample frames unless named samples.</summary>
    public readonly struct MNNPcmFormat : IEquatable<MNNPcmFormat>
    {
        public int SampleRate { get; }
        public int Channels { get; }

        public MNNPcmFormat(int sampleRate, int channels)
        {
            if (sampleRate < 8000 || sampleRate > 192000)
                throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (channels < 1 || channels > 8)
                throw new ArgumentOutOfRangeException(nameof(channels));
            SampleRate = sampleRate;
            Channels = channels;
        }

        public int SamplesForFrames(int frames)
        {
            RequireValid();
            if (frames < 0)
                throw new ArgumentOutOfRangeException(nameof(frames));
            return checked(frames * Channels);
        }

        /// <summary>Reject an uninitialized default PCM format before binding a device or allocating buffers.</summary>
        public void RequireValid()
        {
            if (SampleRate == 0 || Channels == 0)
                throw new ArgumentException("An initialized PCM format is required.");
        }

        public bool Equals(MNNPcmFormat other) => SampleRate == other.SampleRate && Channels == other.Channels;
        public override bool Equals(object obj) => obj is MNNPcmFormat other && Equals(other);
        public override int GetHashCode() => (SampleRate * 397) ^ Channels;
    }
}
