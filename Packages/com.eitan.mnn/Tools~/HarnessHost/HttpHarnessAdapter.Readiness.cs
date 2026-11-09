namespace SiriAI.HarnessHost;

internal sealed partial class HttpHarnessAdapter
{
    internal async Task ProbeProtocolAsync(CancellationToken cancellation)
    {
        var health = await _client.GetHealthAsync(cancellation);
        if (health?.ProtocolVersion != 1) throw new IOException("Remote Host protocol is unavailable.");
        var catalog = await _client.GetCatalogAsync(cancellation);
        var target = catalog?.Configured?.SingleOrDefault(item => item.Id == Configuration.RemoteHarnessId);
        if (target == null || catalog.Workspaces?.Contains(Configuration.RemoteWorkspace, StringComparer.Ordinal) != true ||
            (Capabilities & ~target.Capabilities) != 0) throw new IOException("Remote target configuration is unavailable.");
    }
}
