using MNN.Unity;

namespace MNN.Examples
{
    /// <summary>Compatibility entry point; application ownership lives in the reusable MNN package.</summary>
    public sealed class MNNStandaloneConversation : MNNConversationApplication
    {
        public MNNStandaloneConversation(MNNRealtimeDeviceBinding binding, MNNRealtimeCapabilities deviceCapabilities,
            MNNConversationRegistrations registrations, string transcriptDirectory,
            MNNConversationOptions options = null, MNNRealtimeDeviceSessionOptions deviceOptions = null)
            : base(binding, deviceCapabilities, registrations, transcriptDirectory, options, deviceOptions) { }
    }
}
