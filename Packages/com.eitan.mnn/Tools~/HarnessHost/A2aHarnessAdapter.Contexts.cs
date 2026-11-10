using System.Collections.Concurrent;
using MNN.Samples.SiriAI.Harnesses;

namespace SiriAI.HarnessHost;

internal sealed partial class A2aHarnessAdapter
{
    private readonly A2aContextCache _contexts = new();
    private readonly ConcurrentDictionary<RemoteCheckpoint, A2aContextCache.Lease> _contextLeases = new();
    private readonly ConcurrentDictionary<string, ContextAdmission> _contextAdmissions = new();
    private sealed class ContextAdmission : IDisposable
    {
        internal readonly CancellationTokenSource Cancellation = new();
        private readonly object _sync = new();
        private bool _waiting = true;
        internal bool Cancel()
        {
            lock (_sync)
            {
                if (!_waiting) return false;
                Cancellation.Cancel(); return true;
            }
        }
        internal bool BeginSend()
        { lock (_sync) { _waiting = false; return !Cancellation.IsCancellationRequested; } }
        public void Dispose() { lock (_sync) { _waiting = false; Cancellation.Dispose(); } }
    }
    private async Task<bool> AcquireContextAsync(HarnessTask task, RemoteCheckpoint state, CancellationToken stopping)
    {
        if (!Configuration.ReuseContext || task.IsolatedContext) return true;
        using var admission = new ContextAdmission();
        _contextAdmissions[task.Id] = admission;
        try
        {
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(stopping, admission.Cancellation.Token);
            var lease = await _contexts.AcquireAsync(task.OwnerId, cancellation.Token);
            if (!admission.BeginSend()) { lease.Dispose(); return false; }
            _contextLeases[state] = lease;
            state.ContextId = lease.Id;
            return true;
        }
        catch (OperationCanceledException) when (!stopping.IsCancellationRequested && admission.Cancellation.IsCancellationRequested)
        { return false; }
        finally { _contextAdmissions.TryRemove(task.Id, out _); }
    }
    private RemoteSnapshot ObserveContext(RemoteCheckpoint state, RemoteSnapshot snapshot)
    {
        if (state.RemoteId != null && snapshot.Id != null && state.RemoteId != snapshot.Id)
            throw new IOException("A2A agent changed the remote task identity.");
        string context = snapshot.ContextId;
        if (context != null && (string.IsNullOrWhiteSpace(context) || context.Length > 128))
            throw new IOException("Invalid A2A context identity.");
        if (state.ContextId != null && context != null && state.ContextId != context)
            throw new IOException("A2A agent changed the task context.");
        if (_contextLeases.TryGetValue(state, out var lease)) lease.Remember(context);
        return snapshot;
    }
    public override Task<bool> CancelAsync(HarnessTask task, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        return _contextAdmissions.TryGetValue(task.Id, out var admission) && admission.Cancel()
            ? Task.FromResult(true) : base.CancelAsync(task, cancellation);
    }
}
