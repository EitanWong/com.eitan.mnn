using System;
using System.Collections.Generic;
namespace MNN.Unity
{
    public sealed class MNNConversationStagePlan
    {
        public bool TurnInput { get; private set; }
        public bool Tts { get; private set; }
        public bool Asr { get; private set; }
        public bool Vad { get; private set; }
        public bool Turn { get; private set; }
        public static MNNConversationStagePlan ForRoute(MNNConversationOptions config, MNNRealtimeRoute route, bool allowBufferedSpeech = false, bool providerVad = false, bool providerTurn = false, bool turnRequiresTranscript = false)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (!Enum.IsDefined(typeof(MNNRealtimeRoute), route)) throw new ArgumentOutOfRangeException(nameof(route));
            bool turnBased = route == MNNRealtimeRoute.CascadedSpeech || route == MNNRealtimeRoute.AudioTextToSpeech || route == MNNRealtimeRoute.EndToEndSpeech && allowBufferedSpeech;
            return new MNNConversationStagePlan
            {
                TurnInput = turnBased,
                Tts = config.speechOutput && (route == MNNRealtimeRoute.NativeDuplexTextToSpeech || route == MNNRealtimeRoute.AudioTextToSpeech || route == MNNRealtimeRoute.CascadedSpeech),
                Asr = route == MNNRealtimeRoute.CascadedSpeech || turnBased && (config.filterVoiceInput || config.endRealtimeOnExitIntent || turnRequiresTranscript),
                Vad = turnBased && providerVad,
                Turn = turnBased && providerTurn
            };
        }
        public static MNNConversationStagePlan ForRoutes(MNNConversationOptions config, ICollection<MNNRealtimeRoute> excluded, bool allowBufferedSpeech = false, bool providerVad = false, bool providerTurn = false, bool turnRequiresTranscript = false)
        {
            var result = new MNNConversationStagePlan();
            foreach (MNNRealtimeRoute route in Enum.GetValues(typeof(MNNRealtimeRoute)))
            {
                if (excluded?.Contains(route) == true) continue;
                var plan = ForRoute(config, route, allowBufferedSpeech, providerVad, providerTurn, turnRequiresTranscript);
                result.TurnInput |= plan.TurnInput; result.Tts |= plan.Tts; result.Asr |= plan.Asr; result.Vad |= plan.Vad; result.Turn |= plan.Turn;
            }
            return result;
        }
    }
}
