using System;
using System.Collections.Generic;

namespace MNN.Unity
{
    /// <summary>
    /// Prefers continuous understanding, then model speech, then audio-to-text/TTS, then ASR/text-model/TTS.
    /// Uses one selected model's validated capabilities. Does not infer capabilities from a model name,
    /// download replacements or relax ABI, interruption, echo-control and measured throughput requirements.
    /// </summary>
    public static class MNNRealtimeRouteSelector
    {
        private static readonly MNNRealtimeRoute[] Priority =
        {
            MNNRealtimeRoute.NativeDuplexSpeech,
            MNNRealtimeRoute.NativeDuplexTextToSpeech,
            MNNRealtimeRoute.EndToEndSpeech,
            MNNRealtimeRoute.AudioTextToSpeech,
            MNNRealtimeRoute.CascadedSpeech
        };
        private static readonly MNNRealtimeRoute[] TextReplyPriority =
        {
            MNNRealtimeRoute.AudioTextToSpeech,
            MNNRealtimeRoute.CascadedSpeech
        };

        /// <summary>
        /// Runs before resource acquisition or after old owners have quiesced. Exclude failed routes to
        /// make subsequent selections progress without retry loops. Cancellation, permissions and shared
        /// device/ABI failures are not reasons to blindly replay a response on a different route.
        /// Allocation is bounded by the five topologies and occurs only on the control path.
        /// </summary>
        public static MNNRealtimeRouteSelection SelectBest(MNNRealtimeCapabilities model,
            MNNRealtimeCapabilities device, MNNRealtimeCapabilities tts = null,
            MNNRealtimeCapabilities asr = null, MNNRealtimeCapabilities vad = null,
            IReadOnlyCollection<MNNRealtimeRoute> excludedRoutes = null, bool requireMeasuredSpeech = true, bool allowBufferedSpeech = false,
            bool speechOutput = true)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (device == null) throw new ArgumentNullException(nameof(device));
            var priority = speechOutput ? Priority : TextReplyPriority;
            int excludedMask = 0;
            if (excludedRoutes != null)
            {
                foreach (var excluded in excludedRoutes)
                {
                    if (!Enum.IsDefined(typeof(MNNRealtimeRoute), excluded))
                        throw new ArgumentOutOfRangeException(nameof(excludedRoutes));
                    excludedMask |= 1 << (int)excluded;
                }
            }

            var attempts = new MNNRealtimeReport[priority.Length];
            var excludedSnapshot = new MNNRealtimeRoute[Priority.Length];
            int excludedCount = 0;
            foreach (var route in Priority)
            {
                if ((excludedMask & (1 << (int)route)) != 0)
                    excludedSnapshot[excludedCount++] = route;
            }
            Array.Resize(ref excludedSnapshot, excludedCount);
            int count = 0;
            foreach (var route in priority)
            {
                if ((excludedMask & (1 << (int)route)) != 0)
                    continue;
                var report = MNNRealtimePreflight.Check(route, model, device, tts, asr, vad, requireMeasuredSpeech, allowBufferedSpeech, speechOutput);
                attempts[count++] = report;
                if (report.CanStart)
                    break;
            }
            Array.Resize(ref attempts, count);
            return new MNNRealtimeRouteSelection(attempts, excludedSnapshot);
        }
    }
}
