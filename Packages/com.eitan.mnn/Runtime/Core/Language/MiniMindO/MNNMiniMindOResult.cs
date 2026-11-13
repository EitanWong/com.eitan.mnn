namespace MNN.Unity
{
    /// <summary>Generated text, aligned eight-codebook Mimi frames and decoded mono speech.</summary>
    public sealed class MNNMiniMindOResult
    {
        public string Text { get; }
        public int[][] AudioCodes { get; }
        public float[] Waveform { get; }
        public int SampleRate => 24000;
        public int GeneratedTokens { get; }
        /// <summary>True when the generation budget ended before text and all speech streams stopped.</summary>
        public bool TokenLimitReached { get; }
        internal MNNMiniMindOResult(string text, int[][] codes, float[] waveform, int tokens, bool limited)
        {
            Text = text; AudioCodes = codes; Waveform = waveform; GeneratedTokens = tokens; TokenLimitReached = limited;
        }
    }
}
