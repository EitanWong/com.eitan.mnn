using System.Collections.Concurrent;
using System.Text.Json;
using MNN.Samples.SiriAI.Harnesses;

namespace SiriAI.HarnessHost;

/// <summary>Native ACP context ownership is independent of a foreground request and a task turn.</summary>
internal sealed partial class AcpHarnessAdapter : IRecoverableHarnessAdapter, IHarnessCancellationOutcomeAdapter, IHarnessSubmissionValidator, IHarnessAdmissionCheckpoint, IHarnessInputActionAdapter, IHarnessInstructionAdapter, IAsyncDisposable
{
    private readonly HarnessConfiguration _configuration;
    private readonly AcpSessionIndex _index;
    private readonly AcpContextOperations _contextOperations = new();
    private readonly string _fingerprint;
    private readonly ConcurrentDictionary<string, AcpSession> _contexts = new();
    private readonly SemaphoreSlim _contextGate = new(1, 1);
    private readonly HashSet<string> _activeContexts = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, AcpExecution> _executions = new();
    internal string ExecutionFingerprint => _fingerprint;
    public string Id => _configuration.Id;
    public HarnessCapabilities Capabilities => HarnessCapabilities.Cancel | HarnessCapabilities.Input | HarnessCapabilities.Update | HarnessCapabilities.Attachments | HarnessCapabilities.UpdateAttachments | (_configuration.Backend == "pi" ? HarnessCapabilities.None : HarnessCapabilities.Permission);
    public string CancellationScope => _configuration.OpenClawStartupWaitMilliseconds == 0 ? "acp-prompt" : "startup-wait-or-acp-prompt";
    internal AcpHarnessAdapter(HarnessConfiguration configuration, string directory)
    {
        _configuration = configuration;
        if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(configuration.Command) || configuration.TimeoutMilliseconds < 0)
            throw new ArgumentException("ACP requires an ID, executable and nonnegative timeout.");
        if (configuration.Model?.Length > 1024 || configuration.SessionMode?.Length > 256 ||
            configuration.SessionConfigOptions == null || configuration.SessionConfigOptions.Count > 32 ||
            configuration.SessionConfigOptions.Any(p => string.IsNullOrWhiteSpace(p.Key) || p.Key.Length > 256 ||
                p.Value.ValueKind is not (JsonValueKind.String or JsonValueKind.True or JsonValueKind.False) ||
                p.Value.ValueKind == JsonValueKind.String && (string.IsNullOrWhiteSpace(p.Value.GetString()) || p.Value.GetString().Length > 1024)))
            throw new ArgumentException("ACP session settings require bounded boolean or selector values.");
        _fingerprint = MspWire.Hash(new { configuration.Id, configuration.Backend, configuration.Command, configuration.NativeCommand,
            configuration.Arguments, configuration.PermissionMode, configuration.ForwardEnvironment, configuration.Model,
            configuration.Endpoint, configuration.TokenEnvironmentVariable });
        if (configuration.AcpMcpServers.Length > 0) _fingerprint = MspWire.Hash(new { Legacy = _fingerprint, configuration.AcpMcpServers });
        if (configuration.SessionMode != null || configuration.SessionConfigOptions.Count > 0)
            _fingerprint = MspWire.Hash(new { Legacy = _fingerprint, configuration.SessionMode, configuration.SessionConfigOptions });
        ValidateStartupWait(configuration);
        _index = new AcpSessionIndex(directory, Id, _fingerprint);
    }
    private string ContextKey(HarnessTask task) => MspWire.Hash(new { task.OwnerId, task.ConversationId, task.WorkspaceKey });
    public void ValidateSubmission(TaskSubmission submission) => AcpPromptContent.Create(submission.Objective, submission.Attachments);
    private async Task<AcpSession> ContextAsync(string key, CancellationToken cancellation)
    {
        await _contextGate.WaitAsync(cancellation);
        try
        {
            if (!_contexts.ContainsKey(key) && _contexts.Count >= 16)
            {
                var idle = _contexts.Where(p => !_activeContexts.Contains(p.Key) && p.Value.Resumable)
                    .OrderBy(p => p.Value.LastUsed).FirstOrDefault();
                if (idle.Value == null) throw new InvalidOperationException("ACP live context limit reached; no inactive resumable context can be evicted.");
                _contexts.TryRemove(idle.Key, out _); await idle.Value.DisposeAsync();
            }
            _activeContexts.Add(key);
            return _contexts.GetOrAdd(key, _ => new AcpSession(_configuration));
        }
        finally { _contextGate.Release(); }
    }
    private async Task ReleaseContextAsync(string key)
    {
        await _contextGate.WaitAsync();
        try { _activeContexts.Remove(key); }
        finally { _contextGate.Release(); }
    }
    public bool CanRecover(string checkpoint)
    {
        try { var state = JsonSerializer.Deserialize<AcpCheckpoint>(checkpoint); return state?.Fingerprint == _fingerprint && state.ContextKey?.Length == 64 && ValidStartupState(state); }
        catch (JsonException) { return false; }
    }
    public Task<HarnessOutcome> RunAsync(HarnessTask task, Action<HarnessActivity> activity, CancellationToken stopping) => throw new NotSupportedException("ACP requires durable context checkpoints.");
    public Task<HarnessOutcome> RunDurableAsync(HarnessTask task, Action<HarnessActivity> activity, Action<string> checkpoint, CancellationToken stopping) =>
        RunDurableAsync(task, activity, checkpoint, stopping, false);
    internal async Task<HarnessOutcome> RunDurableAsync(HarnessTask task, Action<HarnessActivity> activity, Action<string> checkpoint,
        CancellationToken stopping, bool propagatePromptCapabilityFailure, Action<JsonElement> toolObserver = null, string expectedCheckpoint = null)
    {
        var execution = Execution(task);
        using var admission = CancellationTokenSource.CreateLinkedTokenSource(stopping, execution.StartupCancellation);
        string key = ContextKey(task);
        IDisposable contextLease = null;
        try
        {
            contextLease = await _contextOperations.EnterAsync(key, admission.Token);
            if (task.BackendState != null && !ReadStartupState(task).WaitingForBackend)
            {
                var state = JsonSerializer.Deserialize<AcpCheckpoint>(task.BackendState);
                if (state?.Fingerprint != _fingerprint || state.ContextKey != key) throw new IOException("ACP checkpoint configuration changed.");
                _index.Fence(key);
                return execution.End(HarnessOutcome.Interrupted("ACP prompt outcome was lost during host restart. Native context is fenced; no input was replayed.").WithArtifacts(task.Artifacts));
            }
            var record = _index.Get(key);
            if (expectedCheckpoint != null)
            {
                var expected = JsonSerializer.Deserialize<AcpCheckpoint>(expectedCheckpoint);
                if (expected?.Fingerprint != _fingerprint || expected.ContextKey != key || string.IsNullOrWhiteSpace(expected.SessionId) ||
                    record == null || record.SessionId != expected.SessionId)
                {
                    _index.Fence(key);
                    return execution.End(HarnessOutcome.Interrupted("Original ACP coordinator session is unavailable; no replacement session or result prompt was sent."));
                }
            }
            if (record?.Uncertain == true)
                return execution.End(HarnessOutcome.Interrupted("ACP context has an unconfirmed prior operation. Inspect native history and use a new explicit conversation; no replacement session or input was sent."));
            if (_configuration.OpenClawStartupWaitMilliseconds > 0)
            {
                var startupOutcome = await AwaitStartupAsync(task, key, execution, activity, checkpoint, admission.Token);
                if (startupOutcome != null) return execution.End(startupOutcome);
            }
            var prompt = AcpPromptContent.Create(task.Objective, task.Attachments);
            var session = await ContextAsync(key, stopping);
            if (!session.IsOpen && record != null && !record.Resumable)
                return execution.End(HarnessOutcome.Interrupted("ACP agent did not advertise persistent session loading. Existing context was preserved; no replacement session was created."));
            if (!session.IsOpen && session.Started)
            {
                await session.DisposeAsync();
                session = new AcpSession(_configuration); _contexts[key] = session;
            }
            using var operation = CancellationTokenSource.CreateLinkedTokenSource(stopping);
            using var budget = new AcpOperationBudget(operation, _configuration.TimeoutMilliseconds);
            bool promptStarted = false;
            try
            {
                if (_configuration.OpenClawStartupWaitMilliseconds > 0) _index.Fence(key);
                await session.AttachAsync(task.WorkspaceKey, record?.SessionId, () => _index.Fence(key), operation.Token);
                _index.Put(key, new AcpSessionRecord(session.SessionId, session.Resumable, false));
                session.RequirePromptCapabilities(prompt);
                _index.Fence(key);
                checkpoint(JsonSerializer.Serialize(new AcpCheckpoint { Fingerprint = _fingerprint, ContextKey = key, SessionId = session.SessionId }));
                long generation = await session.PreparePromptAsync(task, update => { budget.Observe(update); activity(update); }, operation.Token, toolObserver);
                execution.Bind(session, generation);
                operation.Token.ThrowIfCancellationRequested();
                promptStarted = true;
                var outcome = await session.PromptAsync(prompt, operation.Token);
                _index.Put(key, new AcpSessionRecord(session.SessionId, session.Resumable, false));
                return execution.End(outcome);
            }
            catch (OperationCanceledException) when (stopping.IsCancellationRequested) { throw; }
            catch (OperationCanceledException)
            {
                if (!promptStarted)
                {
                    await session.DisposeAsync();
                    return execution.End(HarnessOutcome.Interrupted("ACP timed out before confirmed prompt admission; no previous task result was reused and no input was retried."));
                }
                var outcome = await session.TimeoutAsync();
                if (session.TerminalConfirmed) _index.Put(key, new AcpSessionRecord(session.SessionId, session.Resumable, false));
                else await session.DisposeAsync();
                return execution.End(outcome);
            }
            catch (AcpPromptCapabilityException error)
            {
                if (propagatePromptCapabilityFailure) throw;
                return execution.End(HarnessOutcome.Failed(error.Message));
            }
            catch (AcpMcpCapabilityException error)
            {
                await session.DisposeAsync();
                return execution.End(HarnessOutcome.Failed(error.Message));
            }
            catch (AcpConfigurationException)
            {
                await session.DisposeAsync();
                return execution.End(HarnessOutcome.Failed("ACP did not advertise or confirm the requested native session setting."));
            }
            catch (Exception)
            {
                await session.DisposeAsync();
                const string message = "ACP session or prompt outcome is unconfirmed. Native context remains fenced; no replacement session or retry was attempted.";
                return execution.End(promptStarted ? session.Interrupted(message) : HarnessOutcome.Interrupted(message));
            }
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) when (execution.StartupCancellation.IsCancellationRequested)
        { return execution.End(HarnessOutcome.Cancelled("Startup wait cancelled; no ACP process, native session or prompt was started.")); }
        catch (InvalidOperationException)
        { return execution.End(HarnessOutcome.Failed("ACP cannot admit another native context within its current live-process limit.")); }
        finally
        {
            if (contextLease != null) { await ReleaseContextAsync(key); contextLease.Dispose(); }
            execution.Close(); _executions.TryRemove(task.Id, out _);
        }
    }
    internal void FenceContext(HarnessTask task) => _index.Fence(ContextKey(task));
    public Task<HarnessOutcome> CancelWithOutcomeAsync(HarnessTask task, CancellationToken cancellation) =>
        Execution(task).CancelAsync(cancellation);
    internal Task<HarnessOutcome> CancelObservedAsync(HarnessTask task, Task<HarnessOutcome> completion, CancellationToken cancellation) =>
        _executions.TryGetValue(task.Id, out var execution) ? execution.CancelAsync(cancellation) : completion.WaitAsync(cancellation);
    public async Task<bool> CancelAsync(HarnessTask task, CancellationToken cancellation) => (await CancelWithOutcomeAsync(task, cancellation)).State == HarnessTaskState.Cancelled;
    public async Task RespondAsync(HarnessTask task, string requestId, string reply, bool allow, CancellationToken cancellation)
    {
        if (!_executions.TryGetValue(task.Id, out var execution)) throw new InvalidOperationException("ACP task is not active.");
        await execution.RespondAsync(requestId, allow, cancellation);
    }
    public async Task RespondInputAsync(HarnessTask task, string requestId, string reply, HarnessInputAction action, CancellationToken cancellation)
    {
        if (!_executions.TryGetValue(task.Id, out var execution)) throw new InvalidOperationException("ACP task is not active.");
        await execution.RespondInputAsync(requestId, reply, action, cancellation);
    }
    public async Task UpdateAsync(HarnessTask task, string instruction, CancellationToken cancellation)
    {
        if (!_executions.TryGetValue(task.Id, out var execution)) throw new InvalidOperationException("ACP task is not active.");
        await execution.SteerAsync(instruction, [], cancellation);
    }
    public void ValidateInstruction(HarnessTask task, HarnessInstruction instruction) =>
        AcpSession.ValidateInstructionContent(instruction.Text, instruction.Attachments);
    public async Task UpdateInstructionAsync(HarnessTask task, HarnessInstruction instruction, CancellationToken cancellation)
    {
        if (!_executions.TryGetValue(task.Id, out var execution)) throw new InvalidOperationException("ACP task is not active.");
        await execution.SteerAsync(instruction.Text, instruction.Attachments, cancellation);
    }
    public async ValueTask DisposeAsync()
    {
        foreach (var session in _contexts.Values) await session.DisposeAsync();
        _contexts.Clear();
    }
}

internal sealed class AcpExecution
{
    private readonly TaskCompletionSource<AcpSession> _session = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<HarnessOutcome> _outcome = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly object _startupSync = new();
    private readonly CancellationTokenSource _startupCancellation = new();
    private bool _awaitingStartup;
    internal AcpExecution(bool awaitingStartup = false) { _awaitingStartup = awaitingStartup; }
    internal CancellationToken StartupCancellation => _startupCancellation.Token;
    internal bool CancelStartup()
    {
        lock (_startupSync)
        {
            if (!_awaitingStartup) return false;
            _startupCancellation.Cancel(); return true;
        }
    }
    internal bool BeginNative(Action checkpoint)
    {
        lock (_startupSync)
        {
            if (_startupCancellation.IsCancellationRequested) return false;
            checkpoint(); // Persist revoked recovery eligibility before allowing any native effects.
            _awaitingStartup = false; return true;
        }
    }
    private long _generation;
    internal void Bind(AcpSession session, long generation) { _generation = generation; _session.TrySetResult(session); }
    internal HarnessOutcome End(HarnessOutcome outcome) { _outcome.TrySetResult(outcome); return outcome; }
    internal void Close() => _session.TrySetCanceled();
    internal async Task<HarnessOutcome> CancelAsync(CancellationToken cancellation)
    {
        if (_outcome.Task.IsCompleted) return await _outcome.Task;
        if (CancelStartup()) return await _outcome.Task.WaitAsync(cancellation);
        await Task.WhenAny(_session.Task, _outcome.Task).WaitAsync(cancellation);
        if (_outcome.Task.IsCompleted) return await _outcome.Task;
        var session = await _session.Task.WaitAsync(cancellation);
        await session.RequestCancelAsync(cancellation, _generation);
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        limit.CancelAfter(TimeSpan.FromSeconds(10));
        try { return await _outcome.Task.WaitAsync(limit.Token); }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
        { return session.Interrupted("ACP cancellation was sent but prompt termination remains unconfirmed."); }
    }
    internal async Task SteerAsync(string text, HarnessContentPart[] attachments, CancellationToken cancellation) =>
        await (await _session.Task.WaitAsync(cancellation)).SteerAsync(text, attachments, _generation, cancellation);
    internal async Task RespondAsync(string id, bool allow, CancellationToken cancellation) =>
        await (await _session.Task.WaitAsync(cancellation)).RespondAsync(id, allow, cancellation);
    internal async Task RespondInputAsync(string id, string reply, HarnessInputAction action, CancellationToken cancellation) =>
        await (await _session.Task.WaitAsync(cancellation)).RespondInputAsync(id, reply, action, cancellation);
}

