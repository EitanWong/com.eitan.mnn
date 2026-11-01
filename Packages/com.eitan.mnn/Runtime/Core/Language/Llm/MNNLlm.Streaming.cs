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

        /// <summary>Streams only newly decoded text without allocating cumulative snapshots.
        /// Append each delta once; the returned result still contains the complete reply.
        /// Callbacks run synchronously on the inference caller's thread. Cancellation and
        /// platform/ABI constraints are the same as GenerateConversationStreaming.</summary>
        public MNNMultimodalResult GenerateConversationStreamingDeltas(IReadOnlyList<MNNChatMessage> messages, Action<MNNGenerationDelta> onDelta, int maxNewTokens = 128, string imagePath = null, string audioPath = null, bool generateSpeech = false, int maxAudioTokens = 256, CancellationToken cancellationToken = default)
        {
            if (onDelta == null)
                throw new ArgumentNullException(nameof(onDelta));
            return GenerateConversationCore(messages, maxNewTokens, imagePath, audioPath, generateSpeech, maxAudioTokens, null, onDelta, cancellationToken);
        }

        /// <summary>Schedules one inference worker for the request. Delta callbacks run on
        /// that worker; marshal Unity UI work to the main thread.</summary>
        public Task<MNNMultimodalResult> GenerateConversationStreamingDeltasAsync(IReadOnlyList<MNNChatMessage> messages, Action<MNNGenerationDelta> onDelta, int maxNewTokens = 128, string imagePath = null, string audioPath = null, bool generateSpeech = false, int maxAudioTokens = 256, CancellationToken cancellationToken = default)
        {
            if (messages == null)
                throw new ArgumentNullException(nameof(messages));
            if (onDelta == null)
                throw new ArgumentNullException(nameof(onDelta));
            var copy = new MNNChatMessage[messages.Count];
            for (int i = 0; i < copy.Length; ++i)
                copy[i] = messages[i];
            return Task.Run(() => GenerateConversationStreamingDeltas(copy, onDelta, maxNewTokens, imagePath, audioPath, generateSpeech, maxAudioTokens, cancellationToken));
        }
    }
}
