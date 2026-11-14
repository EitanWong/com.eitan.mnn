using System.Text.Json;

namespace SiriAI.HarnessHost;

/// <summary>One provider registry per native ACP context; channels never cross owners or workspaces.</summary>
internal sealed class AcpMcpChannels : IAsyncDisposable
{
    private readonly object _sync = new();
    private readonly IReadOnlyDictionary<string, AcpMcpProcess> _providers;
    private readonly Dictionary<string, AcpMcpConnection> _connections = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _operations = new(32, 32);
    private readonly string _workspace;
    private readonly CancellationToken _lifetime;
    private readonly Func<string, object, CancellationToken, Task<JsonElement>> _request;
    private readonly Func<object, CancellationToken, Task> _notify;
    private bool _disposed;

    internal AcpMcpChannels(IReadOnlyDictionary<string, AcpMcpProcess> providers, string workspace,
        Func<string, object, CancellationToken, Task<JsonElement>> request,
        Func<object, CancellationToken, Task> notify, CancellationToken lifetime)
    { _providers = providers; _workspace = workspace; _request = request; _notify = notify; _lifetime = lifetime; }

    internal async Task<JsonElement> HandleAsync(string method, JsonElement parameters, JsonElement id)
    {
        if (!_operations.Wait(0)) throw new AcpMcpError(-32000, "MCP channel request limit reached.");
        try
        {
            if (parameters.ValueKind != JsonValueKind.Object) throw Invalid();
            if (method == "mcp/connect")
            {
                if (id.ValueKind == JsonValueKind.Undefined) throw Invalid();
                string server = Required(parameters, "serverId");
                lock (_sync)
                {
                    if (_disposed || !_providers.TryGetValue(server, out var provider)) throw Invalid();
                    if (_connections.Count >= 8) throw new AcpMcpError(-32000, "MCP connection limit reached.");
                    string connection = Guid.NewGuid().ToString("N");
                    _connections.Add(connection, new AcpMcpConnection(connection, provider, _workspace, _request, _notify, _lifetime));
                    return JsonSerializer.SerializeToElement(new { result = new { connectionId = connection } });
                }
            }
            string key = Required(parameters, "connectionId");
            AcpMcpConnection value;
            lock (_sync)
            {
                if (_disposed || !_connections.TryGetValue(key, out value)) throw Invalid();
                if (method == "mcp/disconnect")
                {
                    if (id.ValueKind == JsonValueKind.Undefined) throw Invalid();
                    _connections.Remove(key);
                }
            }
            if (method == "mcp/disconnect")
            {
                await value.DisposeAsync();
                return JsonSerializer.SerializeToElement(new { result = new { } });
            }
            string innerMethod = Required(parameters, "method");
            var inner = parameters.TryGetProperty("params", out var data) ? data : default;
            if (inner.ValueKind is not (JsonValueKind.Undefined or JsonValueKind.Null or JsonValueKind.Object)) throw Invalid();
            return await value.MessageAsync(id, innerMethod, inner);
        }
        finally { _operations.Release(); }
    }
    internal static string Required(JsonElement value, string name)
    {
        if (!value.TryGetProperty(name, out var field) || field.ValueKind != JsonValueKind.String) throw Invalid();
        string text = field.GetString();
        if (string.IsNullOrWhiteSpace(text) || text.Length > 256 || text.Any(char.IsControl)) throw Invalid();
        return text;
    }
    internal static AcpMcpError Invalid() => new(-32602, "Invalid or unavailable MCP channel parameters.");
    public async ValueTask DisposeAsync()
    {
        AcpMcpConnection[] channels;
        lock (_sync) { _disposed = true; channels = _connections.Values.ToArray(); _connections.Clear(); }
        foreach (var channel in channels) await channel.DisposeAsync();
    }
}

internal sealed class AcpMcpError : IOException
{
    internal int Code { get; }
    internal AcpMcpError(int code, string message) : base(message) { Code = code; }
}
