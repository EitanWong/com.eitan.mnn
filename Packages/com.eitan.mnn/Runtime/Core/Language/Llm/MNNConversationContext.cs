using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MNN.Unity
{
    /// <summary>A text conversation fitted to an explicit model context limit.
    /// Oldest complete turns may be removed. No summarization or content rewriting occurs.</summary>
    public sealed class MNNConversationContext
    {
        public IReadOnlyList<MNNChatMessage> Messages { get; }
        /// <summary>Exact token count after applying this model's official chat template.</summary>
        public int InputTokens { get; }
        public int RemovedMessages { get; }

        private MNNConversationContext(MNNChatMessage[] messages, int inputTokens, int removed)
        {
            Messages = new ReadOnlyCollection<MNNChatMessage>(messages);
            InputTokens = inputTokens;
            RemovedMessages = removed;
        }

        internal static MNNConversationContext Prepare(IReadOnlyList<MNNChatMessage> messages, int contextTokens,
            int outputTokens, Func<IReadOnlyList<MNNChatMessage>, int> count)
        {
            if (messages == null || messages.Count == 0) throw new ArgumentException("A conversation is required.", nameof(messages));
            if (contextTokens < 1) throw new ArgumentOutOfRangeException(nameof(contextTokens));
            if (outputTokens < 1 || outputTokens >= contextTokens) throw new ArgumentOutOfRangeException(nameof(outputTokens));
            var copy = new MNNChatMessage[messages.Count];
            for (int i = 0; i < copy.Length; ++i)
            {
                var message = messages[i] ?? throw new ArgumentException("Messages cannot contain null.", nameof(messages));
                if (message.Role == MNNChatRole.System && i != 0) throw new ArgumentException("The system message must be first.", nameof(messages));
                if (ContainsMedia(message.Content)) throw new NotSupportedException("Text budgeting does not count image, audio or video embeddings.");
                copy[i] = message;
            }
            int first = copy[0].Role == MNNChatRole.System ? 1 : 0;
            if (first >= copy.Length || copy[first].Role != MNNChatRole.User ||
                copy[copy.Length - 1].Role != MNNChatRole.User || string.IsNullOrWhiteSpace(copy[copy.Length - 1].Content))
                throw new ArgumentException("History must begin with a user turn and end with a nonempty user message.", nameof(messages));
            int start = first;
            while (true)
            {
                var candidate = new MNNChatMessage[first + copy.Length - start];
                if (first != 0) candidate[0] = copy[0];
                Array.Copy(copy, start, candidate, first, copy.Length - start);
                int tokens = count(candidate);
                if (tokens <= contextTokens - outputTokens)
                    return new MNNConversationContext(candidate, tokens, start - first);
                if (start == copy.Length - 1)
                    throw new ArgumentException("The system instruction and current user message exceed the input token budget.", nameof(contextTokens));
                // Advance to the next user boundary, so no orphaned assistant survives.
                do { ++start; } while (start < copy.Length - 1 && copy[start].Role != MNNChatRole.User);
            }
        }

        public static bool ContainsMedia(string text) => text.IndexOf("<img>", StringComparison.Ordinal) >= 0 ||
            text.IndexOf("<audio>", StringComparison.Ordinal) >= 0 || text.IndexOf("<video>", StringComparison.Ordinal) >= 0;
    }
}
