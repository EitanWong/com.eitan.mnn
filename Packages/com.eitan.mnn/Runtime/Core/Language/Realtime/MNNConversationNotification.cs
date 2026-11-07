namespace MNN.Unity
{
    public readonly struct MNNConversationNotification
    {
        public MNNResponseId Response { get; }
        public MNNConversationStage Stage { get; }
        public string Text { get; }
        public bool Assistant => Response.IsValid;
        internal MNNConversationNotification(MNNResponseId response, MNNConversationStage stage, string text)
        { Response = response; Stage = stage; Text = text; }
    }
}
