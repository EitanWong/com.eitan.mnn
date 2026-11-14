using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace SiriAI.HarnessHost;

/// <summary>Bounded duplex JSON-RPC relay. A failed provider is never recreated or replayed.</summary>
internal sealed class AcpMcpConnection : IAsyncDisposable
{
    private const int FrameBudget = 256 * 1024;
    private readonly string _id;
    private readonly Process _process;
    private readonly SemaphoreSlim _write = new(1, 1);
    private readonly SemaphoreSlim _reverse = new(8, 8);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonElement>> _pending = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _lifetime;
    private readonly Func<string, object, CancellationToken, Task<JsonElement>> _request;
    private readonly Func<object, CancellationToken, Task> _notify;
    private readonly Task _reader;
    private readonly Task _stderr;
    private int _disposed;

    internal AcpMcpConnection(string id, AcpMcpProcess definition, string workspace,
        Func<string, object, CancellationToken, Task<JsonElement>> request,
        Func<object, CancellationToken, Task> notify, CancellationToken lifetime)
    {
        _id = id; _request = request; _notify = notify;
        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
        var start = new ProcessStartInfo(definition.Command)
        {
            WorkingDirectory = workspace, UseShellExecute = false, RedirectStandardInput = true,
            RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true,
            StandardInputEncoding = new UTF8Encoding(false), StandardOutputEncoding = new UTF8Encoding(false, true)
        };
        foreach (var argument in definition.Arguments) start.ArgumentList.Add(argument);
        start.Environment.Clear();
        foreach (var name in BackendProcessEnvironment.AvailableSystemNames())
            if (Environment.GetEnvironmentVariable(name) is string text) start.Environment[name] = text;
        start.Environment["PWD"] = workspace;
        foreach (var entry in definition.Environment) start.Environment[entry.name] = entry.value;
        _process = Process.Start(start) ?? throw new IOException("MCP provider could not start.");
        _reader = ReadAsync(); _stderr = DrainAsync();
    }
    internal async Task<JsonElement> MessageAsync(JsonElement id, string method, JsonElement parameters)
    {
        _lifetime.Token.ThrowIfCancellationRequested();
        if (id.ValueKind == JsonValueKind.Undefined)
        {
            await SendAsync(new { jsonrpc = "2.0", method, @params = Optional(parameters) });
            return default;
        }
        string key = IdKey(id);
        var pending = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (_pending.Count >= 32 || !_pending.TryAdd(key, pending)) throw new AcpMcpError(-32000, "MCP pending request limit or duplicate id.");
        try
        {
            await SendAsync(new { jsonrpc = "2.0", id, method, @params = Optional(parameters) });
            return await pending.Task.WaitAsync(_lifetime.Token);
        }
        finally { _pending.TryRemove(key, out _); }
    }
    private static object Optional(JsonElement value) => value.ValueKind == JsonValueKind.Undefined ? null : value;
    private static string IdKey(JsonElement id)
    {
        if (id.ValueKind is not (JsonValueKind.String or JsonValueKind.Number) || id.GetRawText().Length > 256) throw AcpMcpChannels.Invalid();
        if (id.ValueKind == JsonValueKind.String) return JsonSerializer.Serialize(id.GetString());
        if (!id.TryGetInt64(out long number)) throw AcpMcpChannels.Invalid();
        return number.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
    private string ReverseId(JsonElement id) => "mcp:" + _id + ":" + Convert.ToBase64String(Encoding.UTF8.GetBytes(IdKey(id)));
    private async Task SendAsync(object value)
    {
        string line = JsonSerializer.Serialize(value);
        if (Encoding.UTF8.GetByteCount(line) > FrameBudget) throw new AcpMcpError(-32602, "MCP message exceeds its escaped frame budget.");
        await _write.WaitAsync(_lifetime.Token);
        try
        {
            _lifetime.Token.ThrowIfCancellationRequested();
            await _process.StandardInput.WriteLineAsync(line.AsMemory(), _lifetime.Token);
            await _process.StandardInput.FlushAsync(_lifetime.Token);
        }
        finally { _write.Release(); }
    }
    private async Task ReadAsync()
    {
        try
        {
            var buffer = new char[4096]; var line = new StringBuilder(); int count;
            while ((count = await _process.StandardOutput.ReadAsync(buffer.AsMemory(), _lifetime.Token)) > 0)
                for (int i = 0; i < count; i++)
                    if (buffer[i] == '\n')
                    {
                        if (line.Length > 0) Dispatch(line.ToString());
                        line.Clear();
                    }
                    else
                    {
                        if (line.Length >= FrameBudget) throw new IOException("MCP provider frame exceeds its budget.");
                        line.Append(buffer[i]);
                    }
            throw new IOException("MCP provider disconnected.");
        }
        catch (Exception)
        {
            Fail();
        }
    }
    private void Dispatch(string line)
    {
        if (Encoding.UTF8.GetByteCount(line) > FrameBudget) throw new IOException("MCP provider frame exceeds its budget.");
        using var document = JsonDocument.Parse(line);
        var frame = document.RootElement;
        if (frame.ValueKind != JsonValueKind.Object || MspWire.Text(frame, "jsonrpc") != "2.0") throw new IOException("Invalid MCP frame.");
        if (frame.TryGetProperty("method", out _))
        {
            if (!_reverse.Wait(0)) throw new IOException("MCP reverse request limit reached.");
            _ = RelayAsync(frame.Clone());
            return;
        }
        if (frame.TryGetProperty("id", out var id) && _pending.TryGetValue(IdKey(id), out var pending))
        {
            if (frame.TryGetProperty("result", out _) == frame.TryGetProperty("error", out _)) throw new IOException("Invalid MCP response.");
            pending.TrySetResult(frame.Clone());
        }
    }
    private async Task RelayAsync(JsonElement frame)
    {
        bool request = frame.TryGetProperty("id", out var id);
        try
        {
            string method = AcpMcpChannels.Required(frame, "method");
            object parameters = frame.TryGetProperty("params", out var value) ? Optional(value) : null;
            if (value.ValueKind is not (JsonValueKind.Undefined or JsonValueKind.Null or JsonValueKind.Object)) throw AcpMcpChannels.Invalid();
            if (method == "notifications/cancelled" && value.ValueKind == JsonValueKind.Object && value.TryGetProperty("requestId", out var cancelled))
            {
                var translated = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(value.GetRawText());
                translated["requestId"] = JsonSerializer.SerializeToElement(ReverseId(cancelled)); parameters = translated;
            }
            var message = new { connectionId = _id, method, @params = parameters };
            if (!request) { await _notify(message, _lifetime.Token); return; }
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            limit.CancelAfter(TimeSpan.FromSeconds(30));
            var reply = await _request(ReverseId(id), message, limit.Token);
            if (reply.TryGetProperty("error", out var error)) await SendAsync(new { jsonrpc = "2.0", id, error });
            else await SendAsync(new { jsonrpc = "2.0", id, result = reply.GetProperty("result") });
        }
        catch (Exception)
        {
            if (request && !_lifetime.IsCancellationRequested)
                try { await SendAsync(new { jsonrpc = "2.0", id, error = new { code = -32603, message = "MCP reverse request failed; no request was replayed." } }); }
                catch { Fail(); }
        }
        finally { _reverse.Release(); }
    }
    private async Task DrainAsync()
    {
        var buffer = new char[4096];
        try { while (await _process.StandardError.ReadAsync(buffer.AsMemory(), _lifetime.Token) > 0) { } }
        catch (OperationCanceledException) { }
    }
    private void Fail()
    {
        _lifetime.Cancel();
        foreach (var pending in _pending.Values) pending.TrySetException(new IOException("MCP provider disconnected; outcome is unknown and no request was replayed."));
        try { if (!_process.HasExited) _process.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) { }
    }
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        Fail();
        try { await _process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); }
        catch (TimeoutException) { }
        try { await Task.WhenAll(_reader, _stderr); }
        catch (OperationCanceledException) { }
        _process.Dispose();
    }
}
