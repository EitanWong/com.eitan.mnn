using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MNN.Unity.Editor
{
    internal static class MNNStudioPrompt
    {
        internal const int MaximumCharacters = 16000;
        internal const int RecentMessagesAfterCompression = 8;
        internal const int MaximumSummaryCharacters = 2400;
        // Keep complete recent turns, within a bounded text budget. Media is supplied
        // separately for the current turn; prior image/audio paths never become fake context.
        internal static MNNChatMessage[] Build(IReadOnlyList<MNNStudioMessage> messages, string current, string summary = null)
        {
            if (string.IsNullOrWhiteSpace(current))
                throw new ArgumentException("Enter a message.", nameof(current));
            if (current.Length > MaximumCharacters)
                throw new ArgumentException("The message is too long (16,000 characters maximum).", nameof(current));
            summary = BoundSummary(summary);
            int summaryBudget = Math.Max(0, MaximumCharacters - current.Length - 256);
            if (summary.Length > summaryBudget)
                summary = summary.Substring(summary.Length - summaryBudget);
            int count = messages?.Count ?? 0, start = count, length = current.Length + (summary?.Length ?? 0);
            while (start > 0)
            {
                int addition = messages[start - 1].Text.Length;
                if (length + addition > MaximumCharacters)
                    break;
                length += addition;
                --start;
            }

            if (start < count && !messages[start].IsUser)
                ++start;
            var result = new List<MNNChatMessage>();
            if (!string.IsNullOrWhiteSpace(summary))
                result.Add(SummaryMessage(summary));
            for (int i = start; i < count; ++i)
                AppendRole(result, messages[i].IsUser ? MNNChatRole.User : MNNChatRole.Assistant, messages[i].Text);
            AppendRole(result, MNNChatRole.User, current);
            return result.ToArray();
        }

        internal static MNNChatMessage[] WithSummary(IReadOnlyList<MNNChatMessage> conversation, string summary)
        {
            var result = new List<MNNChatMessage>{SummaryMessage(BoundSummary(summary))};
            for (int i = 0; i < conversation.Count; ++i)
                if (i != 0 || conversation[i].Role != MNNChatRole.System)
                    result.Add(conversation[i]);
            return result.ToArray();
        }

        internal static void AppendRole(List<MNNChatMessage> messages, MNNChatRole role, string content)
        {
            if (messages.Count > 0 && messages[messages.Count - 1].Role == role)
            {
                var previous = messages[messages.Count - 1];
                messages[messages.Count - 1] = new MNNChatMessage(role, previous.Content + "\n\n" + content);
            }
            else
                messages.Add(new MNNChatMessage(role, content));
        }

        internal static string CleanSummary(string value)
        {
            value = CleanResponse(value);
            int endThink = value.LastIndexOf("</think>", StringComparison.Ordinal);
            if (endThink >= 0)
                value = value.Substring(endThink + 8);
            else if (value.Contains("<think>"))
                return "";
            return BoundSummary(value.Trim());
        }

        // Some instruction models use an explicit answer envelope in their native
        // chat protocol. Keep that protocol out of the Studio transcript while
        // preserving the model-native text returned by the public runtime API.
        internal static string CleanResponse(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";
            int answerStart = value.IndexOf("<answer>", StringComparison.OrdinalIgnoreCase);
            if (answerStart >= 0)
            {
                value = value.Substring(answerStart + "<answer>".Length);
                int answerEnd = value.IndexOf("</answer>", StringComparison.OrdinalIgnoreCase);
                if (answerEnd >= 0)
                    value = value.Substring(0, answerEnd);
            }

            return value.Trim();
        }

        private static MNNChatMessage SummaryMessage(string summary) => new MNNChatMessage(MNNChatRole.System, "Use this compressed memory as background context. Follow the current user request if it conflicts.\n" + summary);
        internal static string BoundSummary(string summary)
        {
            if (string.IsNullOrWhiteSpace(summary))
                return "";
            summary = summary.Trim();
            if (summary.Length > MaximumSummaryCharacters)
                summary = summary.Substring(0, MaximumSummaryCharacters).TrimEnd();
            return summary;
        }

        internal static float Cosine(float[] first, float[] second)
        {
            if (first == null || second == null || first.Length == 0 || first.Length != second.Length)
                throw new ArgumentException("The embedding model returned incompatible vectors.");
            double dot = 0, a = 0, b = 0;
            for (int i = 0; i < first.Length; ++i)
            {
                if (float.IsNaN(first[i]) || float.IsInfinity(first[i]) || float.IsNaN(second[i]) || float.IsInfinity(second[i]))
                    throw new ArgumentException("The embedding model returned non-finite values.");
                dot += (double)first[i] * second[i];
                a += (double)first[i] * first[i];
                b += (double)second[i] * second[i];
            }

            if (a <= 0 || b <= 0)
                throw new ArgumentException("The embedding model returned a zero vector.");
            return (float)Math.Max(-1, Math.Min(1, dot / Math.Sqrt(a * b)));
        }
    }
}
