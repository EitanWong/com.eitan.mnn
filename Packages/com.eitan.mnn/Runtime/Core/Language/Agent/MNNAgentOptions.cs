using System;
namespace MNN.Unity
{
    public enum MNNAgentRoutingStrategy { Policy, ModelPlanner }
    public sealed class MNNAgentOptions
    {
        public MNNAgentRoutingStrategy Strategy = MNNAgentRoutingStrategy.ModelPlanner;
        public bool AllowCloud, UseRouter, KnowledgeAnswers = true;
        public string LocalCapabilities = "Short conversation, rewriting and extraction from short supplied text.";
        public int LocalContextCharacters = 12000, CloudContextCharacters = 24000, CloudHistoryMessages, PlanningSeconds = 10;
        internal MNNAgentOptions Snapshot()
        {
            if (!Enum.IsDefined(typeof(MNNAgentRoutingStrategy), Strategy) || LocalContextCharacters < 512 || LocalContextCharacters > 65536 ||
                CloudContextCharacters < LocalContextCharacters || CloudContextCharacters > 65536 || CloudHistoryMessages < 0 || CloudHistoryMessages > 32 ||
                PlanningSeconds < 1 || PlanningSeconds > 30 || string.IsNullOrWhiteSpace(LocalCapabilities) || LocalCapabilities.Length > 2048)
                throw new ArgumentException("Agent context and planning limits are outside supported ranges.");
            return (MNNAgentOptions)MemberwiseClone();
        }
    }
}
