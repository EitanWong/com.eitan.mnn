using System.Text.Json;

namespace SiriAI.HarnessHost;

internal sealed class MspCheckpoint
{
    public string Fingerprint { get; set; }
    public string OwnerWorkspace { get; set; }
    public string SessionId { get; set; }
    public string TurnCommandId { get; set; }
    public string TurnId { get; set; }
    public bool Durable { get; set; }
}

internal sealed record MspSessionRecord(string SessionId, bool Durable);

/// <summary>One adapter gate owns this index; native sessions are separated by owner and workspace.</summary>
internal sealed class MspSessionIndex
{
    private readonly string _path;
    private readonly string _fingerprint;
    internal Dictionary<string, MspSessionRecord> Sessions { get; } = new(StringComparer.Ordinal);
    internal MspSessionIndex(string directory, string id, string fingerprint)
    {
        _path = Path.Combine(directory, "msp-sessions-" + MspWire.Hash(id)[..16] + ".json");
        _fingerprint = fingerprint;
        if (!File.Exists(_path)) return;
        if (new FileInfo(_path).Length > 1048576) throw new IOException("MSP session index exceeds 1 MiB.");
        var saved = JsonSerializer.Deserialize<SavedIndex>(File.ReadAllBytes(_path)) ?? throw new IOException("MSP session index is missing.");
        if (saved.Fingerprint != fingerprint) return;
        if (saved.Sessions == null || saved.Sessions.Count > 10000 || saved.Sessions.Any(p => p.Key.Length != 64 || p.Value == null || string.IsNullOrWhiteSpace(p.Value.SessionId) || p.Value.SessionId.Length > 4096))
            throw new IOException("MSP session index is invalid.");
        foreach (var pair in saved.Sessions.Where(p => p.Value.Durable)) Sessions.Add(pair.Key, pair.Value);
    }
    internal void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path));
        byte[] data = JsonSerializer.SerializeToUtf8Bytes(new SavedIndex { Fingerprint = _fingerprint, Sessions = Sessions.Where(p => p.Value.Durable).ToDictionary(p => p.Key, p => p.Value) });
        if (data.Length > 1048576) throw new IOException("MSP session index exceeds 1 MiB.");
        string temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { file.Write(data);file.Flush(true); }
            File.Move(temporary, _path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private sealed class SavedIndex
    {
        public string Fingerprint { get; set; }
        public Dictionary<string, MspSessionRecord> Sessions { get; set; }
    }
}
