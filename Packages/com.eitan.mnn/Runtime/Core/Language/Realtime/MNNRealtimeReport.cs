using System;

namespace MNN.Unity
{
    public sealed class MNNRealtimeReport
    {
        public bool RequiresMeasuredSpeech { get; }
        /// <summary>False retains the input topology while bypassing speech generation and TTS qualification.</summary>
        public bool ProducesSpeech { get; }
        public MNNRealtimeMode Mode { get; }
        public MNNSpeechRoute SpeechRoute { get; }
        public MNNRealtimeRoute Route { get; }
        public bool UsesSpeechRecognition => Route == MNNRealtimeRoute.CascadedSpeech;
        public MNNRealtimeFeature MissingModel { get; }
        public MNNRealtimeFeature MissingDevice { get; }
        public MNNRealtimeFeature MissingTts { get; }
        public MNNRealtimeFeature MissingAsr { get; }
        public MNNRealtimeFeature MissingVad { get; }
        public MNNRealtimeCapabilities AsrCapabilities { get; }
        public MNNRealtimeCapabilities VadCapabilities { get; }
        public string ModelLimitation { get; }
        public string DeviceLimitation { get; }
        public string TtsLimitation { get; }
        public string ModelContract { get; }
        public string ModelEvidence { get; }
        public string DeviceContract { get; }
        public string DeviceEvidence { get; }
        public string TtsContract { get; }
        public string TtsEvidence { get; }
        public double? ModelSustainedRtf { get; }
        public double ModelSustainedRtfWindowAudioSeconds { get; }
        public double? TtsSustainedRtf { get; }
        public double TtsSustainedRtfWindowAudioSeconds { get; }
        public bool CanStart => (MissingModel | MissingDevice | MissingTts | MissingAsr | MissingVad) == MNNRealtimeFeature.None;

        internal MNNRealtimeReport(MNNRealtimeRoute route, MNNRealtimeMode mode, MNNSpeechRoute speechRoute,
            MNNRealtimeFeature model, MNNRealtimeFeature device, MNNRealtimeFeature tts,
            MNNRealtimeCapabilities modelCapabilities, MNNRealtimeCapabilities deviceCapabilities,
            MNNRealtimeCapabilities ttsCapabilities, MNNRealtimeFeature asr = MNNRealtimeFeature.None,
            MNNRealtimeFeature vad = MNNRealtimeFeature.None, MNNRealtimeCapabilities asrCapabilities = null,
            MNNRealtimeCapabilities vadCapabilities = null, bool requireMeasuredSpeech = true, bool producesSpeech = true)
        {
            RequiresMeasuredSpeech = requireMeasuredSpeech;
            ProducesSpeech = producesSpeech;
            Route = route;
            Mode = mode;
            SpeechRoute = speechRoute;
            MissingModel = model;
            MissingDevice = device;
            MissingTts = tts;
            MissingAsr = asr;
            MissingVad = vad;
            AsrCapabilities = asrCapabilities;
            VadCapabilities = vadCapabilities;
            ModelLimitation = modelCapabilities.Limitation;
            DeviceLimitation = deviceCapabilities.Limitation;
            TtsLimitation = ttsCapabilities?.Limitation;
            ModelContract = modelCapabilities.Contract;
            ModelEvidence = modelCapabilities.Evidence;
            DeviceContract = deviceCapabilities.Contract;
            DeviceEvidence = deviceCapabilities.Evidence;
            TtsContract = ttsCapabilities?.Contract;
            TtsEvidence = ttsCapabilities?.Evidence;
            ModelSustainedRtf = modelCapabilities.SustainedRtf;
            ModelSustainedRtfWindowAudioSeconds = modelCapabilities.SustainedRtfWindowAudioSeconds;
            TtsSustainedRtf = ttsCapabilities?.SustainedRtf;
            TtsSustainedRtfWindowAudioSeconds = ttsCapabilities?.SustainedRtfWindowAudioSeconds ?? 0;
        }

        public void RequireSupported()
        {
            if (!CanStart)
                throw new NotSupportedException(ToString());
        }

        public override string ToString() => CanStart ?
            $"{Route}: required backend capabilities are present; " +
            (!ProducesSpeech ? "text replies; speech synthesis is disabled." :
            RequiresMeasuredSpeech ? "device performance qualification is separate." : "interactive preview; sustained realtime performance is unverified.") :
            $"{Route} unavailable. Model: {MissingModel}. Device: {MissingDevice}. TTS: {MissingTts}. " +
            $"ASR: {MissingAsr}. VAD: {MissingVad}. {ModelLimitation} {DeviceLimitation} {TtsLimitation} " +
            $"{AsrCapabilities?.Limitation} {VadCapabilities?.Limitation}";
    }
}
