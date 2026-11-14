using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MNN.Unity
{
    [Serializable]
    public sealed class MNNConversationInfo
    {
        [SerializeField] internal string id, title = "New conversation", draft = "";
        [SerializeField] internal long updated;
        [SerializeField] internal bool archived, manualTitle, generatedTitle, titleTracked;
        public bool NeedsGeneratedTitle => !manualTitle && !generatedTitle;
        [SerializeField] internal MNNConversationViewState view;
        [SerializeField] internal List<MNNConversationImportCursor> execution = new List<MNNConversationImportCursor>();
        public string Id => id;
        public string Title => title;
        public string Draft => draft;
        public long UpdatedUtcTicks => updated;
        public bool Archived => archived;
    }

    [Serializable]
    internal sealed class MNNConversationImportCursor
    {
        public string source;
        public long through;
    }

    /// <summary>One active journal writer; inactive chats retain only small catalog records.</summary>
    public sealed class MNNConversationCatalog : IDisposable
    {
        [Serializable] private sealed class Catalog
        {
            public string active;
            public MNNConversationInfo pending;
            public List<MNNConversationInfo> conversations = new List<MNNConversationInfo>();
        }
        private readonly string directory, index;
        private Catalog catalog;
        private bool dirty, disposed;
        private IReadOnlyList<MNNConversationInfo> _catalogView;
        public IReadOnlyList<MNNConversationInfo> Conversations => _catalogView;
        public string ActiveId => catalog.active;
        private MNNConversationInfo ActiveInfo => ActiveId == null ? catalog.pending : Find(ActiveId);
        public string ActiveTitle => ActiveInfo.title;
        public string Draft => ActiveInfo.draft;
        public MNNConversationViewState ViewState => ActiveInfo.view ?? MNNConversationViewState.Latest;
        public void SetViewState(MNNConversationViewState value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value)); value.Validate();
            var entry = ActiveInfo; if (value.SameAs(entry.view)) return; entry.view = value; dirty = true;
        }
        public MNNConversationTranscriptStore History { get; private set; }
        public MNNConversationCatalog(string directory) : this(directory, false) { }
        public MNNConversationCatalog(string directory, bool deferCreation)
        {
            this.directory = Path.GetFullPath(directory); Directory.CreateDirectory(this.directory);
            index = Path.Combine(this.directory, "conversations.json");
            catalog = ReadCatalog(index) ?? ReadCatalog(index + ".bak");
            if (catalog == null)
            {
                catalog = new Catalog();
                dirty = true;
            }
            _catalogView = catalog.conversations.AsReadOnly();
            var known = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in catalog.conversations) known.Add(entry.id);
            foreach (string path in Directory.GetFiles(this.directory, "*.bin"))
            {
                string id = Path.GetFileNameWithoutExtension(path);
                if (ValidId(id) && known.Add(id))
                {
                    catalog.conversations.Add(new MNNConversationInfo { id = id,
                        title = "Recovered conversation", updated = File.GetLastWriteTimeUtc(path).Ticks });
                    dirty = true;
                }
            }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in catalog.conversations) ids.Add(entry.id);
            if (deferCreation && (catalog.active == null || catalog.conversations.Count == 0))
            {
                catalog.active = null;
                if (catalog.pending == null) catalog.pending = new MNNConversationInfo();
                History = new MNNConversationTranscriptStore(this.directory);
            }
            else if (catalog.conversations.Count == 0) Create();
            else
            {
                if (!ids.Contains(catalog.active ?? "")) { catalog.active = catalog.conversations[0].id; dirty = true; }
                History = OpenHistory(catalog.active);
                if (History.RecoveredBytes > 0) { Find(ActiveId).execution.Clear(); dirty = true; }
            }
        }
        private static Catalog ReadCatalog(string path)
        {
            if (!File.Exists(path)) return null;
            try
            {
                var result = JsonUtility.FromJson<Catalog>(File.ReadAllText(path));
                if (result?.conversations == null) return null;
                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (var entry in result.conversations)
                {
                    if (entry == null || !ValidId(entry.id) || !ids.Add(entry.id)) return null;
                    entry.title = string.IsNullOrWhiteSpace(entry.title) ? "New conversation" : Title(entry.title);
                    entry.draft = entry.draft ?? "";
                    // Older catalogs cannot distinguish custom names from provisional names; preserve them.
                    if (!entry.titleTracked && entry.title != "New conversation") entry.manualTitle = true;
                    try { entry.view?.Validate(); } catch (ArgumentException) { entry.view = null; }
                    if (entry.execution == null) entry.execution = new List<MNNConversationImportCursor>();
                    // Cursors are hints only; malformed hints can be reconstructed from journal identities.
                    entry.execution.RemoveAll(cursor => cursor == null || string.IsNullOrEmpty(cursor.source) || cursor.source.Length > 64 || cursor.through < 0);
                    if (entry.draft.Length > 2000) entry.draft = entry.draft.Substring(0, 2000);
                }
                if (result.pending != null)
                {
                    result.pending.id = null; result.pending.title = "New conversation";
                    result.pending.draft = result.pending.draft ?? "";
                    if (result.pending.draft.Length > 2000) result.pending.draft = result.pending.draft.Substring(0, 2000);
                    try { result.pending.view?.Validate(); } catch (ArgumentException) { result.pending.view = null; }
                    result.pending.execution = new List<MNNConversationImportCursor>();
                }
                return result;
            }
            catch (ArgumentException) { return null; }
            catch (InvalidDataException) { return null; }
        }
        private static bool ValidId(string id) => Guid.TryParseExact(id, "N", out _);
        private MNNConversationInfo Find(string id)
        {
            if (!ValidId(id)) throw new ArgumentException("Invalid conversation identity.", nameof(id));
            return catalog.conversations.Find(entry => entry.id == id) ?? throw new KeyNotFoundException("Conversation no longer exists.");
        }
        public MNNConversationTranscriptStore OpenHistory(string id)
        {
            Find(id); string path = Path.Combine(directory, id + ".bin");
            if (!File.Exists(path)) throw new FileNotFoundException("Conversation journal is missing.", path);
            return MNNConversationTranscriptStore.OpenPersistent(path);
        }
        public string Create()
        {
            Flush();
            var entry = new MNNConversationInfo { id = Guid.NewGuid().ToString("N"), updated = DateTime.UtcNow.Ticks };
            var next = MNNConversationTranscriptStore.OpenPersistent(Path.Combine(directory, entry.id + ".bin"));
            History?.Dispose(); catalog.conversations.Insert(0, entry);
            catalog.active = entry.id; catalog.pending = null; History = next; dirty = true; Flush();
            return entry.id;
        }
        /// <summary>An unsent draft has no identity, list row or persistent journal.</summary>
        public void BeginDraft()
        {
            Flush(); var next = new MNNConversationTranscriptStore(directory); History?.Dispose();
            catalog.active = null; catalog.pending = new MNNConversationInfo();
            History = next; dirty = true; Flush();
        }
        /// <summary>Promote the current draft once, on the first accepted user turn.</summary>
        public string CommitDraft()
        {
            if (ActiveId != null) return ActiveId;
            var entry = catalog.pending ?? new MNNConversationInfo();
            entry.id = Guid.NewGuid().ToString("N"); entry.updated = DateTime.UtcNow.Ticks;
            var next = MNNConversationTranscriptStore.OpenPersistent(Path.Combine(directory, entry.id + ".bin"));
            try { for (int i = 0; i < History.Count; ++i) next.Add(History[i]); next.Flush(); }
            catch { next.Dispose(); throw; }
            History.Dispose(); History = next; catalog.conversations.Insert(0, entry);
            catalog.active = entry.id; catalog.pending = null; dirty = true; Flush(); return entry.id;
        }
        /// <summary>Late model results cannot overwrite an explicit name or resurrect deleted chats.</summary>
        public bool TrySetGeneratedTitle(string id, string title)
        {
            var entry = catalog.conversations.Find(item => item.id == id);
            if (entry == null || !entry.NeedsGeneratedTitle || string.IsNullOrWhiteSpace(title)) return false;
            entry.title = Title(title); entry.generatedTitle = entry.titleTracked = true; dirty = true; Flush(); return true;
        }
        public void Select(string id)
        {
            if (id == ActiveId) return;
            Find(id); Flush();
            var next = OpenHistory(id);
            History?.Dispose(); History = next; catalog.active = id; catalog.pending = null;
            if (next.RecoveredBytes > 0) Find(id).execution.Clear();
            dirty = true; Flush();
        }
        /// <summary>Imports an ordered external execution stream once, including after reconnect or catalog
        /// recovery. Flush checkpoints journal bytes before saving its cursor, so a crash cannot skip an event.</summary>
        public bool ImportExecution(string sourceId, long sequence, MNNConversationTranscriptEntry entry)
        {
            if (string.IsNullOrWhiteSpace(sourceId) || sourceId.Length > 64 || sequence < 1)
                throw new ArgumentException("A bounded execution source and positive sequence are required.");
            string prefix = sourceId + ":";
            if (entry.Kind == MNNConversationEntryKind.Message || entry.Id != prefix + sequence.ToString(System.Globalization.CultureInfo.InvariantCulture))
                throw new ArgumentException("Execution identity must match its source and sequence.", nameof(entry));
            var info = ActiveInfo;
            var cursor = info.execution.Find(item => item.source == sourceId);
            if (cursor == null)
            {
                cursor = new MNNConversationImportCursor { source = sourceId };
                for (int i = History.Count - 1; i >= 0; --i)
                {
                    string id = History[i].Id;
                    if (id != null && id.StartsWith(prefix, StringComparison.Ordinal) &&
                        long.TryParse(id.Substring(prefix.Length), System.Globalization.NumberStyles.None,
                            System.Globalization.CultureInfo.InvariantCulture, out long observed))
                    { cursor.through = observed; break; }
                }
                info.execution.Add(cursor);
            }
            if (sequence <= cursor.through) return false;
            History.Add(entry); cursor.through = sequence; dirty = true; return true;
        }

        public void SetDraft(string value)
        {
            value = value ?? ""; if (value.Length > 2000) value = value.Substring(0, 2000); if (Draft == value) return;
            ActiveInfo.draft = value; dirty = true;
        }
        public void Touch(string firstUserText = null)
        {
            var entry = ActiveInfo; entry.updated = DateTime.UtcNow.Ticks;
            if (entry.title == "New conversation" && !string.IsNullOrWhiteSpace(firstUserText)) { entry.title = Title(firstUserText); entry.titleTracked = true; }
            dirty = true;
        }
        private static string Title(string value)
        {
            string result = (value ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (result.Length == 0) throw new ArgumentException("Enter a conversation title.");
            int length = Math.Min(80, result.Length);
            if (length > 0 && char.IsHighSurrogate(result[length - 1])) --length;
            if (length == 0) throw new ArgumentException("Enter a conversation title.");
            return result.Substring(0, length);
        }
        public void Rename(string id, string title) { var entry = Find(id); entry.title = Title(title); entry.manualTitle = entry.titleTracked = true; dirty = true; Flush(); }
        public void Archive(string id, bool archived) { Find(id).archived = archived; dirty = true; Flush(); }
        public void Delete(string id)
        {
            var entry = Find(id);
            if (id == ActiveId) throw new InvalidOperationException("Switch to another conversation before deleting this one.");
            catalog.conversations.Remove(entry); dirty = true; Flush();
            string path = Path.Combine(directory, id + ".bin"); if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".idx")) File.Delete(path + ".idx");
        }
        public void Flush()
        {
            History?.Flush();
            if (!dirty) return;
            string temporary = index + ".tmp";
            byte[] bytes = new System.Text.UTF8Encoding(false).GetBytes(JsonUtility.ToJson(catalog));
            using (var output = new FileStream(temporary, FileMode.Create, FileAccess.Write))
            { output.Write(bytes, 0, bytes.Length); output.Flush(true); }
            if (File.Exists(index)) File.Replace(temporary, index, index + ".bak"); else File.Move(temporary, index);
            dirty = false;
        }
        public void Dispose() { if (disposed) return; Flush(); History?.Dispose(); History = null; disposed = true; }
    }
}
