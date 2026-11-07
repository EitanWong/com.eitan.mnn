using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    public interface IMNNConversationTtsProvider : IMNNConversationProvider
    {
        int SampleRate { get; }
        Task SynthesizeAsync(string text, Func<float[], int, Task> pcm, CancellationToken token);
    }
    public interface IMNNConversationSpeechLanguages
    {
        IReadOnlyList<string> SupportedSpeechLanguages { get; }
    }
}
