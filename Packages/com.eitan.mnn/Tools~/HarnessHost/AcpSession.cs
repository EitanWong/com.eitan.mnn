using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using MNN.Samples.SiriAI.Harnesses;

namespace SiriAI.HarnessHost;

internal sealed partial class AcpSession : IAsyncDisposable
{
    private readonly HarnessConfiguration _configuration;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonElement>> _pending = new();
    private readonly ConcurrentDictionary<string, AcpPermission> _permissions = new();
    private readonly SemaphoreSlim _write = new(1, 1);
    private readonly SemaphoreSlim _permissionGate = new(1, 1);
    private readonly object _activitySync = new();
    private CancellationTokenSource _turnCancellation;
    private TaskCompletionSource<bool> _promptReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Task<JsonElement> _promptResponse;
    private bool _collect;
    private int _disposed;
    private long _promptGeneration;
    internal string SessionId => _sessionId;
    internal bool Resumable { get; private set; }
    internal bool TerminalConfirmed { get; private set; }
    internal bool IsOpen => _process != null && !_process.HasExited && !_reader.IsCompleted && !_lifetime.IsCancellationRequested;
    internal bool Started => _process != null;
    internal DateTime LastUsed { get; private set; } = DateTime.UtcNow;
    private readonly CancellationTokenSource _lifetime = new();
    private AcpOutput _output = new();
    private AcpToolUpdates _toolUpdates = new();
    private Process _process;
    private Task _reader = Task.CompletedTask;
    private Task _stderr = Task.CompletedTask;
    private Action<HarnessActivity> _activity;
    private Action<JsonElement> _toolObserver;
    private string _sessionId;
    private long _nextId;
    private JsonElement _promptCapabilities;
    private AcpMcpChannels _mcp;

    internal AcpSession(HarnessConfiguration configuration) { _configuration = configuration; }
    private JsonElement _agentCapabilities;
    private bool _canList, _canResume;
    private async Task StartAsync(string workspace, CancellationToken cancellation)
    {
        if (IsOpen) return;
        if (_process != null) throw new IOException("ACP connection was lost; existing context cannot be silently replaced.");
        var start = new ProcessStartInfo(_configuration.Command)
        {
            WorkingDirectory = workspace, UseShellExecute = false, RedirectStandardInput = true,
            RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true,
            StandardInputEncoding = new UTF8Encoding(false), StandardOutputEncoding = new UTF8Encoding(false, true)
        };
        foreach (var argument in _configuration.Arguments) start.ArgumentList.Add(argument);
        start.Environment.Clear();
        foreach (var name in BackendProcessEnvironment.AvailableSystemNames().Concat(_configuration.ForwardEnvironment).Distinct(StringComparer.Ordinal))
        {
            string value = Environment.GetEnvironmentVariable(name);
            if (value != null) start.Environment[name] = value;
        }
        start.Environment["PWD"] = workspace;
        foreach (var pair in _configuration.ProcessEnvironment) start.Environment[pair.Key] = pair.Value;
        _process = Process.Start(start) ?? throw new IOException("Could not launch ACP backend.");
        _reader = ReadAsync(_lifetime.Token); _stderr = DrainStderrAsync(_lifetime.Token);
        using var initialize = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        initialize.CancelAfter(TimeSpan.FromSeconds(20));
        var initialized = await CallAsync("initialize", new
        {
            protocolVersion = 1,
            clientCapabilities = new { fs = new { readTextFile = false, writeTextFile = false }, terminal = false, elicitation = new { form = new { }, url = new { } } },
            clientInfo = new { name = "siriai-harness-host", version = "0.2.0" }
        }, initialize.Token);
        if (!initialized.TryGetProperty("protocolVersion", out var version) || version.GetInt32() != 1)
            throw new IOException("ACP did not negotiate protocol1.");
        _steeringExtension = MspWire.Field(MspWire.Field(MspWire.Field(initialized, "_meta"), "steering"), "supported").ValueKind == JsonValueKind.True;
        var capabilities = MspWire.Field(initialized, "agentCapabilities");
        _agentCapabilities = capabilities.Clone();
        _canList = MspWire.Field(MspWire.Field(capabilities, "sessionCapabilities"), "list").ValueKind == JsonValueKind.Object;
        var promptCapabilities = MspWire.Field(capabilities, "promptCapabilities");
        _promptCapabilities = promptCapabilities.ValueKind == JsonValueKind.Object ? promptCapabilities.Clone() : default;
        bool load = MspWire.Field(capabilities, "loadSession").ValueKind == JsonValueKind.True;
        bool resume = MspWire.Field(MspWire.Field(capabilities, "sessionCapabilities"), "resume").ValueKind == JsonValueKind.Object;
        Resumable = load || resume;
        _canResume = resume;
    }
    internal async Task AttachAsync(string workspace, string existingSession, Action beforeSession, CancellationToken cancellation)
    {
        if (IsOpen && _sessionId != null) return;
        await StartAsync(workspace, cancellation);
        var capabilities = _agentCapabilities;
        AcpMcpConfiguration.RequireCapabilities(_configuration, capabilities);
        _mcp = new AcpMcpChannels(_configuration.ResolvedAcpMcpProcesses, workspace,
            (id, message, token) => CallEnvelopeAsync("mcp/message", message, token, id),
            (message, token) => SendAsync(new { jsonrpc = "2.0", method = "mcp/message", @params = message }, token), _lifetime.Token);
        using var initialize = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        initialize.CancelAfter(TimeSpan.FromSeconds(20));
        JsonElement session;
        if (existingSession != null)
        {
            if (!Resumable) throw new IOException("Agent no longer advertises native session loading.");
            _sessionId = existingSession;
            beforeSession();
            session = await CallAsync(_canResume ? "session/resume" : "session/load", new { sessionId = existingSession, cwd = workspace, mcpServers = _configuration.ResolvedAcpMcpServers }, initialize.Token);
        }
        else
        {
            beforeSession();
            session = await CallAsync("session/new", new { cwd = workspace, mcpServers = _configuration.ResolvedAcpMcpServers }, initialize.Token);
            _sessionId = MspWire.Text(session, "sessionId");
            if (string.IsNullOrWhiteSpace(_sessionId) || _sessionId.Length > 4096) throw new IOException("ACP returned no bounded session identity.");
        }
        string confirmedSession = MspWire.Text(session, "sessionId");
        if (existingSession != null && confirmedSession != null && confirmedSession != existingSession)
            throw new IOException("ACP session loading returned a different native session identity.");
        await ConfigureSessionAsync(session, initialize.Token);
    }
    internal async Task<long> PreparePromptAsync(HarnessTask task, Action<HarnessActivity> activity, CancellationToken cancellation, Action<JsonElement> toolObserver = null)
    {
        await _write.WaitAsync(cancellation);
        try
        {
            _turnCancellation?.Dispose();
            _turnCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            lock (_activitySync) { _activity = activity; _output = new AcpOutput(task); _toolUpdates = new AcpToolUpdates(); _toolObserver = toolObserver; }
            PublishActivity("Backend session configuration observed.");
            TerminalConfirmed = false;
            _promptResponse = null;
            _promptReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
            ResetSteering();
            return ++_promptGeneration;
        }
        finally { _write.Release(); }
    }
    internal void RequirePromptCapabilities(JsonElement[] prompt) => AcpPromptContent.RequireCapabilities(prompt, _promptCapabilities);
    internal Task<HarnessOutcome> PromptAsync(JsonElement[] prompt, CancellationToken cancellation) => RunSteerablePromptAsync(prompt, cancellation);
    private HarnessOutcome Outcome(JsonElement response)
    {
        string reason = MspWire.Text(response, "stopReason");
        TerminalConfirmed = true; _collect = false;
        lock (_activitySync) _activity = null;
        LastUsed = DateTime.UtcNow;
        _turnCancellation?.Cancel();
        foreach (var permission in _permissions.Values) permission.Decision.TrySetCanceled();
        foreach (var input in _inputs.Values) input.Decision.TrySetCanceled();
        return _output.Finish(reason);
    }
    internal HarnessOutcome Interrupted(string message) => _output.Interrupted(message);
    internal async Task<HarnessOutcome> TimeoutAsync()
    {
        if (_promptResponse == null) return Interrupted("ACP timed out before confirmed prompt admission; no input was retried.");
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await RequestCancelAsync(limit.Token);
            var result = Outcome(await _promptResponse.WaitAsync(limit.Token));
            return result.State == HarnessTaskState.Cancelled ? HarnessOutcome.Failed("ACP timed out and native prompt cancellation was confirmed; side effects are not reverted.").WithArtifacts(result.Artifacts) : result;
        }
        catch { return Interrupted("ACP timed out; native prompt termination is unconfirmed. Context remains fenced."); }
    }
    internal async Task RequestCancelAsync(CancellationToken cancellation, long expectedGeneration = 0, bool forSteering = false)
    {
        if (expectedGeneration != 0 && expectedGeneration != Interlocked.Read(ref _promptGeneration)) return;
        if (!forSteering) StopSteering();
        await _promptReady.Task.WaitAsync(cancellation);
        if (_promptResponse?.IsCompleted == true) return;
        await SendAsync(new { jsonrpc = "2.0", method = "session/cancel", @params = new { sessionId = _sessionId } }, cancellation, expectedGeneration);
    }
    internal Task RespondAsync(string id, bool allow, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        lock (_activitySync)
        {
            if (!_permissions.TryGetValue(id, out var response) || !response.Decision.TrySetResult(allow))
                throw new InvalidOperationException("ACP permission request has expired or was already answered.");
        }
        return Task.CompletedTask;
    }
    private async Task<JsonElement> CallAsync(string method, object parameters, CancellationToken cancellation)
    {
        var reply = await CallEnvelopeAsync(method, parameters, cancellation);
        if (reply.TryGetProperty("error", out _)) throw new IOException("ACP rejected the protocol request.");
        return reply.GetProperty("result").Clone();
    }
    private async Task<JsonElement> CallEnvelopeAsync(string method, object parameters, CancellationToken cancellation, string id = null, long expectedGeneration = 0)
    {
        id ??= Interlocked.Increment(ref _nextId).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var response = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pending.TryAdd(id, response)) throw new IOException("Duplicate ACP outgoing request id.");
        try
        {
            await SendAsync(new { jsonrpc = "2.0", id, method, @params = parameters }, cancellation, expectedGeneration, requireCurrent: expectedGeneration != 0);
            return await response.Task.WaitAsync(cancellation);
        }
        finally { _pending.TryRemove(id, out _); }
    }
    private async Task SendAsync(object value, CancellationToken cancellation, long expectedGeneration = 0, bool requireCurrent = false)
    {
        await _write.WaitAsync(cancellation);
        try
        {
            if (requireCurrent && (expectedGeneration != _promptGeneration || _steeringStopped || !_collect)) throw new OperationCanceledException("Native steering generation has ended.");
            if (expectedGeneration != 0 && expectedGeneration != _promptGeneration) return;
            await _process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(value).AsMemory(), cancellation);
            await _process.StandardInput.FlushAsync(cancellation);
        }
        finally { _write.Release(); }
    }
    private async Task ReadAsync(CancellationToken cancellation)
    {
        try
        {
            var chunk = new char[4096];
            var line = new StringBuilder();
            int count;
            while ((count = await _process.StandardOutput.ReadAsync(chunk.AsMemory(), cancellation)) > 0)
            {
                for (int i = 0; i < count; i++)
                {
                    if (chunk[i] == '\n')
                    {
                        if (line.Length != 0) Dispatch(line.ToString(), cancellation);
                        line.Clear();
                    }
                    else
                    {
                        if (line.Length >= 1024 * 1024) throw new IOException("ACP message exceeds 1 MiB.");
                        line.Append(chunk[i]);
                    }
                }
            }
            throw new IOException("ACP process disconnected before pending requests completed.");
        }
        catch (Exception error)
        {
            foreach (var pending in _pending.Values) pending.TrySetException(error);
            _promptReady.TrySetException(error);
            foreach (var pending in _permissions.Values) pending.Decision.TrySetCanceled();
            foreach (var pending in _inputs.Values) pending.Decision.TrySetCanceled();
            if (_mcp != null) await _mcp.DisposeAsync();
        }
    }
    private void Dispatch(string line, CancellationToken cancellation)
    {
        using var document = JsonDocument.Parse(line);
        var root = document.RootElement;
        if (root.TryGetProperty("method", out var method))
        {
            var parameters = root.TryGetProperty("params", out var args) ? args : default;
            if (_historyMode && root.TryGetProperty("id", out var historyRequest))
            { _ = RejectRequestAsync(historyRequest.Clone(), cancellation); return; }
            if (method.GetString() == "session/update") { if (!CaptureHistory(parameters)) Update(parameters); }
            else if (method.GetString() == "elicitation/complete" && !root.TryGetProperty("id", out _)) CompleteInput(parameters);
            else if (method.GetString() is "mcp/connect" or "mcp/message" or "mcp/disconnect")
                _ = McpAsync(method.GetString(), root.TryGetProperty("id", out var mcpId) ? mcpId.Clone() : default,
                    parameters.ValueKind == JsonValueKind.Undefined ? default : parameters.Clone(), cancellation);
            else if (root.TryGetProperty("id", out var requestId))
            {
                if (method.GetString() == "session/request_permission")
                    _ = PermissionAsync(requestId.Clone(), parameters.Clone(), _turnCancellation?.Token ?? cancellation);
                else if (method.GetString() == "craft/drainMidTurnQueue")
                    _ = DrainInstructionsAsync(requestId.Clone(), parameters.Clone(), _promptGeneration, cancellation);
                else if (method.GetString() == "elicitation/create")
                    _ = InputAsync(requestId.Clone(), parameters.Clone(), _turnCancellation?.Token ?? cancellation);
                else _ = RejectRequestAsync(requestId.Clone(), cancellation);
            }
            return;
        }
        if (!root.TryGetProperty("id", out var id) || !_pending.TryGetValue(id.ToString(), out var pending)) return;
        if (root.TryGetProperty("error", out _) || root.TryGetProperty("result", out _))
        {
            if (_historyMode && id.ToString() == _historyLoadId) _historyReplay?.Stop();
            pending.TrySetResult(root.Clone());
        }
    }
    private async Task McpAsync(string method, JsonElement id, JsonElement parameters, CancellationToken cancellation)
    {
        try
        {
            if (_mcp == null) throw new AcpMcpError(-32601, "MCP channel has not been negotiated.");
            var reply = await _mcp.HandleAsync(method, parameters, id);
            if (id.ValueKind == JsonValueKind.Undefined) return;
            if (reply.TryGetProperty("error", out var error)) await SendAsync(new { jsonrpc = "2.0", id, error }, cancellation);
            else await SendAsync(new { jsonrpc = "2.0", id, result = reply.GetProperty("result") }, cancellation);
        }
        catch (Exception error)
        {
            if (id.ValueKind == JsonValueKind.Undefined || cancellation.IsCancellationRequested) return;
            try
            {
                await SendAsync(new { jsonrpc = "2.0", id, error = new {
                    code = error is AcpMcpError protocol ? protocol.Code : -32603,
                    message = error is AcpMcpError ? error.Message : "MCP provider request failed; outcome is unknown and no request was replayed." } }, cancellation);
            }
            catch { _lifetime.Cancel(); }
        }
    }
    private void Update(JsonElement parameters)
    {
        if (!parameters.TryGetProperty("sessionId", out var session) || session.GetString() != _sessionId) return;
        if (UpdateConfiguration(parameters.GetProperty("update"))) return;
        if (!_collect) return;
        ConfirmFollowup();
        var update = parameters.GetProperty("update");
        string kind = update.GetProperty("sessionUpdate").GetString();
        if (kind == "agent_message_chunk")
        {
            if (_output.Add(MspWire.Field(update, "content"))) PublishActivity("Backend produced an artifact.", includeArtifacts: true);
        }
        else if (kind == "plan")
            PublishActivity("Agent plan", executionKind: HarnessExecutionKind.Plan, executionDetail: update.GetRawText());
        else if (kind == "tool_call" || kind == "tool_call_update")
        {
            _toolObserver?.Invoke(update);
            HarnessNativeToolCall tool;
            lock (_activitySync) tool = _toolUpdates.Apply(update);
            string state = tool.Status == "completed" ? "Completed" : tool.Status == "failed" ? "Failed" :
                tool.Status == "pending" ? "Pending" : tool.Status == "in_progress" ? "Running" : "Unknown";
            PublishActivity(string.IsNullOrWhiteSpace(tool.Title) ? tool.ToolCallId : tool.Title,
                executionKind: kind == "tool_call" ? HarnessExecutionKind.ToolCall : HarnessExecutionKind.ToolResult,
                executionDetail: tool.Payload, executionState: state);
        }
    }
    private void PublishActivity(string summary, bool includeArtifacts = false,
        HarnessExecutionKind executionKind = HarnessExecutionKind.Activity, string executionDetail = null, string executionState = "Running")
    {
        lock (_activitySync)
        {
            var pending = _permissions.FirstOrDefault(pair => !pair.Value.Decision.Task.IsCompleted);
            var input = _inputs.FirstOrDefault(pair => !pair.Value.Decision.Task.IsCompleted);
            if (string.IsNullOrWhiteSpace(summary)) summary = "Backend tool activity";
            if (input.Key != null)
            {
                _activity?.Invoke(new HarnessActivity(HarnessWaitingFor.Input, input.Key, input.Value.Message,
                    includeArtifacts ? _output.Artifacts : null, input.Value.Form?.Display, executionKind, executionDetail, executionState, inputPrompt: input.Value.Prompt, sessionConfiguration: SessionConfiguration(), nativeTools: _toolUpdates.Snapshot()));
                return;
            }
            _activity?.Invoke(new HarnessActivity(pending.Key == null ? HarnessWaitingFor.None : HarnessWaitingFor.Permission,
                pending.Key, string.IsNullOrWhiteSpace(pending.Value?.Title) ? summary : pending.Value.Title, includeArtifacts ? _output.Artifacts : null,
                executionKind: executionKind, executionDetail: executionDetail, executionState: executionState, sessionConfiguration: SessionConfiguration(), nativeTools: _toolUpdates.Snapshot()));
        }
    }
    private async Task PermissionAsync(JsonElement requestId, JsonElement parameters, CancellationToken cancellation)
    {
        string id = Guid.NewGuid().ToString("N");
        var decision = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        bool entered = false;
        try
        {
            await _permissionGate.WaitAsync(cancellation); entered = true;
            cancellation.ThrowIfCancellationRequested();
            if (!_collect || parameters.GetProperty("sessionId").GetString() != _sessionId) throw new IOException("Unrelated ACP session permission request.");
            var options = parameters.GetProperty("options").EnumerateArray().Select(item => item.Clone()).ToArray();
            string title = parameters.TryGetProperty("toolCall", out var call) && call.TryGetProperty("title", out var label)
                ? label.GetString() : "Backend requests permission";
            bool allow;
            if (_configuration.PermissionMode == "full") allow = true;
            else
            {
                lock (_activitySync)
                {
                    _permissions[id] = new AcpPermission(title?.Length > 4096 ? title[..4096] : title, decision);
                    PublishActivity(title);
                }
                allow = await decision.Task.WaitAsync(cancellation);
            }
            string kind = allow ? "allow_once" : "reject_once";
            var option = options.FirstOrDefault(item => item.TryGetProperty("kind", out var optionKind) && optionKind.GetString() == kind);
            if (option.ValueKind == JsonValueKind.Undefined && allow && _configuration.PermissionMode == "full")
                option = options.FirstOrDefault(item => item.TryGetProperty("kind", out var optionKind) && optionKind.GetString() == "allow_always");
            object outcome = option.ValueKind == JsonValueKind.Undefined
                ? new { outcome = "cancelled" }
                : (object)new { outcome = "selected", optionId = option.GetProperty("optionId").GetString() };
            await SendAsync(new { jsonrpc = "2.0", id = requestId, result = new { outcome } }, cancellation);
        }
        catch (OperationCanceledException) when (!_lifetime.IsCancellationRequested)
        {
            try { await SendAsync(new { jsonrpc = "2.0", id = requestId, result = new { outcome = new { outcome = "cancelled" } } }, _lifetime.Token); }
            catch { _lifetime.Cancel(); }
        }
        catch (Exception) { _lifetime.Cancel(); }
        finally
        {
            lock (_activitySync) { _permissions.TryRemove(id, out _); if (entered) PublishActivity("Backend request resolved."); }
            if (entered) _permissionGate.Release();
        }
    }
    private async Task RejectRequestAsync(JsonElement id, CancellationToken cancellation)
    {
        try { await SendAsync(new { jsonrpc = "2.0", id, error = new { code = -32601, message = "Client capability is not supported." } }, cancellation); }
        catch (Exception) { _lifetime.Cancel(); }
    }
    private async Task DrainStderrAsync(CancellationToken cancellation)
    {
        var buffer = new char[4096];
        try { while (await _process.StandardError.ReadAsync(buffer.AsMemory(), cancellation) > 0) { } }
        catch (OperationCanceledException) { }
    }
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _lifetime.Cancel();
        foreach (var permission in _permissions.Values) permission.Decision.TrySetCanceled();
        foreach (var input in _inputs.Values) input.Decision.TrySetCanceled();
        if (_mcp != null) await _mcp.DisposeAsync();
        if (_process != null)
        {
            try { if (!_process.HasExited) _process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
            try { await Task.WhenAll(_reader, _stderr); }
            catch (OperationCanceledException) { }
            _process.Dispose();
        }
    }
    private sealed record AcpPermission(string Title, TaskCompletionSource<bool> Decision);
}

internal sealed class AcpConfigurationException : IOException { }
