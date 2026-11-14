using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>Prompt history and voice-validation context share one reset boundary.
    /// The response owner mutates messages; capture validation reads only the atomic text tails.</summary>
    public class MNNConversationMemory
    {
        public readonly List<MNNOpenAIMessage> Messages;
        private readonly int _maximumMessages;
        private string _lastUserText, _lastAssistantText;
        public string LastUserText => Volatile.Read(ref _lastUserText);
        public string LastAssistantText => Volatile.Read(ref _lastAssistantText);

        public MNNConversationMemory(List<MNNOpenAIMessage> messages, int maximumMessages)
        {
            Messages = messages ?? throw new ArgumentNullException(nameof(messages));
            _maximumMessages = maximumMessages;
        }

        public void AddUser(MNNOpenAIMessage message, string text)
        {
            if (text != null) Volatile.Write(ref _lastUserText, Tail(text));
            Messages.Add(message);
            Trim();
        }

        public void RememberAssistant(string text) => Volatile.Write(ref _lastAssistantText, Tail(text));

        public void AddEvent(MNNConversationEvent input)
        {
            Messages.Add(input.CreateMemoryRecord());
            Trim();
        }

        public void AddAssistant(string text)
        {
            if (!string.IsNullOrEmpty(text)) Messages.Add(new MNNOpenAIMessage(MNNChatRole.Assistant, text));
            Trim();
        }

        public void CommitPresented(MNNRealtimeOutputPipeline output, MNNResponseId response)
        {
            if (!response.IsValid) return;
            var spoken = new StringBuilder();
            for (int i = 0; i < output.SegmentCount; ++i)
            {
                var segment = output.GetSegment(i);
                if (segment.Response == response && segment.IsPresented) spoken.Append(segment.Text).Append(' ');
            }
            if (spoken.Length > 0)
            {
                string text = spoken.ToString().Trim();
                if (Messages.Count == 0 || Messages[Messages.Count - 1].Role != MNNChatRole.Assistant ||
                    !string.Equals(Messages[Messages.Count - 1].Text?.Trim(), text, StringComparison.Ordinal))
                    Messages.Add(new MNNOpenAIMessage(MNNChatRole.Assistant, text));
            }
            output.RetireHistoryThrough(response);
            Trim();
        }

        /// <summary>Prepare a bounded text-only context before replacing live memory. Display-only execution
        /// evidence never becomes model instructions or consumes the message-count window.</summary>
        public void Restore(IMNNConversationTranscriptSource source, int maximumCharacters = 65536)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (maximumCharacters < 1) throw new ArgumentOutOfRangeException(nameof(maximumCharacters));
            var selected = new List<MNNConversationTranscriptEntry>();
            int characters = 0;
            for (int i = source.Count - 1; i >= 0 && selected.Count < _maximumMessages; --i)
            {
                var entry = source[i];
                if (entry.Kind != MNNConversationEntryKind.Message || entry.Role == MNNChatRole.System) continue;
                if (entry.Text.Length > maximumCharacters - characters) break;
                selected.Add(entry); characters += entry.Text.Length;
            }
            selected.Reverse();
            Reset();
            foreach (var entry in selected)
            {
                if (entry.Role == MNNChatRole.User) AddUser(new MNNOpenAIMessage(entry.Role, entry.Text), entry.Text);
                else { AddAssistant(entry.Text); RememberAssistant(entry.Text); }
            }
        }

        public void Reset()
        {
            Messages.Clear();
            Volatile.Write(ref _lastUserText, null);
            Volatile.Write(ref _lastAssistantText, null);
        }

        private void Trim()
        {
            long frames = 0;
            for (int i = Messages.Count - 1; i >= 0; --i)
            {
                frames += Messages[i].Audio?.Length ?? 0;
                if (Messages.Count - i > _maximumMessages || frames > 16000 * 120L)
                { Messages.RemoveRange(0, i + 1); break; }
            }
            while (Messages.Count > 0 && Messages[0].Role == MNNChatRole.Assistant) Messages.RemoveAt(0);
        }

        public static string Tail(string text)
        {
            if (text == null) return "";
            if (text.Length <= 512) return text;
            int start = text.Length - 512;
            if (char.IsLowSurrogate(text[start])) ++start;
            return text.Substring(start);
        }
    }
}
