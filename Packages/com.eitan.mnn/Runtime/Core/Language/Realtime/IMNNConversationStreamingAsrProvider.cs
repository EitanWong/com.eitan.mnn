using System;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    public interface IMNNConversationStreamingAsrProvider : IMNNConversationAsrProvider
    {
        IMNNConversationRecognitionStream CreateStream();
    }
    public interface IMNNConversationRecognitionStream : IDisposable
    {
        string AcceptWaveform(float[] samples, int offset, int count, CancellationToken token);
        void Reset();
    }
}
