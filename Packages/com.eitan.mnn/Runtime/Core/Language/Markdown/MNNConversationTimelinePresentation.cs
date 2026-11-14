using System;
using System.Collections.Generic;

namespace MNN.Unity
{
    /// <summary>Package-owned disclosure and Markdown projection for conversation execution entries.
    /// Apps bind the resulting layout and invoke Toggle; tool payloads remain literal fenced data.</summary>
    public sealed class MNNConversationTimelinePresentation : IMNNConversationTranscriptSource
    {
        private IMNNConversationTranscriptSource _source;
        private readonly HashSet<int> _expanded = new HashSet<int>();
        public int Count => _source?.Count ?? 0;
        public void Bind(IMNNConversationTranscriptSource source)
        { if (ReferenceEquals(_source, source)) return; _source = source ?? throw new ArgumentNullException(nameof(source)); _expanded.Clear(); }
        public void Clear() { _source = null; _expanded.Clear(); }
        public int[] CaptureExpanded()
        { var result = new int[_expanded.Count]; _expanded.CopyTo(result); Array.Sort(result); return result; }
        public void RestoreExpanded(IReadOnlyList<int> entries)
        {
            if (entries == null || entries.Count > 256) throw new ArgumentException("Expansion state exceeds its budget.");
            _expanded.Clear();
            foreach (int index in entries)
                if (index >= 0 && index < Count && _source[index].Kind != MNNConversationEntryKind.Message) _expanded.Add(index);
        }
        public bool Toggle(int index)
        {
            var entry = _source[index];
            if (entry.Kind == MNNConversationEntryKind.Message) return false;
            if (!_expanded.Remove(index))
            {
                if (_expanded.Count == 256) { int oldest = int.MaxValue; foreach (int item in _expanded) oldest = Math.Min(oldest, item); _expanded.Remove(oldest); }
                _expanded.Add(index);
            }
            return true;
        }
        public bool IsExpanded(int index) => _expanded.Contains(index);
        public MNNConversationTranscriptEntry this[int index]
        {
            get
            {
                var entry = _source[index];
                if (entry.Kind == MNNConversationEntryKind.Message) return entry;
                string text = EscapeLabel(entry.Title.Length == 0 ? entry.Kind.ToString() : entry.Title);
                text = "**" + text + "** · " + entry.State + (_expanded.Contains(index) ? " ▾" : " ▸");
                if (_expanded.Contains(index) && entry.Text.Length > 0)
                {
                    string payload = entry.Text;
                    // The persisted payload is intact. Bound the display projection independently of its envelope.
                    if (payload.Length > 16000)
                    {
                        int end = char.IsHighSurrogate(payload[15999]) ? 15999 : 16000;
                        payload = payload.Substring(0, end) + "\n[Display excerpt; full content remains in the transcript.]";
                    }
                    int ticks = LongestRun(payload, '`'), tildes = LongestRun(payload, '~');
                    char delimiter = ticks <= tildes ? '`' : '~';
                    string fence = new string(delimiter, Math.Max(3, Math.Min(ticks, tildes) + 1));
                    text += "\n\n" + fence + "text\n" + payload + "\n" + fence;
                }
                return entry.WithText(text);
            }
        }
        private static int LongestRun(string text, char delimiter)
        {
            int maximum = 0, current = 0;
            foreach (char c in text) { current = c == delimiter ? current + 1 : 0; maximum = Math.Max(maximum, current); }
            return maximum;
        }
        private static string EscapeLabel(string text)
        {
            string result = text.Replace("\\", "\\\\").Replace("\r", " ").Replace("\n", " ");
            foreach (char c in new[] { '*', '_', '[', ']', '<', '>', '#', '`' }) result = result.Replace(c.ToString(), "\\" + c);
            return result;
        }
    }
}
