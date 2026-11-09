namespace MNN.Unity
{
    /// <summary>Conversation topology, independent of model names and physical device qualification.</summary>
    public enum MNNRealtimeRoute
    {
        NativeDuplexSpeech,
        NativeDuplexTextToSpeech,
        EndToEndSpeech,
        AudioTextToSpeech,
        CascadedSpeech,
        TextChat
    }
}
