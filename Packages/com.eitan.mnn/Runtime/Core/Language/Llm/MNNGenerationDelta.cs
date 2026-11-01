namespace MNN.Unity
{
    /// <summary>An immutable fragment containing only newly decoded complete UTF-8 text.
    /// Delivered synchronously on the inference caller's thread, not the Unity main thread.</summary>
    public readonly struct MNNGenerationDelta
    {
        /// <summary>New text since the previous callback. Append it once to the reply.</summary>
        public string Text { get; }

        /// <summary>Total generated tokens so far, including tokens that emitted no text.</summary>
        public int GeneratedTokens { get; }

        internal MNNGenerationDelta(string text, int tokens)
        {
            Text = text;
            GeneratedTokens = tokens;
        }
    }
}
