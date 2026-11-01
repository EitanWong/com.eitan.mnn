namespace MNN.Unity
{
    /// <summary>Metadata for one fixed 512-sample mono 16 kHz model input frame.</summary>
    public readonly struct MNNRealtimeModelInputInfo
    {
        /// <summary>The VAD result consumed with this PCM frame, on the input owner's thread.</summary>
        public MNNRealtimeVadUpdate Vad => new MNNRealtimeVadUpdate(StartSample,
            SpeechProbability, IsSpeechActive, SpeechStarted, SpeechEnded);
        public long StartSample { get; }
        public long Sequence { get; }
        public float SpeechProbability { get; }
        public bool IsSpeechActive { get; }
        public bool SpeechStarted { get; }
        public bool SpeechEnded { get; }
        public bool Discontinuity { get; }
        /// <summary>Enqueue time in current-process Stopwatch ticks, including when expiration is disabled.</summary>
        public long EnqueuedMonotonicTicks { get; }

        internal MNNRealtimeModelInputInfo(MNNRealtimeVadUpdate vad, long sequence, bool discontinuity,
            long enqueuedMonotonicTicks)
        {
            StartSample = vad.StartSample;
            Sequence = sequence;
            SpeechProbability = vad.SpeechProbability;
            IsSpeechActive = vad.IsSpeechActive;
            SpeechStarted = vad.SpeechStarted;
            SpeechEnded = vad.SpeechEnded;
            Discontinuity = discontinuity;
            EnqueuedMonotonicTicks = enqueuedMonotonicTicks;
        }

        internal MNNRealtimeModelInputInfo WithGap() => new MNNRealtimeModelInputInfo(
            new MNNRealtimeVadUpdate(StartSample, SpeechProbability, IsSpeechActive, SpeechStarted, SpeechEnded),
            Sequence, true, EnqueuedMonotonicTicks);
    }
}
