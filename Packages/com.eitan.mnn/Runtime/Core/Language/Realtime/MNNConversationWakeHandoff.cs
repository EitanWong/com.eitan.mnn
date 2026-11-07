using System;

namespace MNN.Unity
{
    /// <summary>Whole mono 16 kHz model frames and their exclusive stream cursor, transferred at wake activation.
    /// The caller transfers ownership of Frames and must not modify it after construction.</summary>
    public sealed class MNNConversationWakeHandoff
    {
        public readonly float[] Frames;
        public readonly long EndSample, CaptureRevision;
        public readonly bool LostPrefix;
        public MNNConversationWakeHandoff(float[] frames, long endSample, long captureRevision, bool lostPrefix)
        {
            if (frames == null) throw new ArgumentNullException(nameof(frames));
            if (frames.Length % 512 != 0 || frames.Length > 16000 * MNNRealtimeUtteranceBuffer.MaximumSeconds)
                throw new ArgumentOutOfRangeException(nameof(frames), "Wake audio must contain whole 512-sample frames within the supported utterance limit.");
            if (endSample < frames.Length || endSample % 512 != 0)
                throw new ArgumentOutOfRangeException(nameof(endSample), "The exclusive stream cursor must follow the supplied whole frames.");
            if (captureRevision < 0) throw new ArgumentOutOfRangeException(nameof(captureRevision));
            Frames = frames; EndSample = endSample; CaptureRevision = captureRevision; LostPrefix = lostPrefix;
        }
    }
}
