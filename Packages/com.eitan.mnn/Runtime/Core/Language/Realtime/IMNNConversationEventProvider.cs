using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    /// <summary>Routing providers implement an explicit response-only path for application events.</summary>
    public interface IMNNConversationEventProvider
    {
        Task GenerateEventResponseAsync(IReadOnlyList<MNNOpenAIMessage> history, MNNConversationEvent input,
            Func<string, Task> delta, CancellationToken token);
    }
}
