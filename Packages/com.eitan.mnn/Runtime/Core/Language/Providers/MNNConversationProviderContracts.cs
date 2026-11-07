using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
namespace MNN.Unity
{
    public interface IMNNConversationRequestPlanner : IMNNConversationTextProvider
    {
        Task PlanRequestAsync(IReadOnlyList<MNNOpenAIMessage> prompt, Func<string, Task> delta, CancellationToken token);
    }
    public interface IMNNConversationProviderDiagnostics { string RuntimeBackend { get; } }
    public interface IMNNConversationTtsDiagnostics { MNNConversationSpeechTimings LastStageTimings { get; } }
}
