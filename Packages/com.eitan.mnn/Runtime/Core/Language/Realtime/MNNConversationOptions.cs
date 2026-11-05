using System;
using System.Collections.Generic;
namespace MNN.Unity
{
    /// <summary>Immutable-at-construction limits for the shared conversation engine. Providers own model settings.</summary>
    public sealed class MNNConversationOptions
    {
        public bool speechOutput = true, filterVoiceInput, endRealtimeOnExitIntent = true;
        public int minimumSpeechMilliseconds = 240, silenceMilliseconds = 550, turnRetryMilliseconds = 500;
        public int maximumUtteranceSeconds = 30, maximumHistoryMessages = 64, maximumResponseCharacters = 65536;
        public int speechSegmentCharacters = 120, maximumResponseSeconds = 600, realtimeInactivitySeconds = 60;
        public float energyThreshold = .025f;
        public string instructions = "You are a helpful voice assistant.";
        public IReadOnlyList<string> SpeechLanguages = Array.Empty<string>();
        internal MNNConversationOptions Snapshot()
        {
            if (minimumSpeechMilliseconds < 20 || minimumSpeechMilliseconds > 2000 || silenceMilliseconds < 100 || silenceMilliseconds > 5000 ||
                turnRetryMilliseconds < 100 || turnRetryMilliseconds > 5000 || maximumUtteranceSeconds < 2 || maximumUtteranceSeconds > MNNRealtimeUtteranceBuffer.MaximumSeconds ||
                maximumHistoryMessages < 2 || maximumHistoryMessages > 512 || maximumResponseCharacters < 128 || maximumResponseCharacters > 65536 ||
                speechSegmentCharacters < 8 || speechSegmentCharacters > 512 || maximumResponseSeconds < 5 || maximumResponseSeconds > 1800 ||
                realtimeInactivitySeconds < 0 || realtimeInactivitySeconds > 1800 || float.IsNaN(energyThreshold) || energyThreshold <= 0 || energyThreshold > 1 ||
                instructions == null || instructions.Length > 16384) throw new ArgumentException("Conversation limits are outside supported ranges.");
            var copy = (MNNConversationOptions)MemberwiseClone();
            copy.SpeechLanguages = Array.AsReadOnly(new List<string>(SpeechLanguages ?? Array.Empty<string>()).ToArray());
            return copy;
        }
    }
}
