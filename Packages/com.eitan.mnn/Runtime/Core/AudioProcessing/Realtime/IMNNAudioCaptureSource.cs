namespace MNN.Unity
{
    /// <summary>Single-producer capture queue consumed by one audio-processing worker.</summary>
    public interface IMNNAudioCaptureSource
    {
        MNNPcmFormat CaptureFormat { get; }
        int MaximumCaptureSamples { get; }
        long DroppedCaptureBlocks { get; }
        bool HasPendingCaptureFrames { get; }
        /// <summary>
        /// Copies one complete interleaved block. Live metadata timestamps the first sample frame
        /// in current-process Stopwatch ticks; historical replay requires an explicit offline age policy.
        /// </summary>
        bool TryReadCapture(float[] destination, int offset, out MNNAudioFrameInfo info, out int count);
    }
}
