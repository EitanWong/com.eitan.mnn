namespace MNN.Unity
{
    /// <summary>A text snapshot and newly available aligned audio codes. Callbacks run on the inference caller's thread.</summary>
    public sealed class MNNMiniMindOUpdate
    {
        public string Text { get; }
        public int GeneratedTokens { get; }
        public int[] AudioFrame { get; }
        /// <summary>New mono 24 kHz PCM samples, without samples emitted by earlier callbacks.</summary>
        public float[] Waveform { get; }
        internal MNNMiniMindOUpdate(string text, int[] frame, float[] waveform, int tokens)
        {
            GeneratedTokens = tokens; Text = text; AudioFrame = frame; Waveform = waveform;
        }
    }
}
