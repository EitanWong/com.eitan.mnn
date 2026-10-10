namespace MNN.Unity
{
    public sealed class MNNMultimodalResult
    {
        public string Text { get; }

        public int GeneratedTokens { get; }

        public bool ReachedTokenLimit { get; }

        public bool Cancelled { get; }

        /// <summary>Owned mono PCM samples, independent of the next inference call.</summary>
        public float[] Waveform { get; }

        public int SampleRate { get; }

        public long VisionMicroseconds { get; }

        public long AudioMicroseconds { get; }

        public float InputAudioSeconds { get; }

        internal MNNMultimodalResult(string text, int tokens, bool limited, float[] waveform, int rate, long visionUs, long audioUs, float audioSeconds, bool cancelled = false)
        {
            Text = text;
            GeneratedTokens = tokens;
            ReachedTokenLimit = limited;
            Waveform = waveform;
            SampleRate = rate;
            VisionMicroseconds = visionUs;
            AudioMicroseconds = audioUs;
            InputAudioSeconds = audioSeconds;
            Cancelled = cancelled;
        }
    }
}
