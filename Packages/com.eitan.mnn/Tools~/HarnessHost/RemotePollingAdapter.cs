using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MNN.Samples.SiriAI.Harnesses;

namespace SiriAI.HarnessHost;

internal sealed class RemoteCheckpoint
{
    public string Fingerprint { get; set; }
    public string RemoteId { get; set; }
    public string ContextId { get; set; }
    public string Endpoint { get; set; }
    public string Binding { get; set; }
    public string Version { get; set; }
    public bool Streaming { get; set; }
}

internal sealed record RemoteSnapshot(string Id, string ContextId, HarnessActivity Activity, HarnessOutcome Outcome);
internal sealed class RemoteTransientException : IOException
{
    internal RemoteTransientException(int status) : base("Remote service temporarily unavailable: " + status) { }
}

/// <summary>One gate owns each remote session. Polls and controls cannot overwrite each other's snapshots.</summary>
internal abstract class RemotePollingAdapter : IRecoverableHarnessAdapter, IAsyncDisposable
{
    protected readonly HarnessConfiguration Configuration;
    protected readonly Uri Endpoint;
    private readonly string _fingerprint;
    private readonly ConcurrentDictionary<string, Session> _sessions = new();
    public string Id => Configuration.Id;
    public abstract HarnessCapabilities Capabilities { get; }
    protected abstract bool ReplayAdmission { get; }
    protected RemotePollingAdapter(HarnessConfiguration configuration)
    {
        Configuration = configuration;
        Endpoint = HarnessHostAddress.Parse(configuration.Endpoint);
        if (string.IsNullOrWhiteSpace(Id) || configuration.PollMilliseconds < 20 || configuration.PollMilliseconds > 60000)
            throw new ArgumentException("Remote adapters need an ID and a polling interval of 20–60000 ms.");
        // Keep the established wire identity stable when unrelated local-launch options are added.
        // Property order and names match the pre-profile configuration used by persisted checkpoints.
        var remoteIdentity = new
        {
            configuration.Id, configuration.Protocol, configuration.Command, configuration.Arguments, configuration.ForwardEnvironment,
            configuration.Endpoint, configuration.RemoteHarnessId, configuration.RemoteWorkspace, configuration.TokenEnvironmentVariable,
            configuration.RemoteCapabilities, configuration.PollMilliseconds, configuration.AgentCardPath, configuration.AllowedOrigins
        };
        string identity = JsonSerializer.Serialize(remoteIdentity);
        if (configuration.Protocol == "a2a" && configuration.ReuseContext) identity += "\nA2aReuseContext=true";
        _fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
    }
    private RemoteCheckpoint Read(string text) => text == null ? new RemoteCheckpoint { Fingerprint = _fingerprint } :
        JsonSerializer.Deserialize<RemoteCheckpoint>(text) ?? throw new ArgumentException("Missing remote checkpoint.");
    public bool CanRecover(string text)
    {
        try { var value = Read(text); return value.Fingerprint == _fingerprint && (value.RemoteId != null || ReplayAdmission); }
        catch (JsonException) { return false; }
    }
    public Task<HarnessOutcome> RunAsync(HarnessTask task, Action<HarnessActivity> activity, CancellationToken stopping) =>
        throw new NotSupportedException("A remote adapter requires the durable runtime.");
    public async Task<HarnessOutcome> RunDurableAsync(HarnessTask task, Action<HarnessActivity> activity, Action<string> checkpoint, CancellationToken stopping)
    {
        var session = _sessions.GetOrAdd(task.Id, _ => new Session());
        session.Checkpoint = Read(task.BackendState);
        session.Save = () => checkpoint(JsonSerializer.Serialize(session.Checkpoint));
        try
        {
            await session.Gate.WaitAsync(stopping);
            try
            {
                // Commit admission intent before any side effect. Only the HTTP idempotency contract permits replay.
                session.Save();
                session.Current = session.Checkpoint.RemoteId == null
                    ? await AdmitAsync(task, session.Checkpoint, session.Save, stopping)
                    : await GetConfirmedAsync(session.Checkpoint, stopping);
                Remember(session);
                session.Ready.TrySetResult(true);
            }
            finally { session.Gate.Release(); }
            bool resumeCancellation = task.State == HarnessTaskState.Cancelling;
            string lastActivity = null;
            HarnessActivity lastUpdate = null;
            while (true)
            {
                await session.Gate.WaitAsync(stopping);
                try
                {
                    if (resumeCancellation && session.Current.Outcome == null)
                    {
                        session.Current = await RetryCancelAsync(session.Checkpoint, stopping);
                        Remember(session);
                        resumeCancellation = false;
                    }
                    if (session.Current.Outcome != null) return session.Current.Outcome;
                    var update = session.Current.Activity;
                    if (update != null && !ReferenceEquals(update, lastUpdate))
                    {
                        string signature = JsonSerializer.Serialize(update);
                        if (signature != lastActivity) { activity(update);lastActivity = signature; }
                        lastUpdate = update;
                    }
                }
                finally { session.Gate.Release(); }
                await Task.Delay(Configuration.PollMilliseconds, stopping);
                await session.Gate.WaitAsync(stopping);
                try
                {
                    try { session.Current = await GetAsync(session.Checkpoint, stopping); Remember(session); }
                    catch (Exception error) when (Transient(error, stopping)) { /* Keep the confirmed handle live during transport loss. */ }
                }
                finally { session.Gate.Release(); }
            }
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested) { throw; }
        catch (Exception error)
        {
            Console.Error.WriteLine("Remote adapter " + Id + ": " + error);
            // A lost response is not evidence that side effects failed or never happened.
            return HarnessOutcome.Interrupted("Remote execution could not be confirmed; inspect the backend before retrying. " + error.GetType().Name);
        }
        finally
        {
            await FinishAsync(session.Checkpoint);
            session.Ready.TrySetResult(false);
            session.Done.TrySetResult(session.Current?.Outcome?.State == HarnessTaskState.Cancelled);
            // Retain the settled session until host disposal (runtime record capacity is bounded).
            // A control admitted immediately before the runtime's terminal commit must still observe Done.
        }
    }
    private static void Remember(Session session)
    {
        if (session.Current.Id == null) return;
        if (session.Checkpoint.RemoteId != null && session.Checkpoint.RemoteId != session.Current.Id)
            throw new IOException("Remote task identity changed.");
        if (session.Checkpoint.ContextId != null && session.Current.ContextId != null && session.Checkpoint.ContextId != session.Current.ContextId)
            throw new IOException("Remote context identity changed.");
        if (session.Checkpoint.RemoteId == session.Current.Id && (session.Current.ContextId == null || session.Checkpoint.ContextId == session.Current.ContextId)) return;
        session.Checkpoint.RemoteId = session.Current.Id;
        session.Checkpoint.ContextId = session.Current.ContextId ?? session.Checkpoint.ContextId;
        session.Save();
    }
    protected static bool Transient(Exception error, CancellationToken stopping) => !stopping.IsCancellationRequested &&
        (error is HttpRequestException || error is TaskCanceledException || error is RemoteTransientException ||
         error is TaskHostRequestException host && (host.StatusCode == 408 || host.StatusCode == 429 || host.StatusCode >= 500));
    private async Task<RemoteSnapshot> GetConfirmedAsync(RemoteCheckpoint state, CancellationToken cancellation)
    {
        while (true)
        {
            try { return await GetAsync(state, cancellation); }
            catch (Exception error) when (Transient(error, cancellation)) { await Task.Delay(Configuration.PollMilliseconds, cancellation); }
        }
    }
    private async Task<RemoteSnapshot> RetryCancelAsync(RemoteCheckpoint state, CancellationToken cancellation)
    {
        while (true)
        {
            try { return await CancelRemoteAsync(state, cancellation); }
            catch (Exception error) when (Transient(error, cancellation)) { await Task.Delay(Configuration.PollMilliseconds, cancellation); }
        }
    }
    private Session GetSession(HarnessTask task) => _sessions.GetOrAdd(task.Id, _ => new Session());
    public virtual async Task<bool> CancelAsync(HarnessTask task, CancellationToken cancellation)
    {
        var session = GetSession(task);
        if (!await session.Ready.Task.WaitAsync(cancellation)) return false;
        await session.Gate.WaitAsync(cancellation);
        try
        {
            if (session.Current.Outcome != null) return session.Current.Outcome.State == HarnessTaskState.Cancelled;
            session.Current = await RetryCancelAsync(session.Checkpoint, cancellation);
            Remember(session);
            if (session.Current.Outcome != null) return session.Current.Outcome.State == HarnessTaskState.Cancelled;
        }
        finally { session.Gate.Release(); }
        return await session.Done.Task.WaitAsync(cancellation);
    }
    public async Task RespondAsync(HarnessTask task, string requestId, string reply, bool allow, CancellationToken cancellation)
    {
        var session = GetSession(task);
        if (!await session.Ready.Task.WaitAsync(cancellation)) throw new InvalidOperationException("Remote session unavailable.");
        await session.Gate.WaitAsync(cancellation);
        try
        {
            if (session.Current.Outcome != null || session.Current.Activity?.RequestId != requestId)
                throw new InvalidOperationException("Remote input request expired.");
            session.Current = await RespondRemoteAsync(session.Checkpoint, requestId, reply, allow, cancellation);
            Remember(session);
        }
        finally { session.Gate.Release(); }
    }
    public virtual Task UpdateAsync(HarnessTask task, string instruction, CancellationToken cancellation) =>
        throw new NotSupportedException("This remote adapter does not advertise mid-task steering.");
    protected abstract Task<RemoteSnapshot> AdmitAsync(HarnessTask task, RemoteCheckpoint state, Action save, CancellationToken cancellation);
    protected abstract Task<RemoteSnapshot> GetAsync(RemoteCheckpoint state, CancellationToken cancellation);
    protected abstract Task<RemoteSnapshot> CancelRemoteAsync(RemoteCheckpoint state, CancellationToken cancellation);
    protected abstract Task<RemoteSnapshot> RespondRemoteAsync(RemoteCheckpoint state, string requestId, string reply, bool allow, CancellationToken cancellation);
    public virtual ValueTask DisposeAsync() => ValueTask.CompletedTask;
    protected virtual ValueTask FinishAsync(RemoteCheckpoint state) => ValueTask.CompletedTask;
    private sealed class Session
    {
        internal readonly SemaphoreSlim Gate = new(1, 1);
        internal readonly TaskCompletionSource<bool> Ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource<bool> Done = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal RemoteCheckpoint Checkpoint;
        internal RemoteSnapshot Current;
        internal Action Save;
    }
}
