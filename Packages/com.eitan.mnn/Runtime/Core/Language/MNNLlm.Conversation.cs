using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using MNN.Unity.Interop;

namespace MNN.Unity
{
    public sealed partial class MNNLlm
    {
        /// <summary>Uses the model's official chat template with separate roles. Media belongs
        /// to the final user message; previous messages contain text only. Each call resets KV state.
        /// Same platform/ABI and speech-output constraints as GenerateMultimodal.</summary>
        public MNNMultimodalResult GenerateConversation(IReadOnlyList<MNNChatMessage> messages, int maxNewTokens = 128, string imagePath = null, string audioPath = null, bool generateSpeech = false, int maxAudioTokens = 256, Action<MNNGenerationUpdate> onProgress = null, CancellationToken cancellationToken = default)
        {
            if (messages == null || messages.Count == 0)
                throw new ArgumentException("A conversation is required.", nameof(messages));
            if (maxNewTokens < 1 || maxNewTokens > 8192)
                throw new ArgumentOutOfRangeException(nameof(maxNewTokens));
            if (maxAudioTokens < 1 || maxAudioTokens > 2048)
                throw new ArgumentOutOfRangeException(nameof(maxAudioTokens));
            var copy = new MNNChatMessage[messages.Count];
            for (int i = 0; i < copy.Length; ++i)
            {
                var message = messages[i] ?? throw new ArgumentException("Messages cannot contain null.", nameof(messages));
                if (message.Role == MNNChatRole.System && i != 0)
                    throw new ArgumentException("The system message must be first.", nameof(messages));
                copy[i] = message;
            }

            if (copy[copy.Length - 1].Role != MNNChatRole.User || string.IsNullOrWhiteSpace(copy[copy.Length - 1].Content))
                throw new ArgumentException("A conversation must end with a nonempty user message.", nameof(messages));
            imagePath = ValidateMediaPath(imagePath);
            audioPath = ValidateMediaPath(audioPath);
            lock (_gate)
            {
                ThrowIfDisposed();
                var required = (imagePath != null ? MNNModelCapabilities.Vision : 0) | (audioPath != null ? MNNModelCapabilities.AudioInput : 0) | (generateSpeech ? MNNModelCapabilities.SpeechOutput : 0);
                if ((Capabilities & required) != required)
                    throw new NotSupportedException("This model does not support the requested media or speech output.");
                cancellationToken.ThrowIfCancellationRequested();
                RequireSpeechCompatibility(generateSpeech);
                if (onProgress != null)
                {
                    bool progressDelivered = false;
                    Action<MNNGenerationUpdate> progress = update =>
                    {
                        // Never replay delivered tokens or application callback failures.
                        progressDelivered = true;
                        onProgress(update);
                    };
                    return UseNative(handle => MNNInterop.GenerateChat(handle, copy, imagePath, audioPath, generateSpeech, maxAudioTokens, maxNewTokens, progress, cancellationToken), () => !progressDelivered && !cancellationToken.IsCancellationRequested);
                }

                return UseNative(handle => MNNInterop.GenerateChat(handle, copy, imagePath, audioPath, generateSpeech, maxAudioTokens, maxNewTokens, null, cancellationToken), () => !cancellationToken.IsCancellationRequested);
            }
        }

        public Task<MNNMultimodalResult> GenerateConversationAsync(IReadOnlyList<MNNChatMessage> messages, int maxNewTokens = 128, string imagePath = null, string audioPath = null, bool generateSpeech = false, int maxAudioTokens = 256)
        {
            // Snapshot the collection before scheduling so UI history edits cannot race marshalling.
            if (messages == null)
                throw new ArgumentNullException(nameof(messages));
            var copy = new MNNChatMessage[messages.Count];
            for (int i = 0; i < copy.Length; ++i)
                copy[i] = messages[i];
            return Task.Run(() => GenerateConversation(copy, maxNewTokens, imagePath, audioPath, generateSpeech, maxAudioTokens));
        }
    }
}
