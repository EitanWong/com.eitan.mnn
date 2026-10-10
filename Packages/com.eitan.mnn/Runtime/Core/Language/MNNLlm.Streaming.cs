using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    public sealed partial class MNNLlm
    {
        /// <summary>Real incremental decode through the official response/generate API.
        /// Callbacks run synchronously on the caller thread; marshal UI updates to the main
        /// thread. Cancellation is observed between tokens and before speech generation;
        /// it cannot interrupt prefill or a native waveform call. Existing platform/ABI limits apply.</summary>
        public MNNMultimodalResult GenerateConversationStreaming(IReadOnlyList<MNNChatMessage> messages, Action<MNNGenerationUpdate> onProgress, int maxNewTokens = 128, string imagePath = null, string audioPath = null, bool generateSpeech = false, int maxAudioTokens = 256, CancellationToken cancellationToken = default)
        {
            if (onProgress == null)
                throw new ArgumentNullException(nameof(onProgress));
            return GenerateConversation(messages, maxNewTokens, imagePath, audioPath, generateSpeech, maxAudioTokens, onProgress, cancellationToken);
        }

        public Task<MNNMultimodalResult> GenerateConversationStreamingAsync(IReadOnlyList<MNNChatMessage> messages, Action<MNNGenerationUpdate> onProgress, int maxNewTokens = 128, string imagePath = null, string audioPath = null, bool generateSpeech = false, int maxAudioTokens = 256, CancellationToken cancellationToken = default)
        {
            if (messages == null)
                throw new ArgumentNullException(nameof(messages));
            var copy = new MNNChatMessage[messages.Count];
            for (int i = 0; i < copy.Length; ++i)
                copy[i] = messages[i];
            return Task.Run(() => GenerateConversationStreaming(copy, onProgress, maxNewTokens, imagePath, audioPath, generateSpeech, maxAudioTokens, cancellationToken));
        }
    }
}
