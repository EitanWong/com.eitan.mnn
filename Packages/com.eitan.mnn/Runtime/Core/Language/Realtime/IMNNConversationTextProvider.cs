using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    public interface IMNNConversationTextProvider : IMNNConversationProvider
    {
        Task GenerateAsync(IReadOnlyList<MNNOpenAIMessage> history, float[] audio, int sampleRate, Func<string, Task> textDelta, CancellationToken token);
    }
    public interface IMNNConversationResponseProvider : IMNNConversationTextProvider
    {
        Task GenerateResponseAsync(IReadOnlyList<MNNOpenAIMessage> history, string instructions, Func<string, Task> textDelta, CancellationToken token);
    }
    /// <summary>Optional short text generation on the same model, without planning, tools or speech.</summary>
    public interface IMNNConversationTextAssistanceProvider : IMNNConversationTextProvider
    {
        Task GenerateAssistanceAsync(MNNConversationTextTask task, IReadOnlyList<MNNOpenAIMessage> prompt,
            Func<string, Task> textDelta, CancellationToken token);
    }
}
