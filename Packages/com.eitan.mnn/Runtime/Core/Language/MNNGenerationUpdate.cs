namespace MNN.Unity
{
    /// <summary>Immutable cumulative snapshot delivered on the inference caller's thread.</summary>
    public sealed class MNNGenerationUpdate
    {
        public string Text { get; }

        public int GeneratedTokens { get; }

        internal MNNGenerationUpdate(string text, int tokens)
        {
            Text = text;
            GeneratedTokens = tokens;
        }
    }
}
