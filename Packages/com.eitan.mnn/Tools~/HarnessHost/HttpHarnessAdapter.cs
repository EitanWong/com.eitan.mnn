using MNN.Samples.SiriAI.Harnesses;

namespace SiriAI.HarnessHost;

/// <summary>Delegates only to the versioned SiriAI task-host HTTP contract, with explicit remote aliases.</summary>
internal sealed partial class HttpHarnessAdapter : RemotePollingAdapter
{
    private readonly RemoteTaskClient _client;
    protected override bool ReplayAdmission => true;
    public override HarnessCapabilities Capabilities => (HarnessCapabilities)Configuration.RemoteCapabilities;
    internal HttpHarnessAdapter(HarnessConfiguration configuration) : base(configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.RemoteHarnessId) || string.IsNullOrWhiteSpace(configuration.RemoteWorkspace) ||
            (configuration.RemoteCapabilities & ~23) != 0)
            throw new ArgumentException("HTTP requires remote harness/workspace aliases and supported Cancel/Input/Permission/Attachments capability flags.");
        string token = Environment.GetEnvironmentVariable(configuration.TokenEnvironmentVariable ?? "");
        if (string.IsNullOrWhiteSpace(token)) throw new ArgumentException("Configure the remote token environment variable.");
        _client = new RemoteTaskClient(Endpoint, token);
    }
    protected override async Task<RemoteSnapshot> AdmitAsync(HarnessTask task, RemoteCheckpoint state, Action save, CancellationToken cancellation)
    {
        // Local ID is an idempotency key only, never the downstream task ID. Do not forward owner, history or local paths.
        var request = new SubmitTaskRequest { ConversationId = task.Id, SubmissionKey = task.Id,
            HarnessId = Configuration.RemoteHarnessId, WorkspaceKey = Configuration.RemoteWorkspace, Objective = task.Objective, Attachments = task.Attachments };
        while (true)
        {
            try { return Project(await _client.SubmitAsync(request, cancellation)); }
            catch (Exception error) when (Transient(error, cancellation)) { await Task.Delay(Configuration.PollMilliseconds, cancellation); }
        }
    }
    protected override async Task<RemoteSnapshot> GetAsync(RemoteCheckpoint state, CancellationToken cancellation) => Project(await _client.GetAsync(state.RemoteId, cancellation));
    protected override async Task<RemoteSnapshot> CancelRemoteAsync(RemoteCheckpoint state, CancellationToken cancellation) => Project(await _client.CancelAsync(state.RemoteId, cancellation));
    protected override async Task<RemoteSnapshot> RespondRemoteAsync(RemoteCheckpoint state, string requestId, string reply, bool allow, CancellationToken cancellation) =>
        Project(await _client.RespondAsync(state.RemoteId, new TaskControlRequest { RequestId = requestId, Text = reply, Allow = allow }, cancellation));
    private static RemoteSnapshot Project(HarnessTask task)
    {
        HarnessOutcome outcome = task.State switch
        {
            HarnessTaskState.Completed => HarnessOutcome.Completed(task.Result, task.Artifacts),
            HarnessTaskState.Failed => HarnessOutcome.Failed(task.Result),
            HarnessTaskState.Cancelled => HarnessOutcome.Cancelled(),
            HarnessTaskState.Interrupted => HarnessOutcome.Interrupted(task.Result),
            _ => null
        };
        var activity = outcome != null ? null : new HarnessActivity(task.WaitingFor, task.RequestId, task.Activity ?? "Remote task is running.", task.Artifacts);
        return new RemoteSnapshot(task.Id, null, activity, outcome);
    }
    public override ValueTask DisposeAsync() { _client.Dispose(); return ValueTask.CompletedTask; }
}
