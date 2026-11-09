using System.Collections.Concurrent;
using System.Text.Json;
using MNN.Samples.SiriAI.Harnesses;

namespace SiriAI.HarnessHost;

/// <summary>ACP owns native turns; durable exact Gateway handles own delegated work after end_turn.</summary>
internal sealed partial class OpenClawHarnessAdapter : IRecoverableHarnessAdapter, IHarnessCancellationOutcomeAdapter,
    IHarnessSubmissionValidator, IHarnessAdmissionCheckpoint, IHarnessInputActionAdapter, IHarnessInstructionAdapter, IAsyncDisposable
{
    private readonly AcpHarnessAdapter _native;
    private readonly OpenClawGatewayRpc _gateway;
    private readonly HarnessConfiguration _configuration;
    private readonly string _fingerprint;
    private readonly ConcurrentDictionary<string, Run> _active = new();
    internal AcpHarnessAdapter NativeAdapter => _native;
    public string Id => _native.Id;
    public HarnessCapabilities Capabilities => _native.Capabilities;
    public string CancellationScope => "acp-prompt-and-native-children";
    internal OpenClawHarnessAdapter(HarnessConfiguration configuration, string directory)
    {
        _configuration = configuration; _native = new AcpHarnessAdapter(configuration, directory); _gateway = new(configuration);
        _fingerprint = MspWire.Hash(new { Native = _native.ExecutionFingerprint, Contract = "openclaw-native-delegation-v1" });
    }
    private static string Scope(string owner, string conversation, string workspace) => MspWire.Hash(new { owner, conversation, workspace });
    private OpenClawCheckpoint NewState(string owner, string conversation, string workspace) => new() { Fingerprint = _fingerprint, Scope = Scope(owner, conversation, workspace) };
    private OpenClawCheckpoint Read(string value)
    {
        var state = JsonSerializer.Deserialize<OpenClawCheckpoint>(value) ?? throw new IOException("Missing OpenClaw checkpoint.");
        if (state.Fingerprint != _fingerprint || !OpenClawDelegations.Valid(state)) throw new IOException("OpenClaw checkpoint configuration or scope changed.");
        return state;
    }
    public bool CanRecover(string checkpoint)
    {
        try { using var document = JsonDocument.Parse(checkpoint); return document.RootElement.TryGetProperty("Phase", out _) ? OpenClawDelegations.Valid(Read(checkpoint)) : _native.CanRecover(checkpoint); }
        catch (Exception error) when (error is JsonException or IOException) { return false; }
    }
    public string CreateAdmissionCheckpoint(TaskSubmission request)
    {
        string native = _native.CreateAdmissionCheckpoint(request);
        if (native == null) return null;
        var state = NewState(request.OwnerId, request.ConversationId, request.WorkspaceKey); state.NativeCheckpoint = native;
        return JsonSerializer.Serialize(state);
    }
    public void ValidateSubmission(TaskSubmission submission) => _native.ValidateSubmission(submission);
    public Task<HarnessOutcome> RunAsync(HarnessTask task, Action<HarnessActivity> activity, CancellationToken stopping) => throw new NotSupportedException("OpenClaw requires durable native delegation checkpoints.");
    public async Task<HarnessOutcome> RunDurableAsync(HarnessTask task, Action<HarnessActivity> activity, Action<string> checkpoint, CancellationToken stopping)
    {
        var state = NewState(task.OwnerId, task.ConversationId, task.WorkspaceKey);
        if (task.BackendState != null)
        {
            using var saved = JsonDocument.Parse(task.BackendState);
            if (saved.RootElement.TryGetProperty("Phase", out _)) state = Read(task.BackendState);
            else state.NativeCheckpoint = task.BackendState; // Preserve conservative handling for pre-upgrade native work.
        }
        if (state.Scope != Scope(task.OwnerId, task.ConversationId, task.WorkspaceKey) || state.TaskId != null && state.TaskId != task.Id)
            return HarnessOutcome.Interrupted("OpenClaw task ownership binding changed; no native operation was sent.");
        state.TaskId = task.Id;
        var run = new Run(task, state, checkpoint, activity, stopping);
        if (!_active.TryAdd(task.Id, run)) throw new InvalidOperationException("OpenClaw task execution already exists.");
        HarnessOutcome outcome;
        try
        {
            if (state.Phase == "summarizing")
            {
                _native.FenceContext(task);
                return run.End(HarnessOutcome.Interrupted("OpenClaw result turn outcome was lost on Host restart. Original context remains fenced; no result prompt or child work was replayed.").WithArtifacts(run.Artifacts));
            }
            if (state.Phase == "native")
            {
                var native = task.Copy(); native.BackendState = state.NativeCheckpoint;
                lock (run.Sync)
                {
                    if (run.Cancelling && state.NativeCheckpoint == null)
                        return run.End(HarnessOutcome.Cancelled("Cancelled before any native OpenClaw turn was started."));
                    if (run.Cancelling) native.State = HarnessTaskState.Cancelling;
                    run.NativeTask = native;
                    run.NativeCompletion = _native.RunDurableAsync(native, activity,
                        value => { lock (run.Sync) { state.NativeCheckpoint = value; run.Save(); } }, run.NativeLifetime.Token, false,
                        update => run.Observe(update));
                }
                var settled = await Task.WhenAny(run.NativeCompletion, run.NativeCancellation.Task);
                var initial = await settled;
                if (initial.State == HarnessTaskState.Interrupted && run.Cancelling)
                {
                    run.NativeLifetime.Cancel();
                    try { await run.NativeCompletion; } catch (OperationCanceledException) { }
                    using var cleanup = CancellationTokenSource.CreateLinkedTokenSource(stopping); cleanup.CancelAfter(10000);
                    await Task.WhenAll(state.Children.Select(child => WaitChildAsync(run, child, cleanup.Token)));
                    return run.End(HarnessOutcome.Interrupted("OpenClaw coordinator cancellation remains unconfirmed; observed child aborts cannot confirm the coordinator stopped. Native context remains fenced.").WithArtifacts(run.Artifacts));
                }
                lock (run.Sync) run.NativeFinished = true;
                if (state.Error != null) return run.End(HarnessOutcome.Interrupted(state.Error));
                if (state.Children.Length == 0) return run.End(initial);
                if (initial.State != HarnessTaskState.Completed && !run.Cancelling)
                    return run.End(HarnessOutcome.Interrupted("OpenClaw coordinator outcome is unconfirmed. Observed native child handles were retained; no prompt was replayed.").WithArtifacts(initial.Artifacts));
                var artifacts = initial.Artifacts;
                if (!string.IsNullOrWhiteSpace(initial.Text)) artifacts = artifacts.Append(new HarnessArtifact("openclaw-coordinator-reply", "Coordinator reply", null,
                    [new HarnessContentPart(HarnessContentKind.Text, initial.Text, "text/plain", "coordinator.txt")])).ToArray();
                run.Artifacts = artifacts;
                activity(new HarnessActivity(HarnessWaitingFor.None, null, "OpenClaw native child tasks are running; their results are pending.", artifacts));
                lock (run.Sync)
                {
                    state.Phase = "collecting";
                    state.DeadlineUtcTicks = _configuration.TimeoutMilliseconds == 0 ? 0 : DateTime.UtcNow.AddMilliseconds(_configuration.TimeoutMilliseconds).Ticks;
                    run.Save();
                }
            }
            if (state.Children.Length == 0 || state.Error != null) return run.End(HarnessOutcome.Interrupted("OpenClaw native delegation checkpoint has no confirmed child grant."));
            outcome = await CollectAsync(run);
            return run.End(outcome);
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            outcome = HarnessOutcome.Interrupted("OpenClaw native child result or cancellation is unconfirmed. Exact saved handles were retained; no native task or prompt was replayed.");
            return run.End(outcome.WithArtifacts(run.Artifacts));
        }
        finally
        {
            lock (run.Sync)
            {
                run.Closed = true; _active.TryRemove(task.Id, out _);
                run.NativeLifetime.Dispose(); run.PollCancellation.Dispose();
            }
        }
    }
    public async Task<HarnessOutcome> CancelWithOutcomeAsync(HarnessTask task, CancellationToken cancellation)
    {
        if (!_active.TryGetValue(task.Id, out var run)) return HarnessOutcome.Interrupted("OpenClaw native execution is unavailable; cancellation remains unconfirmed.");
        Task<HarnessOutcome> native;
        lock (run.Sync)
        {
            if (run.Closed) return run.Done.Task.IsCompletedSuccessfully ? run.Done.Task.Result :
                HarnessOutcome.Interrupted("OpenClaw execution closed before cancellation could be confirmed.");
            run.Cancelling = true; run.PollCancellation.Cancel();
            native = run.NativeCompletion != null && !run.NativeFinished ? _native.CancelObservedAsync(run.NativeTask, run.NativeCompletion, cancellation) : null;
        }
        if (native != null) run.NativeCancellation.TrySetResult(await native);
        return await run.Done.Task.WaitAsync(cancellation);
    }
    public async Task<bool> CancelAsync(HarnessTask task, CancellationToken cancellation) => (await CancelWithOutcomeAsync(task, cancellation)).State == HarnessTaskState.Cancelled;
    public Task RespondAsync(HarnessTask task, string id, string reply, bool allow, CancellationToken cancellation) => _native.RespondAsync(task, id, reply, allow, cancellation);
    public Task RespondInputAsync(HarnessTask task, string id, string reply, HarnessInputAction action, CancellationToken cancellation) => _native.RespondInputAsync(task, id, reply, action, cancellation);
    public void ValidateInstruction(HarnessTask task, HarnessInstruction instruction)
    {
        if (_active.TryGetValue(task.Id, out var run) && (run.State.Phase != "native" || run.State.Children.Length > 0))
            throw new NotSupportedException("OpenClaw native delegation has no reliable directed update entry; no instruction was sent during collection or final result synthesis.");
        _native.ValidateInstruction(task, instruction);
    }
    public Task UpdateAsync(HarnessTask task, string instruction, CancellationToken cancellation) => _native.UpdateAsync(task, instruction, cancellation);
    public Task UpdateInstructionAsync(HarnessTask task, HarnessInstruction instruction, CancellationToken cancellation) => _native.UpdateInstructionAsync(task, instruction, cancellation);
    public ValueTask DisposeAsync() => _native.DisposeAsync();
    private sealed class Run
    {
        internal readonly object Sync = new();
        internal readonly CancellationTokenSource PollCancellation = new();
        internal readonly CancellationTokenSource NativeLifetime;
        internal readonly TaskCompletionSource<HarnessOutcome> NativeCancellation = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly HarnessTask Task;
        internal readonly OpenClawCheckpoint State;
        internal readonly Action<HarnessActivity> Activity;
        internal readonly CancellationToken Stopping;
        internal readonly TaskCompletionSource<HarnessOutcome> Done = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Action<string> _checkpoint;
        private readonly OpenClawDelegations _delegations = new();
        internal HarnessTask NativeTask;
        internal Task<HarnessOutcome> NativeCompletion;
        internal bool NativeFinished, Closed;
        internal volatile bool Cancelling;
        internal HarnessArtifact[] Artifacts;
        internal Run(HarnessTask task, OpenClawCheckpoint state, Action<string> checkpoint, Action<HarnessActivity> activity, CancellationToken stopping)
        { NativeLifetime = CancellationTokenSource.CreateLinkedTokenSource(stopping); Task = task; State = state; _checkpoint = checkpoint; Activity = activity; Stopping = stopping; Cancelling = task.State == HarnessTaskState.Cancelling; Artifacts = task.Artifacts; }
        internal void Save()
        {
            string value = JsonSerializer.Serialize(State);
            if (value.Length > 16384) throw new IOException("OpenClaw native checkpoint exceeds its durable budget.");
            _checkpoint(value);
        }
        internal void Observe(JsonElement update)
        {
            lock (Sync)
            {
                try { if (_delegations.Observe(update, State, Task.WorkspaceKey)) Save(); }
                catch (Exception error) when (error is IOException or JsonException)
                { State.Error = "OpenClaw native delegation grant is incomplete or exceeds its budget; execution is unconfirmed."; Save(); throw; }
            }
        }
        internal HarnessOutcome End(HarnessOutcome outcome) { Done.TrySetResult(outcome); return outcome; }
    }
}
