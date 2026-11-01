using System;

namespace MNN.Unity
{
    public enum MNNRealtimeExecution { LocalMnn, RemoteApi, Managed }

    public enum MNNRealtimeMode { InterruptibleConversation, NativeDuplex }
    public enum MNNSpeechRoute { OmniSpeech, IndependentTts }

    [Flags]
    public enum MNNRealtimeFeature
    {
        None = 0,
        SupportedAbi = 1,
        InProcessMnn = 2,
        MemoryAudioInput = 4,
        ContinuousUnderstanding = 8,
        BoundedScheduling = 16,
        BoundedCancellation = 32,
        ContextRecovery = 64,
        StreamingSpeech = 128,
        StableSpokenText = 256,
        IndependentTts = 512,
        DuplexDevice = 1024,
        EchoControl = 2048,
        PresentationClock = 4096,
        HeadphoneRoute = 8192,
        SustainedRtfBelowOne = 16384,
        TextInput = 32768,
        SpeechRecognition = 65536,
        VoiceActivityDetection = 131072,
        /// <summary>Produces a complete waveform; no progressive PCM or throughput claim.</summary>
        BufferedSpeech = 262144,
        /// <summary>Capture is gated during response production and presentation.</summary>
        HalfDuplexDevice = 524288
    }

    /// <summary>Evidence supplied by an implemented backend; the preflight never infers it from a model name.</summary>
    public sealed class MNNRealtimeCapabilities
    {
        public MNNRealtimeExecution Execution { get; }
        public const double MinimumRtfWindowSeconds = 1.0;
        public MNNRealtimeFeature Features { get; }
        public string Contract { get; }
        public string Evidence { get; }
        public string Limitation { get; }
        public double? SustainedRtf { get; }
        public double SustainedRtfWindowAudioSeconds { get; }

        public MNNRealtimeCapabilities(MNNRealtimeFeature features, string contract, string evidence,
            string limitation = null, double? sustainedRtf = null, double sustainedRtfWindowAudioSeconds = 0,
            MNNRealtimeExecution execution = MNNRealtimeExecution.LocalMnn)
        {
            if (string.IsNullOrWhiteSpace(contract))
                throw new ArgumentException("A versioned backend contract is required.", nameof(contract));
            if (features != MNNRealtimeFeature.None && string.IsNullOrWhiteSpace(evidence))
                throw new ArgumentException("Claimed features require a validation evidence reference.", nameof(evidence));
            if (sustainedRtf.HasValue &&
                (double.IsNaN(sustainedRtf.Value) || double.IsInfinity(sustainedRtf.Value) || sustainedRtf.Value < 0))
                throw new ArgumentOutOfRangeException(nameof(sustainedRtf));
            if (!sustainedRtf.HasValue && sustainedRtfWindowAudioSeconds != 0)
                throw new ArgumentException("An RTF window requires an RTF measurement.", nameof(sustainedRtfWindowAudioSeconds));
            if (sustainedRtf.HasValue &&
                (double.IsNaN(sustainedRtfWindowAudioSeconds) || double.IsInfinity(sustainedRtfWindowAudioSeconds) || sustainedRtfWindowAudioSeconds <= 0))
                throw new ArgumentOutOfRangeException(nameof(sustainedRtfWindowAudioSeconds));
            if (sustainedRtf.HasValue && string.IsNullOrWhiteSpace(evidence))
                throw new ArgumentException("RTF measurements require a validation evidence reference.", nameof(evidence));

            if ((features & MNNRealtimeFeature.SustainedRtfBelowOne) != 0 &&
                (!sustainedRtf.HasValue || sustainedRtf.Value >= 1 ||
                 sustainedRtfWindowAudioSeconds < MinimumRtfWindowSeconds))
                throw new ArgumentException("SustainedRtfBelowOne requires measured RTF < 1 over at least one second of generated audio.", nameof(features));

            if (sustainedRtf.HasValue && sustainedRtf.Value < 1 &&
                sustainedRtfWindowAudioSeconds >= MinimumRtfWindowSeconds)
                features |= MNNRealtimeFeature.SustainedRtfBelowOne;
            else
                features &= ~MNNRealtimeFeature.SustainedRtfBelowOne;

            if (!Enum.IsDefined(typeof(MNNRealtimeExecution), execution))
                throw new ArgumentOutOfRangeException(nameof(execution));
            Execution = execution;
            Features = features;
            Contract = contract;
            Evidence = evidence;
            Limitation = limitation;
            SustainedRtf = sustainedRtf;
            SustainedRtfWindowAudioSeconds = sustainedRtfWindowAudioSeconds;
        }
    }
}
