namespace MNN.Unity
{
    /// <summary>A submitted slice, not proof of acoustic presentation. Positions are exclusive at the end.</summary>
    public readonly struct MNNPlaybackReceipt
    {
        public MNNSpeechChunkInfo Chunk { get; }
        public int SourceStartFrame { get; }
        public int FrameCount { get; }
        public long OutputStartFrame { get; }
        public long OutputEndFrame => OutputStartFrame + FrameCount;
        public bool EndsChunk { get; }
        public bool EndsSegment { get; }

        internal MNNPlaybackReceipt(MNNSpeechChunkInfo chunk, int sourceStartFrame, int frameCount,
            long outputStartFrame, bool endsChunk, bool endsSegment)
        {
            Chunk = chunk;
            SourceStartFrame = sourceStartFrame;
            FrameCount = frameCount;
            OutputStartFrame = outputStartFrame;
            EndsChunk = endsChunk;
            EndsSegment = endsSegment;
        }
    }
}
