using System;

namespace MNN.Unity
{
    /// <summary>Bounded MiniMind-o generation using the released closed-thinking sampling schedule.</summary>
    public sealed class MNNMiniMindOOptions
    {
        public int MaxNewTokens { get; set; } = 128;
        public int Seed { get; set; } = 42;
        /// <summary>Zero selects greedy text. Speech uses the released temperature 0.2/top-k 50.</summary>
        public float Temperature { get; set; } = 0.7f;
        public float TopP { get; set; } = 0.85f;
        public bool GenerateSpeech { get; set; } = true;

        internal MNNMiniMindOOptions Snapshot()
        {
            if (MaxNewTokens < 1 || MaxNewTokens > 512) throw new ArgumentOutOfRangeException(nameof(MaxNewTokens));
            if (float.IsNaN(Temperature) || Temperature < 0 || Temperature > 2) throw new ArgumentOutOfRangeException(nameof(Temperature));
            if (float.IsNaN(TopP) || TopP <= 0 || TopP > 1) throw new ArgumentOutOfRangeException(nameof(TopP));
            return (MNNMiniMindOOptions)MemberwiseClone();
        }
    }
}
