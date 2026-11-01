namespace MNN.Unity
{
    /// <summary>Immutable snapshot; only IsPresented text may be projected into spoken assistant history.</summary>
    public readonly struct MNNConversationSegment
    {
        public MNNResponseId Response { get; }
        public long SegmentId { get; }
        public string Text { get; }
        public long SubmittedFrames { get; }
        public bool IsPresented { get; }
        public bool IsInterrupted { get; }
        public bool IsEstimatedPresentation { get; }

        internal MNNConversationSegment(MNNResponseId response, long segmentId, string text, long submittedFrames,
            bool presented, bool interrupted, bool estimated)
        {
            Response = response;
            SegmentId = segmentId;
            Text = text;
            SubmittedFrames = submittedFrames;
            IsPresented = presented;
            IsInterrupted = interrupted;
            IsEstimatedPresentation = estimated;
        }
    }
}
