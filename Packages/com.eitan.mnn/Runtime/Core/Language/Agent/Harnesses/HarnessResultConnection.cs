using System;

namespace MNN.Samples.SiriAI.Harnesses
{
    /// <summary>Captured client and conversation scope. Availability is supplied by the application.</summary>
    public sealed class HarnessResultConnection
    {
        public RemoteTaskClient Client { get; }
        public string ConversationId { get; }
        public HarnessFrontendScope Scope { get; }
        public HarnessResultConnection(RemoteTaskClient client, HarnessFrontendScope scope)
            : this(client, scope?.ConversationId)
        { Scope = scope; }
        public HarnessResultConnection(RemoteTaskClient client, string conversationId)
        {
            Client = client ?? throw new ArgumentNullException(nameof(client));
            ConversationId = HarnessText.Required(conversationId, 128);
        }
    }
}
