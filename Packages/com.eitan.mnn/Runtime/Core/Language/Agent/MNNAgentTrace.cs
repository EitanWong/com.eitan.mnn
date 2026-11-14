using System;
using System.Collections.Generic;

namespace MNN.Unity
{
    public interface IMNNAgentTraceSource
    {
        bool TryReadTrace(out MNNConversationTranscriptEntry entry);
    }

    public sealed partial class MNNAgentProvider : IMNNAgentTraceSource
    {
        private readonly object _traceGate = new object();
        private readonly Queue<MNNConversationTranscriptEntry> _execution = new Queue<MNNConversationTranscriptEntry>();
        private string _executionId = "";
        private int _droppedExecutionEntries;
        public bool TryReadTrace(out MNNConversationTranscriptEntry entry)
        {
            lock (_traceGate)
            {
                if (_droppedExecutionEntries > 0)
                {
                    entry = new MNNConversationTranscriptEntry(MNNChatRole.System,
                        _droppedExecutionEntries + " execution entries exceeded the unread display buffer.", MNNConversationEntryKind.Error,
                        Guid.NewGuid().ToString("N"), "", "Execution history incomplete", MNNConversationEntryState.Failed);
                    _droppedExecutionEntries = 0; return true;
                }
                if (_execution.Count > 0) { entry = _execution.Dequeue(); return true; }
                entry = default; return false;
            }
        }
        private string RecordExecution(MNNConversationEntryKind kind, string title, string text,
            MNNConversationEntryState state = MNNConversationEntryState.Completed, string parent = null)
        {
            string id = Guid.NewGuid().ToString("N");
            text = text ?? "";
            if (text.Length > 16000)
            {
                int end = char.IsHighSurrogate(text[15999]) ? 15999 : 16000;
                text = text.Substring(0, end) + "\n[Display excerpt; output exceeded 16000 characters.]";
            }
            var entry = new MNNConversationTranscriptEntry(MNNChatRole.System, text, kind, id,
                parent ?? _executionId, title, state);
            lock (_traceGate)
            {
                if (_execution.Count == 128) { ++_droppedExecutionEntries; return id; }
                _execution.Enqueue(entry);
            }
            return id;
        }
    }
}
