namespace MNN.Unity
{
    /// <summary>Immutable spoken text queued from the model owner to the TTS owner.</summary>
    public readonly struct MNNRealtimeSpeechTextSegment
    {
        public MNNResponseId Response { get; }
        public long SegmentId { get; }
        public string Text { get; }

        internal MNNRealtimeSpeechTextSegment(MNNResponseId response, long segmentId, string text)
        {
            Response = response;
            SegmentId = segmentId;
            Text = text;
        }
    }
}
