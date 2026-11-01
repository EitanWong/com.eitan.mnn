namespace MNN.Unity
{
    /// <summary>One stateful VAD result; frame positions count 16 kHz mono samples within this stream.</summary>
    public readonly struct MNNRealtimeVadUpdate
    {
        public long StartSample { get; }
        public float SpeechProbability { get; }
        public bool IsSpeechActive { get; }
        public bool SpeechStarted { get; }
        public bool SpeechEnded { get; }

        internal MNNRealtimeVadUpdate(long startSample, float probability, bool active, bool started, bool ended)
        {
            StartSample = startSample;
            SpeechProbability = probability;
            IsSpeechActive = active;
            SpeechStarted = started;
            SpeechEnded = ended;
        }
    }
}
