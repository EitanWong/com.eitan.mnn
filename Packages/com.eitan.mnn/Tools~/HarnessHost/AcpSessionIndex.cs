using System.Text.Json;

namespace SiriAI.HarnessHost;

internal sealed class AcpCheckpoint
{
    public string Fingerprint { get; set; }
    public string ContextKey { get; set; }
    public string SessionId { get; set; }
    public bool WaitingForBackend { get; set; }
    public long StartupDeadlineUtcTicks { get; set; }
}
internal sealed record AcpSessionRecord(string SessionId, bool Resumable, bool Uncertain);

/// <summary>Atomic context references; uncertainty is persisted before outbound side effects.</summary>
internal sealed class AcpSessionIndex
{
    private readonly string _path, _fingerprint;
    private readonly object _sync = new();
    private Dictionary<string, AcpSessionRecord> _sessions = new(StringComparer.Ordinal);
    internal AcpSessionIndex(string directory, string id, string fingerprint)
    {
        _path = Path.Combine(directory, "acp-sessions-" + MspWire.Hash(id)[..16] + ".json"); _fingerprint = fingerprint;
        if (!File.Exists(_path)) return;
        if (new FileInfo(_path).Length > 1048576) throw new IOException("ACP session index exceeds 1 MiB.");
        var data = JsonSerializer.Deserialize<Saved>(File.ReadAllBytes(_path)) ?? throw new IOException("Missing ACP context index.");
        if (data.Fingerprint != fingerprint) throw new IOException("ACP execution configuration changed; preserve the existing data directory and select a new harness id for the new configuration.");
        if (data.Sessions == null || data.Sessions.Count > 10000 || data.Sessions.Any(p => p.Key.Length != 64 || p.Value == null ||
            p.Value.SessionId?.Length > 4096 || !p.Value.Uncertain && string.IsNullOrWhiteSpace(p.Value.SessionId))) throw new IOException("Invalid ACP context index.");
        _sessions = data.Sessions;
    }
    internal AcpSessionRecord Get(string key) { lock (_sync) return _sessions.GetValueOrDefault(key); }
    internal void Fence(string key)
    {
        lock (_sync) Put(key, Get(key) is AcpSessionRecord record ? record with { Uncertain = true } : new AcpSessionRecord(null, false, true));
    }
    internal void Put(string key, AcpSessionRecord record)
    {
        lock (_sync)
        {
            var next = new Dictionary<string, AcpSessionRecord>(_sessions, StringComparer.Ordinal) { [key] = record };
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new Saved { Fingerprint = _fingerprint, Sessions = next });
            if (bytes.Length > 1048576 || next.Count > 10000) throw new IOException("ACP context index exceeds its budget.");
            Directory.CreateDirectory(Path.GetDirectoryName(_path)); string temp = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { file.Write(bytes); file.Flush(true); }
                File.Move(temp, _path, true); _sessions = next;
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
    private sealed class Saved
    {
        public string Fingerprint { get; set; }
        public Dictionary<string, AcpSessionRecord> Sessions { get; set; }
    }
}
