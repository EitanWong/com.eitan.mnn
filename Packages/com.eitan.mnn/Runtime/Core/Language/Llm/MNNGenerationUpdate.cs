using System;

namespace MNN.Unity
{
    /// <summary>Immutable cumulative snapshot delivered on the inference caller's thread.</summary>
    public sealed class MNNGenerationUpdate
    {
        public string Text { get; }

        public int GeneratedTokens { get; }

        /// <summary>Create a cumulative provider update with a nonnegative generated-token count.</summary>
        public MNNGenerationUpdate(string text, int tokens)
        {
            if (tokens < 0) throw new ArgumentOutOfRangeException(nameof(tokens));
            Text = text;
            GeneratedTokens = tokens;
        }
    }
}
