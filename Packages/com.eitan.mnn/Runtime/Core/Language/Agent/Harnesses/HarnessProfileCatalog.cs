using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;

namespace MNN.Samples.SiriAI.Harnesses
{
    /// <summary>One writer commits profile settings and selected identity in the same atomic replacement.</summary>
    public sealed class HarnessProfileCatalog : IDisposable
    {
        private readonly string _path;
        private readonly FileStream _writer;
        private Document _document;
        private bool _disposed;
        public string SelectedId { get { RequireOpen(); return _document.SelectedId; } }
        public HarnessHostProfile[] Profiles { get { RequireOpen(); return _document.Profiles.ToArray(); } }

        public HarnessProfileCatalog(string directory)
        {
            Directory.CreateDirectory(directory);
            _path = Path.Combine(Path.GetFullPath(directory), "profiles.json");
            _writer = new FileStream(_path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            try
            {
                if (File.Exists(_path) && new FileInfo(_path).Length > 1024 * 1024) throw new IOException("Profile catalog exceeds its budget.");
                _document = File.Exists(_path) ? TaskHostJson.Read<Document>(File.ReadAllBytes(_path)) :
                    new Document { Version = 1, Profiles = new HarnessHostProfile[0] };
                if (_document == null || _document.Version != 1 || _document.Profiles == null || _document.Profiles.Length > 64)
                    throw new SerializationException("Unsupported profile catalog.");
                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (var profile in _document.Profiles)
                {
                    if (profile == null) throw new SerializationException("Missing profile.");
                    profile.Validate();
                    if (!ids.Add(profile.Id)) throw new SerializationException("Duplicate profile identity.");
                }
                if (_document.SelectedId != null && !ids.Contains(_document.SelectedId)) throw new SerializationException("Unknown selected profile.");
            }
            catch { _writer.Dispose(); throw; }
        }

        public HarnessHostProfile Get(string id)
        {
            RequireOpen();
            return _document.Profiles.FirstOrDefault(p => p.Id == id) ?? throw new ArgumentException("Unknown host profile.", nameof(id));
        }
        public void Save(HarnessHostProfile profile, bool select)
        {
            RequireOpen();
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            profile.Validate();
            var profiles = _document.Profiles.Where(p => p.Id != profile.Id).Concat(new[] { profile }).ToArray();
            if (profiles.Length > 64) throw new InvalidOperationException("The profile catalog is full.");
            Commit(new Document { Version = 1, Profiles = profiles, SelectedId = select || _document.SelectedId == null ? profile.Id : _document.SelectedId });
        }
        public void Select(string id)
        {
            Get(id);
            Commit(new Document { Version = 1, Profiles = _document.Profiles, SelectedId = id });
        }
        private void Commit(Document next)
        {
            byte[] bytes = TaskHostJson.Write(next);
            string temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { output.Write(bytes, 0, bytes.Length); output.Flush(true); }
                if (File.Exists(_path)) File.Replace(temporary, _path, null);
                else File.Move(temporary, _path);
                _document = next;
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        private void RequireOpen() { if (_disposed) throw new ObjectDisposedException(nameof(HarnessProfileCatalog)); }
        public void Dispose() { if (_disposed) return; _disposed = true; _writer.Dispose(); }
        [DataContract]
        private sealed class Document
        {
            [DataMember] public int Version;
            [DataMember] public string SelectedId;
            [DataMember] public HarnessHostProfile[] Profiles;
        }
    }
}
