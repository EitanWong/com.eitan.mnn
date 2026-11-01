using System;

namespace MNN.Unity
{
    /// <summary>Managed capability evaluation only. Does not load native code, request permissions or download models.</summary>
    public static class MNNRealtimePreflight
    {
        public static MNNRealtimeReport Check(MNNRealtimeMode mode, MNNSpeechRoute route,
            MNNRealtimeCapabilities model, MNNRealtimeCapabilities device, MNNRealtimeCapabilities tts = null,
            MNNRealtimeCapabilities asr = null, MNNRealtimeCapabilities vad = null, bool requireMeasuredSpeech = true, bool allowBufferedSpeech = false)
        {
            if (!Enum.IsDefined(typeof(MNNRealtimeMode), mode))
                throw new ArgumentOutOfRangeException(nameof(mode));
            if (!Enum.IsDefined(typeof(MNNSpeechRoute), route))
                throw new ArgumentOutOfRangeException(nameof(route));
            var topology = mode == MNNRealtimeMode.NativeDuplex
                ? (route == MNNSpeechRoute.OmniSpeech ? MNNRealtimeRoute.NativeDuplexSpeech : MNNRealtimeRoute.NativeDuplexTextToSpeech)
                : (route == MNNSpeechRoute.OmniSpeech ? MNNRealtimeRoute.EndToEndSpeech : MNNRealtimeRoute.AudioTextToSpeech);
            return Check(topology, model, device, tts, asr, vad, requireMeasuredSpeech, allowBufferedSpeech);
        }

        /// <summary>
        /// Checks a specific topology. Buffered speech is opt-in and available only for turn-based
        /// routes with explicit waveform and half-duplex capture capabilities. It never infers
        /// progressive output, continuous understanding or measured throughput.
        /// Audio-model routes do not infer continuous understanding from audio input alone.
        /// </summary>
        public static MNNRealtimeReport Check(MNNRealtimeRoute route, MNNRealtimeCapabilities model,
            MNNRealtimeCapabilities device, MNNRealtimeCapabilities tts = null,
            MNNRealtimeCapabilities asr = null, MNNRealtimeCapabilities vad = null,
            bool requireMeasuredSpeech = true, bool allowBufferedSpeech = false, bool speechOutput = true)
        {
            if (!Enum.IsDefined(typeof(MNNRealtimeRoute), route))
                throw new ArgumentOutOfRangeException(nameof(route));
            if (model == null || device == null)
                throw new ArgumentNullException(model == null ? nameof(model) : nameof(device));
            if (!speechOutput && route != MNNRealtimeRoute.AudioTextToSpeech && route != MNNRealtimeRoute.CascadedSpeech)
                throw new ArgumentException("Text replies require an audio/text or cascaded input topology.", nameof(route));
            bool cascade = route == MNNRealtimeRoute.CascadedSpeech;
            bool buffered = allowBufferedSpeech && (!requireMeasuredSpeech || !speechOutput) &&
                route != MNNRealtimeRoute.NativeDuplexSpeech && route != MNNRealtimeRoute.NativeDuplexTextToSpeech;
            bool turnBasedAudio = route == MNNRealtimeRoute.AudioTextToSpeech || cascade ||
                buffered && route == MNNRealtimeRoute.EndToEndSpeech;
            var mode = route == MNNRealtimeRoute.NativeDuplexSpeech || route == MNNRealtimeRoute.NativeDuplexTextToSpeech
                ? MNNRealtimeMode.NativeDuplex : MNNRealtimeMode.InterruptibleConversation;
            var speechRoute = route == MNNRealtimeRoute.NativeDuplexSpeech || route == MNNRealtimeRoute.EndToEndSpeech
                ? MNNSpeechRoute.OmniSpeech : MNNSpeechRoute.IndependentTts;
            var modelRequired = ExecutionRequirements(model) |
                (buffered ? MNNRealtimeFeature.None : MNNRealtimeFeature.BoundedCancellation) | MNNRealtimeFeature.ContextRecovery |
                (cascade ? MNNRealtimeFeature.TextInput : MNNRealtimeFeature.MemoryAudioInput);
            if (mode == MNNRealtimeMode.NativeDuplex)
                modelRequired |= MNNRealtimeFeature.ContinuousUnderstanding | MNNRealtimeFeature.BoundedScheduling;
            if (speechOutput && speechRoute == MNNSpeechRoute.OmniSpeech)
                modelRequired |= SpeechRequirement(model, buffered) |
                    (requireMeasuredSpeech ? MNNRealtimeFeature.SustainedRtfBelowOne : MNNRealtimeFeature.None);
            else if (speechOutput)
                modelRequired |= MNNRealtimeFeature.StableSpokenText;
            var deviceRequired = MNNRealtimeFeature.DuplexDevice | (speechOutput ? MNNRealtimeFeature.PresentationClock : MNNRealtimeFeature.None);
            bool hasEchoControl = (device.Features & MNNRealtimeFeature.EchoControl) != 0;
            bool qualifiedHeadphones = mode == MNNRealtimeMode.InterruptibleConversation &&
                (device.Features & MNNRealtimeFeature.HeadphoneRoute) != 0;
            if (speechOutput && !hasEchoControl && !qualifiedHeadphones && !(buffered &&
                (device.Features & MNNRealtimeFeature.HalfDuplexDevice) != 0))
                deviceRequired |= MNNRealtimeFeature.EchoControl;
            var ttsRequired = speechOutput && speechRoute == MNNSpeechRoute.IndependentTts ? ExecutionRequirements(tts) |
                MNNRealtimeFeature.IndependentTts |
                SpeechRequirement(tts, buffered) | (buffered ? MNNRealtimeFeature.None : MNNRealtimeFeature.BoundedCancellation) |
                (requireMeasuredSpeech ? MNNRealtimeFeature.SustainedRtfBelowOne : MNNRealtimeFeature.None) : MNNRealtimeFeature.None;
            var asrRequired = cascade ? ExecutionRequirements(asr) |
                MNNRealtimeFeature.MemoryAudioInput | MNNRealtimeFeature.SpeechRecognition |
                (buffered ? MNNRealtimeFeature.None : MNNRealtimeFeature.BoundedCancellation) : MNNRealtimeFeature.None;
            var vadRequired = turnBasedAudio ? ExecutionRequirements(vad) |
                MNNRealtimeFeature.MemoryAudioInput | MNNRealtimeFeature.VoiceActivityDetection |
                MNNRealtimeFeature.BoundedScheduling : MNNRealtimeFeature.None;
            return new MNNRealtimeReport(route, mode, speechRoute, modelRequired & ~model.Features, deviceRequired & ~device.Features,
                ttsRequired & ~(tts?.Features ?? MNNRealtimeFeature.None), model, device,
                speechOutput && speechRoute == MNNSpeechRoute.IndependentTts ? tts : null,
                asrRequired & ~(asr?.Features ?? MNNRealtimeFeature.None),
                vadRequired & ~(vad?.Features ?? MNNRealtimeFeature.None), cascade ? asr : null, turnBasedAudio ? vad : null,
                requireMeasuredSpeech && speechOutput, speechOutput);
        }

        private static MNNRealtimeFeature SpeechRequirement(MNNRealtimeCapabilities capabilities, bool buffered) =>
            buffered && (capabilities?.Features & MNNRealtimeFeature.StreamingSpeech) == 0
                ? MNNRealtimeFeature.BufferedSpeech : MNNRealtimeFeature.StreamingSpeech;

        private static MNNRealtimeFeature ExecutionRequirements(MNNRealtimeCapabilities capabilities) =>
            capabilities == null || capabilities.Execution == MNNRealtimeExecution.LocalMnn
                ? MNNRealtimeFeature.SupportedAbi | MNNRealtimeFeature.InProcessMnn
                : MNNRealtimeFeature.None;

        /// <summary>No currently registered official continuous-audio backend exists. Existing batch APIs are not substituted.</summary>
        public static MNNRealtimeReport CheckBundledBackend(MNNRealtimeMode mode, MNNSpeechRoute route)
        {
            var unavailable = new MNNRealtimeCapabilities(MNNRealtimeFeature.None, "MNN.Unity realtime backend: not implemented", null,
                "The bundled binding does not expose a validated continuous PCM session or cross-platform C++ ABI contract.");
            var device = new MNNRealtimeCapabilities(MNNRealtimeFeature.None, "Duplex device: not registered", null,
                "A platform device with verified echo control and presentation clock is required.");
            var tts = route == MNNSpeechRoute.IndependentTts
                ? new MNNRealtimeCapabilities(MNNRealtimeFeature.None, "Streaming MNN TTS: not registered", null,
                    "No qualified streaming synthesizer is registered for this session.")
                : null;
            return Check(mode, route, unavailable, device, tts);
        }
    }
}
