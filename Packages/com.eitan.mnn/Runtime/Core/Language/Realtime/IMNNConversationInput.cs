using System;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    public interface IMNNConversationInput
    {
        bool Realtime { get; }
        bool Recording { get; }
        int Revision { get; }
        int RealtimeRevision { get; }
        bool Consume(float[] frame, bool speechActive = false, long enqueuedMonotonicTicks = 0, bool discontinuity = false);
        bool TryAcceptRealtime(int revision, Action accept);
    }
}
