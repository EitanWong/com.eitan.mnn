namespace MNN.Unity
{
    /// <summary>Whole model frames and their exclusive stream cursor, atomically transferred at wake activation.</summary>
    public sealed class MNNConversationWakeHandoff
    {
        public readonly float[] Frames;
        public readonly long EndSample, CaptureRevision;
        public readonly bool LostPrefix;
        public MNNConversationWakeHandoff(float[] frames, long endSample, long captureRevision, bool lostPrefix)
        { Frames = frames; EndSample = endSample; CaptureRevision = captureRevision; LostPrefix = lostPrefix; }
    }
}
