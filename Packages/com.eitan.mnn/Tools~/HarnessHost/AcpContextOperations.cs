namespace SiriAI.HarnessHost;

/// <summary>Task execution waits; explicit history loads reject busy contexts. Unrelated contexts remain independent.</summary>
internal sealed class AcpContextOperations
{
    private readonly object _sync = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    internal async Task<IDisposable> EnterAsync(string key, CancellationToken cancellation)
    {
        Entry entry;
        lock (_sync)
        {
            if (!_entries.TryGetValue(key, out entry)) _entries.Add(key, entry = new Entry());
            entry.Users++;
        }
        try { await entry.Gate.WaitAsync(cancellation); }
        catch { Release(key, entry, false); throw; }
        return new Lease(this, key, entry);
    }
    internal IDisposable TryEnter(string key)
    {
        lock (_sync)
        {
            if (_entries.ContainsKey(key)) return null;
            var entry = new Entry { Users = 1 };
            entry.Gate.Wait(); _entries.Add(key, entry);
            return new Lease(this, key, entry);
        }
    }
    private void Release(string key, Entry entry, bool acquired)
    {
        lock (_sync)
        {
            if (acquired) entry.Gate.Release();
            if (--entry.Users == 0) { _entries.Remove(key); entry.Gate.Dispose(); }
        }
    }
    private sealed class Entry { internal readonly SemaphoreSlim Gate = new(1, 1); internal int Users; }
    private sealed class Lease : IDisposable
    {
        private AcpContextOperations _owner;
        private readonly string _key;
        private readonly Entry _entry;
        internal Lease(AcpContextOperations owner, string key, Entry entry) { _owner = owner; _key = key; _entry = entry; }
        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.Release(_key, _entry, true);
    }
}
