using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    public interface IMNNConversationSpeechModelProvider : IMNNConversationProvider
    {
        Task<MNNMultimodalResult> GenerateSpeechAsync(IReadOnlyList<MNNOpenAIMessage> history, Func<string, Task> textDelta, CancellationToken token);
    }
}
