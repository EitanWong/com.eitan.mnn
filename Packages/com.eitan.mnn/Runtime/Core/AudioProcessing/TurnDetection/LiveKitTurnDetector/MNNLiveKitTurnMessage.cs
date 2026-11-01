using System;

namespace MNN.Unity
{
    public sealed class MNNLiveKitTurnMessage
    {
        public readonly string Role;
        public readonly string Content;
        public MNNLiveKitTurnMessage(string role, string content)
        {
            Role = role ?? throw new ArgumentNullException(nameof(role));
            Content = content ?? throw new ArgumentNullException(nameof(content));
        }
    }
}
