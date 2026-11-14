using System;

namespace MNN.Unity
{
    public enum MNNConversationEntryKind { Message, Plan, ReasoningSummary, ToolCall, ToolResult, Error, Artifact }
    public enum MNNConversationEntryState { Running, Completed, Failed, Cancelled }

    /// <summary>Persistable public execution evidence. ReasoningSummary contains provider-supplied public
    /// summaries only. Id identifies an event; ParentId links it to its turn or tool invocation.</summary>
    public readonly struct MNNConversationTranscriptEntry
    {
        public MNNChatRole Role { get; }
        public string Text { get; }
        public MNNConversationEntryKind Kind { get; }
        public MNNConversationEntryState State { get; }
        public string Id { get; }
        public string ParentId { get; }
        public string Title { get; }
        public MNNConversationTranscriptEntry(MNNChatRole role, string text)
            : this(role, text, MNNConversationEntryKind.Message, "", "", "", MNNConversationEntryState.Completed) { }
        public MNNConversationTranscriptEntry(MNNChatRole role, string text, MNNConversationEntryKind kind,
            string id, string parentId, string title, MNNConversationEntryState state)
        {
            if (role < MNNChatRole.System || role > MNNChatRole.Assistant) throw new ArgumentOutOfRangeException(nameof(role));
            if (kind < MNNConversationEntryKind.Message || kind > MNNConversationEntryKind.Artifact ||
                state < MNNConversationEntryState.Running || state > MNNConversationEntryState.Cancelled)
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (kind != MNNConversationEntryKind.Message && string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Execution entries require an identity.", nameof(id));
            Id = Bounded(id, 128); ParentId = Bounded(parentId, 128); Title = Bounded(title, 256);
            Role = role; Text = text ?? throw new ArgumentNullException(nameof(text)); Kind = kind; State = state;
        }
        private static string Bounded(string value, int limit)
        {
            value = value ?? "";
            if (value.Length > limit) throw new ArgumentOutOfRangeException(nameof(value));
            for (int i = 0; i < value.Length; ++i)
            {
                if (char.IsHighSurrogate(value[i]))
                { if (++i == value.Length || !char.IsLowSurrogate(value[i])) throw new ArgumentException("Execution metadata contains invalid Unicode."); }
                else if (char.IsLowSurrogate(value[i])) throw new ArgumentException("Execution metadata contains invalid Unicode.");
            }
            return value;
        }
        internal MNNConversationTranscriptEntry WithText(string text) => new MNNConversationTranscriptEntry(Role, text, Kind, Id, ParentId, Title, State);
    }
}
