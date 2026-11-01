namespace MNN.Unity
{
    /// <summary>Speech interval in seconds in the original recording. End is exclusive.</summary>
    public sealed class MNNSpeechSegment
    {
        public double StartSeconds { get; }
        public double EndSeconds { get; }
        public double DurationSeconds => EndSeconds - StartSeconds;
        internal MNNSpeechSegment(int start, int end)
            : this(start / 16000.0, end / 16000.0) { }
        internal MNNSpeechSegment(double start, double end)
        {
            StartSeconds = start;
            EndSeconds = end;
        }
    }
}
