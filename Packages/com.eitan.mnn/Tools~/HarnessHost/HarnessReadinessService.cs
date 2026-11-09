using MNN.Samples.SiriAI.Harnesses;

namespace SiriAI.HarnessHost;

/// <summary>Independent bounded checks. Cached evidence belongs to one authenticated owner and configured target.</summary>
internal sealed class HarnessReadinessService
{
    private readonly HostConfiguration _configuration;
    private readonly Dictionary<string, IHarnessAdapter> _adapters;
    private readonly object _sync = new();
    private readonly Dictionary<string, Entry> _entries = new();
    private readonly SemaphoreSlim _probes = new(4, 4);
    internal HarnessReadinessService(HostConfiguration configuration, IEnumerable<IHarnessAdapter> adapters)
    { _configuration = configuration; _adapters = adapters.ToDictionary(item => item.Id, StringComparer.Ordinal); }
    private (HarnessConfiguration Config, string Directory, string Key) Target(string owner, string id, string workspace)
    {
        var config = _configuration.Harnesses.SingleOrDefault(item => item.Id == id)
            ?? throw new UnauthorizedAccessException("Harness is not configured.");
        if (!_configuration.Workspaces.TryGetValue(workspace ?? "", out var directory)) throw new ArgumentException("Workspace is not configured.");
        return (config, directory, MspWire.Hash(new { owner, id, workspace }));
    }
    private static HarnessReadiness State(HarnessConfiguration config, string workspace, string state, string evidence = null) =>
        new() { HarnessId = config.Id, WorkspaceKey = workspace, Protocol = config.Protocol, State = state, Evidence = evidence };
    internal HarnessReadiness Read(string owner, string id, string workspace)
    {
        var target = Target(owner, id, workspace);
        lock (_sync) return _entries.TryGetValue(target.Key, out var entry) ? entry.Current : State(target.Config, workspace, "unknown");
    }
    private Entry EntryFor(string key, HarnessConfiguration config, string workspace)
    {
        lock (_sync)
        {
            if (_entries.TryGetValue(key, out var entry)) return entry;
            if (_entries.Count >= 10000) throw new InvalidOperationException("Backend check record capacity reached.");
            entry = new Entry { Current = State(config, workspace, "unknown") }; _entries.Add(key, entry); return entry;
        }
    }
    internal async Task<HarnessReadiness> ProbeAsync(string owner, string id, string workspace, CancellationToken cancellation)
    {
        var target = Target(owner, id, workspace);
        var entry = EntryFor(target.Key, target.Config, workspace);
        if (!await entry.Gate.WaitAsync(0, cancellation)) throw new InvalidOperationException("This backend check is already running.");
        bool acquired = false;
        try
        {
            if (!await _probes.WaitAsync(0, cancellation)) throw new InvalidOperationException("Backend check capacity reached; retry after a current check finishes.");
            acquired = true;
            lock (_sync) entry.Current = State(target.Config, workspace, "checking");
            return await CheckAsync(entry, target.Config, target.Directory, workspace, cancellation);
        }
        finally { if (acquired) _probes.Release(); entry.Gate.Release(); }
    }
    private async Task<HarnessReadiness> CheckAsync(Entry entry, HarnessConfiguration config, string directory, string workspace, CancellationToken cancellation)
    {
        HarnessReadiness result;
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        budget.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            result = await HarnessProtocolProbes.CheckAsync(config, _adapters[config.Id], directory, budget.Token);
            result.State ??= "protocol_verified";
        }
        catch (Exception error)
        {
            result = State(config, workspace, "unavailable", error is OperationCanceledException ? "probe_cancelled_or_timed_out" : "probe_failed");
        }
        result.HarnessId = config.Id; result.WorkspaceKey = workspace; result.Protocol = config.Protocol;
        result.CheckedUtcTicks = DateTime.UtcNow.Ticks;
        lock (_sync) entry.Current = result;
        cancellation.ThrowIfCancellationRequested();
        return result;
    }
    private sealed class Entry
    {
        internal readonly SemaphoreSlim Gate = new(1, 1);
        internal HarnessReadiness Current;
    }
}
