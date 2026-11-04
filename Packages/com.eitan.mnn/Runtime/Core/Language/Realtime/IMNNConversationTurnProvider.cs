using System;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    public interface IMNNConversationTurnProvider : IMNNConversationProvider
    {
        Task<bool> IsTurnCompleteAsync(float[] utterance, int sampleRate, CancellationToken token);
    }
    public interface IMNNConversationTranscriptTurnProvider : IMNNConversationTurnProvider
    {
        bool RequiresTranscript { get; }
        Task<bool> IsTurnCompleteAsync(float[] utterance, int sampleRate, string transcript, CancellationToken token);
    }
}
