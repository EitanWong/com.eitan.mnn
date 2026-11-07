using System;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    public interface IMNNConversationVadProvider : IMNNConversationProvider
    {
        bool IsSpeech(float[] monoFrame, int count, int sampleRate);
        void Reset();
    }
}
