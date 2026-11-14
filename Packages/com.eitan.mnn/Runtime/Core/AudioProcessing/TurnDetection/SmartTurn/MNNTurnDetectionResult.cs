namespace MNN.Unity
{
    /// <summary>Probability that the current audio turn is complete, using its last eight seconds.</summary>
    public sealed class MNNTurnDetectionResult
    {
        public float CompletionProbability { get; }
        public bool IsComplete { get; }
        public double DurationSeconds { get; }
        public MNNTurnDetectionResult(float probability, float threshold, double duration)
        {
            CompletionProbability = probability;
            IsComplete = probability > threshold;
            DurationSeconds = duration;
        }
    }
}
