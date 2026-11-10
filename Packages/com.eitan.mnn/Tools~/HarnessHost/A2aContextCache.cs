namespace SiriAI.HarnessHost;

/// <summary>Server-issued, owner-scoped contexts. Only discovery waits; running tasks keep independent handles.</summary>
internal sealed class A2aContextCache
{
    private readonly object _sync = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private long _clock;
    internal sealed class Entry
    {
        internal string Id;
        internal int Active;
        internal long Used;
        internal readonly TaskCompletionSource Ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    internal async Task<Lease> AcquireAsync(string owner, CancellationToken cancellation)
    {
        while (true)
        {
            cancellation.ThrowIfCancellationRequested();
            Task ready;
            lock (_sync)
            {
                if (!_entries.TryGetValue(owner, out var entry))
                {
                    entry = new Entry { Active = 1, Used = ++_clock };
                    _entries.Add(owner, entry);
                    return new Lease(this, owner, entry);
                }
                if (entry.Id != null)
                {
                    entry.Active++; entry.Used = ++_clock;
                    return new Lease(this, owner, entry);
                }
                ready = entry.Ready.Task;
            }
            await ready.WaitAsync(cancellation);
        }
    }
    internal sealed class Lease : IDisposable
    {
        private readonly A2aContextCache _cache;
        private readonly string _owner;
        private readonly Entry _entry;
        private bool _released;
        internal Lease(A2aContextCache cache, string owner, Entry entry)
        { _cache = cache; _owner = owner; _entry = entry; }
        internal string Id { get { lock (_cache._sync) return _entry.Id; } }
        internal void Remember(string id)
        {
            if (id == null) return;
            lock (_cache._sync)
            {
                if (_entry.Id != null && _entry.Id != id) throw new IOException("A2A agent changed the owner context.");
                _entry.Id = id;
                _entry.Ready.TrySetResult();
            }
        }
        public void Dispose()
        {
            lock (_cache._sync)
            {
                if (_released) return;
                _released = true;
                _entry.Active--;
                if (_entry.Id == null && _cache._entries.TryGetValue(_owner, out var current) && ReferenceEquals(current, _entry))
                    _cache._entries.Remove(_owner);
                _entry.Ready.TrySetResult();
                _cache.Trim();
            }
        }
    }
    private void Trim()
    {
        foreach (var pair in _entries.Where(pair => pair.Value.Active == 0).OrderBy(pair => pair.Value.Used).ToArray())
        {
            if (_entries.Count <= 100) break;
            _entries.Remove(pair.Key);
        }
    }
    internal void Clear()
    {
        lock (_sync)
        {
            foreach (var entry in _entries.Values) entry.Ready.TrySetResult();
            _entries.Clear();
        }
    }
}
