using System.Collections.Concurrent;
using System.Text.Json;
using MNN.Samples.SiriAI.Harnesses;

namespace SiriAI.HarnessHost;

/// <summary>Official MSP v1 commands, session views and durable identities; independent of the ACP adapter.</summary>
internal sealed class MspHarnessAdapter : IRecoverableHarnessAdapter, IHarnessCancellationOutcomeAdapter, IHarnessSubmissionValidator, IHarnessInputActionAdapter, IAsyncDisposable
{
    private readonly HarnessConfiguration _configuration;
    private readonly string _fingerprint;
    private readonly MspSessionIndex _index;
    private readonly SemaphoreSlim _sessions = new(1, 1);
    private readonly HashSet<string> _attached = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, MspExecution> _executions = new();
    private readonly ConcurrentDictionary<string, MspExecution> _active = new();
    private MspConnection _connection;
    public string Id => _configuration.Id;
    public HarnessCapabilities Capabilities => HarnessCapabilities.Cancel | HarnessCapabilities.Input | HarnessCapabilities.Permission | HarnessCapabilities.Update | HarnessCapabilities.Attachments;
    public string CancellationScope => "msp-turn";

    internal static void Validate(HarnessConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.Command) || configuration.Arguments == null || configuration.TimeoutMilliseconds < 0 ||
            configuration.Model?.Length > 1024 || configuration.Model?.Any(char.IsControl) == true ||
            configuration.MspWorkspaceRoot?.Length > 4096 || configuration.MspWorkspaceRoot?.Any(char.IsControl) == true)
            throw new ArgumentException("Invalid MSP command, model, native workspace or timeout.");
    }
    internal MspHarnessAdapter(HarnessConfiguration configuration, string directory)
    {
        Validate(configuration);_configuration = configuration;
        _fingerprint = MspWire.Hash(new { configuration.Id, configuration.Command, configuration.Arguments, configuration.Model, configuration.PermissionMode, configuration.ForwardEnvironment, configuration.MspWorkspaceRoot });
        _index = new MspSessionIndex(directory, Id, _fingerprint);
    }
    public void ValidateSubmission(TaskSubmission submission) => Input(submission.Objective, submission.Attachments);
    private static object[] Input(string objective, HarnessContentPart[] attachments)
    {
        var input = new List<object> { new { type = "text", text = objective } };
        foreach (var part in attachments)
        {
            if (part.Kind != HarnessContentKind.Base64 || part.MediaType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) != true)
                throw new NotSupportedException("Muse MSP accepts inline base64 image attachments only.");
            input.Add(new { type = "image", mediaType = part.MediaType, base64Data = part.Payload });
        }
        return input.ToArray();
    }
    private string OwnerWorkspace(HarnessTask task) => MspWire.Hash(new { task.OwnerId, task.WorkspaceKey });
    public bool CanRecover(string checkpoint)
    {
        try
        {
            var state = JsonSerializer.Deserialize<MspCheckpoint>(checkpoint);
            return state?.Fingerprint == _fingerprint && state.Durable && !string.IsNullOrWhiteSpace(state.SessionId) && !string.IsNullOrWhiteSpace(state.TurnCommandId);
        }
        catch (JsonException) { return false; }
    }
    public Task<HarnessOutcome> RunAsync(HarnessTask task, Action<HarnessActivity> activity, CancellationToken stopping) => throw new NotSupportedException("MSP requires durable task checkpoints.");
    private MspExecution Execution(HarnessTask task) => _executions.GetOrAdd(task.Id, _ => new MspExecution(_configuration.PermissionMode == "full"));
    public async Task<HarnessOutcome> RunDurableAsync(HarnessTask task, Action<HarnessActivity> activity, Action<string> checkpoint, CancellationToken stopping)
    {
        var execution = Execution(task);_active[task.Id] = execution;execution.Observe(activity);
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(stopping);
        if (_configuration.TimeoutMilliseconds > 0) operation.CancelAfter(_configuration.TimeoutMilliseconds);
        var cancellation = operation.Token;
        try
        {
            var state = task.BackendState == null ? new MspCheckpoint { Fingerprint = _fingerprint, OwnerWorkspace = OwnerWorkspace(task) } :
                JsonSerializer.Deserialize<MspCheckpoint>(task.BackendState) ?? throw new IOException("Missing MSP checkpoint.");
            if (state.Fingerprint != _fingerprint || state.OwnerWorkspace != OwnerWorkspace(task)) throw new IOException("MSP checkpoint does not belong to this execution configuration.");
            var session = await SessionAsync(task, state.SessionId, cancellation);
            state.SessionId = session.SessionId;state.Durable = _connection.Durable;
            execution.Bind(_connection, state.SessionId);
            if (_connection.SchemaChanged) activity(new HarnessActivity(HarnessWaitingFor.None, null, "Muse MSP schema fingerprint differs from the pinned schema; using additive optional compatibility."));
            state.TurnCommandId ??= MspWire.CommandId();
            checkpoint(JsonSerializer.Serialize(state));
            if (state.TurnId != null) execution.SetTurn(state.TurnId);
            JsonElement recovered = default;
            if (task.BackendState != null)
            {
                recovered = await _connection.RequestAsync("session/read", new { sessionId = state.SessionId }, cancellation);
                if (state.TurnId != null) execution.Restore(recovered);
            }
            if (state.TurnId == null)
            {
                // Same command identity and exact params are the MSP-sanctioned replay after an unknown acknowledgement.
                var ack = await _connection.RequestAsync("turn/start", new
                {
                    commandId = state.TurnCommandId, sessionId = state.SessionId, input = Input(task.Objective, task.Attachments),
                    displayText = task.Objective, ifBusy = "queue"
                }, cancellation);
                if (MspWire.Text(ack, "commandId") != state.TurnCommandId || MspWire.Text(ack, "status") != "accepted" ||
                    MspWire.Text(ack, "disposition") is not ("started" or "queued")) throw new IOException("MSP did not acknowledge this turn command.");
                state.TurnId = MspWire.Required(ack, "turnId");checkpoint(JsonSerializer.Serialize(state));execution.SetTurn(state.TurnId);
                if (recovered.ValueKind != JsonValueKind.Undefined) execution.Restore(recovered);
            }
            if (task.BackendState != null) await execution.RestorePendingAsync(cancellation);
            if (task.State == HarnessTaskState.Cancelling) return await execution.CancelAsync(cancellation);
            return await execution.Outcome.WaitAsync(cancellation);
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested) { execution.HostStopping(stopping);throw; }
        catch (OperationCanceledException) { return await execution.TimeoutAsync(stopping); }
        catch (MspRemoteException) { return execution.End(HarnessOutcome.Failed("Muse MSP rejected a command; no new task was submitted as a fallback.")); }
        catch (Exception) { return execution.End(HarnessOutcome.Interrupted("Muse MSP outcome is unconfirmed. Inspect its native session before retrying; no new task was submitted.")); }
        finally { _active.TryRemove(task.Id, out _);execution.ReleaseView(); }
    }
    private async Task<MspSessionRecord> SessionAsync(HarnessTask task, string recoveredSession, CancellationToken stopping)
    {
        await _sessions.WaitAsync(stopping);
        try
        {
            if (_connection?.IsOpen != true)
            {
                if (_connection != null) await _connection.DisposeAsync();
                _attached.Clear();
                var connection = new MspConnection(_configuration, task.WorkspaceKey);_connection = connection;
                connection.Notification += (method, parameters) => { foreach (var execution in _active.Values) execution.Accept(method, parameters); };
                connection.Closed += () => { foreach (var execution in _active.Values) execution.TransportLost(connection); };
                await _connection.InitializeAsync(stopping);
            }
            string key = OwnerWorkspace(task);
            _index.Sessions.TryGetValue(key, out var session);
            if (recoveredSession != null)
            {
                if (!_connection.Durable) throw new IOException("MSP no longer advertises durable sessions; reattachment is unconfirmed.");
                if (session != null && session.SessionId != recoveredSession) throw new IOException("MSP session ownership differs from the task checkpoint.");
                session = new MspSessionRecord(recoveredSession, true);
            }
            if (session != null && _attached.Contains(session.SessionId)) return session;
            JsonElement response;
            if (session?.Durable == true)
            {
                response = await _connection.RequestAsync("session/resume", new { commandId = MspWire.CommandId(), sessionId = session.SessionId, history = "inline" }, stopping);
                if (MspWire.Text(MspWire.Field(response, "session"), "sessionId") != session.SessionId) throw new IOException("MSP resumed an unrelated session.");
            }
            else
            {
                var parameters = new Dictionary<string, object> { ["commandId"] = MspWire.CommandId(), ["workspaceRoot"] = _configuration.MspWorkspaceRoot ?? task.WorkspaceKey };
                if (!string.IsNullOrWhiteSpace(_configuration.Model)) parameters["modelId"] = _configuration.Model;
                if (_configuration.PermissionMode == "full") parameters["approvalMode"] = "allowAll";
                response = await _connection.RequestAsync("session/start", parameters, stopping);
            }
            var native = MspWire.Field(response, "session");
            session = new MspSessionRecord(MspWire.Required(native, "sessionId"), _connection.Durable);
            if (_configuration.PermissionMode == "full" && MspWire.Text(MspWire.Field(native, "approvalMode"), "mode") != "allowAll")
            {
                var read = await _connection.RequestAsync("session/read", new { sessionId = session.SessionId }, stopping);
                if (MspWire.Text(MspWire.Field(MspWire.Field(read, "session"), "approvalMode"), "mode") != "allowAll") throw new IOException("MSP did not confirm allowAll approval mode.");
            }
            _attached.Add(session.SessionId);_index.Sessions[key] = session;_index.Save();
            return session;
        }
        finally { _sessions.Release(); }
    }
    public Task<HarnessOutcome> CancelWithOutcomeAsync(HarnessTask task, CancellationToken cancellation) => Execution(task).CancelAsync(cancellation);
    public async Task<bool> CancelAsync(HarnessTask task, CancellationToken cancellation) => (await CancelWithOutcomeAsync(task, cancellation)).State == HarnessTaskState.Cancelled;
    public Task RespondAsync(HarnessTask task, string requestId, string reply, bool allow, CancellationToken cancellation) => Execution(task).RespondAsync(requestId, reply, allow, cancellation);
    public Task RespondInputAsync(HarnessTask task, string requestId, string reply, HarnessInputAction action, CancellationToken cancellation) => Execution(task).RespondAsync(requestId, reply, false, cancellation, action);
    public Task UpdateAsync(HarnessTask task, string instruction, CancellationToken cancellation) => Execution(task).SteerAsync(instruction, cancellation);
    public async ValueTask DisposeAsync() { if (_connection != null) await _connection.DisposeAsync(); }
}
