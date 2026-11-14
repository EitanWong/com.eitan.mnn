using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace SiriAI.HarnessHost;

/// <summary>MSP JSON-RPC over bounded UTF-8 NDJSON. It does not infer command completion from an RPC ack.</summary>
internal sealed class MspConnection : IAsyncDisposable
{
    private const int FrameBytes = 10485760;
    private readonly Process _process;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonElement>> _pending = new();
    private readonly SemaphoreSlim _write = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Task _reader;
    private readonly Task _stderr;
    private Exception _fault;
    private long _request;
    private int _disposed;
    internal event Action<string, JsonElement> Notification;
    internal event Action Closed;
    internal bool Durable { get; private set; }
    internal bool SchemaChanged { get; private set; }
    internal bool IsOpen => _fault == null && _disposed == 0;

    internal MspConnection(HarnessConfiguration configuration, string directory)
    {
        var start = new ProcessStartInfo(configuration.Command)
        {
            WorkingDirectory = directory, UseShellExecute = false, RedirectStandardInput = true,
            RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true,
            StandardOutputEncoding = new UTF8Encoding(false, true), StandardInputEncoding = new UTF8Encoding(false)
        };
        foreach (string argument in configuration.Arguments) start.ArgumentList.Add(argument);
        start.Environment.Clear();
        foreach (string name in BackendProcessEnvironment.AvailableSystemNames().Concat(configuration.ForwardEnvironment).Distinct(StringComparer.Ordinal))
            if (Environment.GetEnvironmentVariable(name) is string value) start.Environment[name] = value;
        foreach (var pair in configuration.ProcessEnvironment) start.Environment[pair.Key] = pair.Value;
        start.Environment["PWD"] = directory;
        _process = Process.Start(start) ?? throw new IOException("Could not launch the MSP backend.");
        _reader = ReadAsync();
        _stderr = DrainAsync();
    }
    internal async Task InitializeAsync(CancellationToken stopping)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(stopping);
        budget.CancelAfter(TimeSpan.FromSeconds(20));
        var response = await RequestAsync("initialize", new
        {
            clientInfo = new { name = "siriai_harness_host", version = "0.1.0" },
            capabilities = new { userInputDialogs = true }
        }, budget.Token);
        var schema = MspWire.Field(response, "schema");
        string fingerprint = MspWire.Required(schema, "fingerprint");
        if (MspWire.Field(schema, "version").ValueKind != JsonValueKind.Number || MspWire.Field(schema, "version").GetInt32() != 1)
            throw new IOException("MSP requires schema version 1.");
        SchemaChanged = fingerprint != MspWire.SchemaFingerprint;
        Durable = MspWire.Text(response, "sessionDurability") == "durable";
        await SendAsync(new { jsonrpc = "2.0", method = "initialized" }, budget.Token);
    }
    internal async Task<JsonElement> RequestAsync(string method, object parameters, CancellationToken cancellation)
    {
        if (!IsOpen) throw new IOException("MSP connection is closed; outcome is unconfirmed.");
        string id = Interlocked.Increment(ref _request).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var pending = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = pending;
        try
        {
            if (!IsOpen) throw new IOException("MSP connection closed before the request was sent.");
            await SendAsync(new { jsonrpc = "2.0", id, method, @params = parameters }, cancellation);
            return await pending.Task.WaitAsync(cancellation);
        }
        finally { _pending.TryRemove(id, out _); }
    }
    private async Task SendAsync(object value, CancellationToken cancellation)
    {
        string json = JsonSerializer.Serialize(value);
        if (Encoding.UTF8.GetByteCount(json) > FrameBytes) throw new IOException("MSP frame exceeds 10 MiB.");
        await _write.WaitAsync(cancellation);
        try
        {
            await _process.StandardInput.WriteLineAsync(json.AsMemory(), cancellation);
            await _process.StandardInput.FlushAsync(cancellation);
        }
        finally { _write.Release(); }
    }
    private async Task ReadAsync()
    {
        try
        {
            var buffer = new char[4096];var line = new StringBuilder();int count;
            while ((count = await _process.StandardOutput.ReadAsync(buffer.AsMemory(), _lifetime.Token)) > 0)
                for (int index = 0; index < count; index++)
                {
                    char ch = buffer[index];
                    if (ch == '\n')
                    {
                        if (line.Length != 0) Dispatch(line.ToString());
                        line.Clear();
                    }
                    else
                    {
                        if (line.Length >= FrameBytes) throw new IOException("MSP frame exceeds 10 MiB.");
                        line.Append(ch);
                    }
                }
            throw new IOException("MSP process disconnected before its outcome was confirmed.");
        }
        catch (Exception)
        {
            _fault = new IOException("MSP transport ended or violated framing; outcome is unconfirmed.");
            foreach (var pending in _pending.Values) pending.TrySetException(_fault);
            Closed?.Invoke();
        }
    }
    private void Dispatch(string line)
    {
        if (Encoding.UTF8.GetByteCount(line) > FrameBytes) throw new IOException("MSP frame exceeds 10 MiB.");
        using var document = JsonDocument.Parse(line);
        var frame = document.RootElement;
        if (MspWire.Text(frame, "jsonrpc") != "2.0") throw new IOException("Invalid MSP envelope.");
        if (MspWire.Text(frame, "method") is string method)
        {
            var parameters = MspWire.Field(frame, "params").Clone();
            if (frame.TryGetProperty("id", out var id)) _ = ReceiptAsync(id.Clone(), method);
            if (method is "approval/request" or "userInput/request") method = method[..^7] + "requested";
            Notification?.Invoke(method, parameters);
            return;
        }
        if (!frame.TryGetProperty("id", out var identity) || !_pending.TryGetValue(identity.ToString(), out var response)) return;
        if (frame.TryGetProperty("error", out var error))
            response.TrySetException(new MspRemoteException(MspWire.Text(MspWire.Field(error, "data"), "kind")));
        else if (frame.TryGetProperty("result", out var result)) response.TrySetResult(result.Clone());
        else throw new IOException("MSP response has no result or error.");
    }
    private async Task ReceiptAsync(JsonElement id, string method)
    {
        try
        {
            object reply = method is "approval/request" or "userInput/request"
                ? new { jsonrpc = "2.0", id, result = new { } }
                : (object)new { jsonrpc = "2.0", id, error = new { code = -32601, message = "Client method is not supported." } };
            await SendAsync(reply, _lifetime.Token);
        }
        catch (Exception) { _lifetime.Cancel(); }
    }
    private async Task DrainAsync()
    {
        var buffer = new char[4096];
        try { while (await _process.StandardError.ReadAsync(buffer.AsMemory(), _lifetime.Token) > 0) { } }
        catch (OperationCanceledException) { }
    }
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        try
        {
            _process.StandardInput.Close();
            using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            try { await _process.WaitForExitAsync(budget.Token); }
            catch (OperationCanceledException) { if (!_process.HasExited) _process.Kill(entireProcessTree: true); await _process.WaitForExitAsync(); }
        }
        finally
        {
            _lifetime.Cancel();
            await Task.WhenAll(_reader, _stderr);
            _process.Dispose();_lifetime.Dispose();
        }
    }
}

internal sealed class MspRemoteException : IOException
{
    internal string Kind { get; }
    internal MspRemoteException(string kind) : base("MSP rejected the command (" + (kind is "commandRejected" or "approvalRequirementStale" or "sessionNotFound" or "userInputAnswerInvalid" ? kind : "remote-error") + ").") { Kind = kind; }
}
