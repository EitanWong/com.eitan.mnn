using System;
using System.Collections.Generic;
using MNN.Unity.Interop;

namespace MNN.Unity
{
    public sealed partial class MNNLlm
    {
        /// <summary>Counts the official rendered text chat prompt, including role/template
        /// tokens. This does not change generation or cached conversation state.</summary>
        public int CountConversationTokens(IReadOnlyList<MNNChatMessage> messages)
        {
            if (messages == null || messages.Count == 0) throw new ArgumentException("A conversation is required.", nameof(messages));
            var copy = new MNNChatMessage[messages.Count];
            for (int i = 0; i < copy.Length; ++i)
            {
                copy[i] = messages[i] ?? throw new ArgumentException("Messages cannot contain null.", nameof(messages));
                if (MNNConversationContext.ContainsMedia(copy[i].Content))
                    throw new NotSupportedException("Text budgeting does not count image, audio or video embeddings.");
                if (copy[i].Role == MNNChatRole.System && i != 0)
                    throw new ArgumentException("The system message must be first.", nameof(messages));
            }
            lock (_gate)
            {
                ThrowIfDisposed();
                using (MNNInterop.BeginNativeCall())
                    return CountRenderedConversation(copy);
            }
        }

        /// <summary>Fits text history using exact official template/tokenizer counts.
        /// Preserves the first system message and latest user message, removing oldest
        /// complete turns only when needed. Supply the actual model context limit and
        /// reserve the same output budget used for generation. Media is rejected.</summary>
        public MNNConversationContext PrepareConversation(IReadOnlyList<MNNChatMessage> messages,
            int contextTokens, int reservedOutputTokens = 128)
        {
            lock (_gate)
            {
                ThrowIfDisposed();
                using (MNNInterop.BeginNativeCall())
                    return MNNConversationContext.Prepare(messages, contextTokens, reservedOutputTokens, CountRenderedConversation);
            }
        }

        private int CountRenderedConversation(IReadOnlyList<MNNChatMessage> messages)
            => _tokenCounts.Get(MNNInterop.RenderConversation(_handle, messages), _countNativeTokens);

        private int CountNativeTokens(string text) => MNNInterop.MNN_Llm_tokenize(_handle, text);
    }
}
