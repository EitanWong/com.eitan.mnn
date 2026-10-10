namespace MNN.Unity
{
    public sealed class MNNLlmResult
    {
        public string Text { get; }

        public int GeneratedTokens { get; }

        public bool ReachedTokenLimit { get; }

        internal MNNLlmResult(string text, int tokens, bool limited)
        {
            Text = text;
            GeneratedTokens = tokens;
            ReachedTokenLimit = limited;
        }
    }
}
