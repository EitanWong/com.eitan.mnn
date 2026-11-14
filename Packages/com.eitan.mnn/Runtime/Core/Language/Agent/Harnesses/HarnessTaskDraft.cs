using System;
using System.Linq;

namespace MNN.Samples.SiriAI.Harnesses
{
    /// <summary>Main-thread draft. An asynchronous operation can commit only to the revision it captured.</summary>
    public sealed class HarnessTaskDraft
    {
        private HarnessContentPart[] _attachments = new HarnessContentPart[0];
        private string _objective = "";
        public long Scope { get; private set; } = long.MinValue;
        public long Revision { get; private set; }
        public HarnessContentPart[] Attachments => _attachments.ToArray();
        public string Objective
        {
            get => _objective;
            set { value = value ?? ""; if (_objective != value) { _objective = value; Revision++; } }
        }
        public void SetScope(long scope)
        { if (Scope != scope) { Scope = scope; Clear(); } }
        public void Add(HarnessContentPart part)
        { _attachments = HarnessContentPart.CheckedInputs(_attachments.Concat(new[] { part }).ToArray()); Revision++; }
        public bool TryAdd(long scope, long revision, HarnessContentPart part)
        { if (scope != Scope || revision != Revision) return false; Add(part); return true; }
        public void RemoveAt(int index)
        {
            if (index < 0 || index >= _attachments.Length) throw new ArgumentOutOfRangeException(nameof(index));
            _attachments = _attachments.Where((part, i) => i != index).ToArray(); Revision++;
        }
        public HarnessTaskDraftSnapshot Snapshot() => new HarnessTaskDraftSnapshot(Scope, Revision, Objective, _attachments);
        public bool ClearIfCurrent(long scope, long revision)
        { if (scope != Scope || revision != Revision) return false; Clear(); return true; }
        private void Clear() { _objective = ""; _attachments = new HarnessContentPart[0]; Revision++; }
    }

    public sealed class HarnessTaskDraftSnapshot
    {
        private readonly HarnessContentPart[] _attachments;
        public long Scope { get; }
        public long Revision { get; }
        public string Objective { get; }
        public HarnessContentPart[] Attachments => _attachments.ToArray();
        internal HarnessTaskDraftSnapshot(long scope, long revision, string objective, HarnessContentPart[] attachments)
        { Scope = scope; Revision = revision; Objective = objective; _attachments = attachments.ToArray(); }
    }
}
