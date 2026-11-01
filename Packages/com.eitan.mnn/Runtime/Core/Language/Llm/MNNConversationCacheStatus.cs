namespace MNN.Unity
{
    /// <summary>Why a text conversation did or did not reuse the previous native state.</summary>
    public enum MNNConversationCacheStatus
    {
        Cold,
        Reused,
        Disabled,
        Media,
        UnsupportedModel,
        UnsupportedRuntime,
        HistoryMismatch,
        Interrupted
    }
}
