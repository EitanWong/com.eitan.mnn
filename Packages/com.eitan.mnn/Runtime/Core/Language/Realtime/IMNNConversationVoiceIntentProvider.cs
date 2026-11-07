using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    public interface IMNNConversationVoiceIntentProvider : IMNNConversationTextProvider
    {
        Task ClassifyVoiceIntentAsync(IReadOnlyList<MNNOpenAIMessage> prompt, Func<string, Task> textDelta, CancellationToken token);
    }
}
