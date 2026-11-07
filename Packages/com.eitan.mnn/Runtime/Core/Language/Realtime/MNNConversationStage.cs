namespace MNN.Unity
{
    public enum MNNConversationStage
    {
        Idle,
        Loading,
        ModelReady,
        Listening,
        Recognizing,
        Generating,
        Synthesizing,
        Speaking,
        ReplyCompleted,
        Interrupted,
        Error,
        Stopped
    }
}
