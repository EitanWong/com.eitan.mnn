namespace MNN.Unity
{
    /// <summary>The author's two probabilities, with no additional softmax.</summary>
    public sealed class MNNTurnSenseResult
    {
        public float NonEndOfUtteranceProbability { get; }
        public float EndOfUtteranceProbability { get; }
        public bool IsComplete => EndOfUtteranceProbability > NonEndOfUtteranceProbability;

        internal MNNTurnSenseResult(float incomplete, float complete)
        {
            NonEndOfUtteranceProbability = incomplete;
            EndOfUtteranceProbability = complete;
        }
    }
}
