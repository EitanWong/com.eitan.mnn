using System;

namespace MNN.Unity
{
    /// <summary>Output is already converted to the playback format before entering the render queue.</summary>
    public readonly struct MNNSpeechChunkInfo
    {
        public MNNResponseId Response { get; }
        public long SegmentId { get; }
        public int ChunkSequence { get; }
        public bool EndsSegment { get; }

        public MNNSpeechChunkInfo(MNNResponseId response, long segmentId, int chunkSequence, bool endsSegment)
        {
            if (!response.IsValid || segmentId < 0 || chunkSequence < 0)
                throw new ArgumentOutOfRangeException(nameof(response));
            Response = response;
            SegmentId = segmentId;
            ChunkSequence = chunkSequence;
            EndsSegment = endsSegment;
        }
    }
}
