using System;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    public interface IMNNConversationProvider : IDisposable
    {
        MNNRealtimeCapabilities Capabilities { get; }
        Task PrepareAsync(CancellationToken token);
    }
}
