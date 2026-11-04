using System;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    public interface IMNNConversationAsrProvider : IMNNConversationProvider
    {
        Task<string> TranscribeAsync(float[] audio, int sampleRate, CancellationToken token);
    }
}
