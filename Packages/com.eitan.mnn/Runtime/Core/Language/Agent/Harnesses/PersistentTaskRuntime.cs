using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Samples.SiriAI.Harnesses
{
    /// <summary>Serial state owner; adapters run independently of callers and frontend connections.</summary>
    public sealed partial class PersistentTaskRuntime : IDisposable
    {
        private readonly object _sync = new object();
        private readonly IHarnessTaskStore _store;
        private readonly Dictionary<string, IHarnessAdapter> _adapters;
        private Dictionary<string, HarnessTask> _tasks;
        private readonly Dictionary<string, Task> _workers = new Dictionary<string, Task>();
        private readonly Dictionary<string, Task> _cancellations = new Dictionary<string, Task>();
        private readonly Dictionary<string, CancellationTokenSource> _localCancellations = new Dictionary<string, CancellationTokenSource>();
        private readonly HashSet<string> _lanes = new HashSet<string>(StringComparer.Ordinal);
        private readonly CancellationTokenSource _stopping = new CancellationTokenSource();
        private readonly int _maximumConcurrent;
        private readonly int _maximumRecords;
        private Exception _fault;
        private bool _disposed;
        public Exception Fault { get { lock (_sync) return _fault; } }

        public PersistentTaskRuntime(IHarnessTaskStore store, IEnumerable<IHarnessAdapter> adapters,
            int maximumConcurrent = 1, int maximumRecords = 10000)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            if (maximumConcurrent < 1 || maximumConcurrent > 64) throw new ArgumentOutOfRangeException(nameof(maximumConcurrent));
            if (maximumRecords < 1 || maximumRecords > 100000) throw new ArgumentOutOfRangeException(nameof(maximumRecords));
            _maximumConcurrent = maximumConcurrent;
            _maximumRecords = maximumRecords;
            _adapters = (adapters ?? throw new ArgumentNullException(nameof(adapters))).ToDictionary(adapter => HarnessText.Required(adapter.Id, 128), StringComparer.Ordinal);
            var loaded = store.Load();
            if (loaded.Length > maximumRecords) throw new InvalidOperationException("Task store exceeds configured record capacity.");
            foreach (var task in loaded) task.Validate();
            _tasks = loaded.ToDictionary(task => task.Id, task => task.Copy(), StringComparer.Ordinal);
            ValidateCollections();
            RecoverInterruptedWork();
            lock (_sync) Schedule();
        }

        private void RecoverInterruptedWork()
        {
            bool changed = false;
            foreach (var task in _tasks.Values)
            {
                if (RecoverInstructions(task)) changed = true;
                // Never blindly replay a side effect after losing its execution handle.
                // Queued can start once if its harness still exists; Running is committed before any adapter call.
                if (!task.IsTerminal && (task.State != HarnessTaskState.Queued && task.State != HarnessTaskState.Paused || !_adapters.ContainsKey(task.HarnessId)) && !CanRecover(task))
                {
                    bool unstarted = task.State == HarnessTaskState.Queued || task.State == HarnessTaskState.Paused;
                    task.State = HarnessTaskState.Interrupted;
                    task.Result = unstarted ? "The task harness is no longer configured. Queued work was not started." :
                        "The host restarted before execution could be confirmed. Work was not replayed; inspect the backend before retrying.";
                    task.ResultPending = true;
                    task.WaitingFor = HarnessWaitingFor.None;
                    task.RequestId = null;
                    task.InputForm = null;
                    task.InputPrompt = null;
                    task.Revision++;
                    task.UpdatedUtcTicks = DateTime.UtcNow.Ticks;
                    changed = true;
                }
                if (task.LeaseId != null) { task.LeaseId = null; task.LeaseUntilUtcTicks = 0; changed = true; }
            }
            if (changed) _store.Save(_tasks.Values.ToArray());
        }

        private bool CanRecover(HarnessTask task) => task.BackendState != null &&
            _adapters.TryGetValue(task.HarnessId, out var adapter) && adapter is IRecoverableHarnessAdapter recoverable &&
            recoverable.CanRecover(task.BackendState);

        public HarnessTask Submit(TaskSubmission request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            lock (_sync)
            {
                RequireOpen();
                var existing = _tasks.Values.FirstOrDefault(item => item.OwnerId == request.OwnerId && item.SubmissionKey == request.SubmissionKey);
                if (existing != null)
                {
                    if (existing.Objective != request.Objective || existing.HarnessId != request.HarnessId ||
                        existing.WorkspaceKey != request.WorkspaceKey || existing.ConversationId != request.ConversationId ||
                        existing.ResultConversationId != request.ResultConversationId || existing.Title != request.Title ||
                        existing.IsolatedContext != request.IsolatedContext ||
                        !HarnessContentPart.SameInputs(existing.Attachments, request.Attachments))
                        throw new ArgumentException("The submission key already belongs to a different request.");
                    return existing.Copy();
                }
                if (!_adapters.ContainsKey(request.HarnessId)) throw new ArgumentException("The requested harness is not configured.");
                if (request.Attachments.Length > 0 && (_adapters[request.HarnessId].Capabilities & HarnessCapabilities.Attachments) == 0)
                    throw new NotSupportedException("This harness does not support input attachments.");
                (_adapters[request.HarnessId] as IHarnessSubmissionValidator)?.ValidateSubmission(request);
                if (_tasks.Count >= _maximumRecords) throw new InvalidOperationException("Task record capacity reached.");
                var task = HarnessTask.Create(request);
                string admission = (_adapters[request.HarnessId] as IHarnessAdmissionCheckpoint)?.CreateAdmissionCheckpoint(request);
                task.BackendState = admission == null ? null : HarnessText.Required(admission, 16384);
                Commit(task);
                Schedule();
                return _tasks[task.Id].Copy();
            }
        }

        public HarnessTask Get(string taskId, string ownerId) { lock (_sync) { RequireOpen(); return Owned(taskId, ownerId).Copy(); } }
        public HarnessTask[] List(string ownerId)
        {
            HarnessText.Required(ownerId, 128);
            lock (_sync) { RequireOpen(); return _tasks.Values.Where(task => task.OwnerId == ownerId).OrderBy(task => task.CreatedUtcTicks).Select(task => task.Copy()).ToArray(); }
        }
        private HarnessTask Owned(string taskId, string ownerId)
        {
            if (!_tasks.TryGetValue(taskId, out var task) || task.OwnerId != ownerId)
                throw new UnauthorizedAccessException("Task is not available to this owner.");
            return task;
        }
        private void Commit(HarnessTask task)
        {
            var next = new Dictionary<string, HarnessTask>(_tasks, StringComparer.Ordinal) { [task.Id] = task };
            _store.Save(next.Values.Select(item => item.Copy()).ToArray());
            _tasks = next;
        }
        private HarnessTask Changed(HarnessTask task)
        {
            var next = task.Copy();
            next.Revision++;
            next.UpdatedUtcTicks = DateTime.UtcNow.Ticks;
            return next;
        }
        private void RequireOpen()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(PersistentTaskRuntime));
            if (_fault != null) throw new InvalidOperationException("Task runtime failed; host recovery is required.", _fault);
        }
        private void Schedule()
        {
            if (_disposed || _fault != null) return;
            foreach (var item in _tasks.Values.OrderBy(task => task.CreatedUtcTicks).ToArray())
            {
                bool coordinator = _adapters.TryGetValue(item.HarnessId, out var candidate) && candidate is IIsolatedCoordinatorHarness;
                int running = _workers.Keys.Count(id => (_adapters[_tasks[id].HarnessId] is IIsolatedCoordinatorHarness) == coordinator);
                if (running >= _maximumConcurrent) continue;
                if (_workers.ContainsKey(item.Id) || item.IsTerminal || item.State == HarnessTaskState.Paused || _lanes.Contains(ExecutionLane(item))) continue;
                if (item.State != HarnessTaskState.Queued && !CanRecover(item)) continue;
                var task = Changed(item);
                if (task.State != HarnessTaskState.Cancelling) task.State = HarnessTaskState.Running;
                Commit(task);
                _lanes.Add(ExecutionLane(task));
                var requested = CancellationToken.None;
                if (_adapters[task.HarnessId] is ILocalCancellationHarnessAdapter)
                {
                    var source = new CancellationTokenSource();
                    _localCancellations.Add(task.Id, source);
                    requested = source.Token;
                }
                _workers.Add(task.Id, Task.Run(() => ExecuteAsync(task.Copy(), requested)));
            }
        }
        private string ExecutionLane(HarnessTask task) => _adapters.TryGetValue(task.HarnessId, out var adapter) && adapter is IIsolatedCoordinatorHarness coordinator
            ? "coordinator:" + HarnessText.Required(coordinator.CoordinatorLane(task), 1024) : task.WorkspaceKey;
        private async Task ExecuteAsync(HarnessTask task, CancellationToken cancellationRequested)
        {
            try
            {
                HarnessOutcome outcome;
                try
                {
                    var adapter = _adapters[task.HarnessId];
                    outcome = adapter is ILocalCancellationHarnessAdapter local
                        ? await local.RunCancellableAsync(task, activity => Publish(task.Id, activity), cancellationRequested, _stopping.Token).ConfigureAwait(false)
                        : adapter is IRecoverableHarnessAdapter durable
                        ? await durable.RunDurableAsync(task, activity => Publish(task.Id, activity), checkpoint => SaveCheckpoint(task.Id, checkpoint), _stopping.Token).ConfigureAwait(false)
                        : await adapter.RunAsync(task, activity => Publish(task.Id, activity), _stopping.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (_stopping.IsCancellationRequested) { return; }
                catch (OperationCanceledException) { outcome = HarnessOutcome.Failed("Backend execution ended without a confirmed final result."); }
                catch (Exception error) { outcome = HarnessOutcome.Failed(error.Message.Length > 4096 ? error.Message.Substring(0, 4096) : error.Message); }
                if (outcome == null) throw new InvalidOperationException("Backend returned no final outcome.");
                lock (_sync) { Complete(task.Id, outcome); }
            }
            catch (Exception error) { lock (_sync) _fault = error; }
            finally
            {
                lock (_sync)
                {
                    _workers.Remove(task.Id);
                    if (_localCancellations.TryGetValue(task.Id, out var source))
                    {
                        _localCancellations.Remove(task.Id);
                        source.Dispose();
                    }
                    _lanes.Remove(ExecutionLane(task));
                    try { Schedule(); }
                    catch (Exception error) { _fault = error; }
                }
            }
        }
        private void Complete(string taskId, HarnessOutcome outcome)
        {
            if (_disposed || _tasks[taskId].IsTerminal) return;
            if (outcome == null) throw new InvalidOperationException("Backend returned no final outcome.");
            var next = Changed(_tasks[taskId]);
            next.State = outcome.State;
            next.Result = outcome.Text;
            HarnessExecutionStep.Append(next, HarnessExecutionKind.Result, next.HarnessId + " result", outcome.Text, outcome.State.ToString());
            next.Artifacts = outcome.Artifacts;
            next.ResultPending = outcome.State != HarnessTaskState.Cancelled;
            next.WaitingFor = HarnessWaitingFor.None;
            next.RequestId = null;
            next.InputForm = null;
            next.InputPrompt = null;
            CommitCollected(next, outcome.CollectedResults);
        }
        private void SaveCheckpoint(string taskId, string checkpoint)
        {
            HarnessText.Required(checkpoint, 16384);
            lock (_sync)
            {
                RequireOpen();
                var next = Changed(_tasks[taskId]);
                if (next.IsTerminal) return;
                next.BackendState = checkpoint;
                Commit(next);
            }
        }
        private void Publish(string taskId, HarnessActivity activity)
        {
            if (activity == null) throw new ArgumentNullException(nameof(activity));
            lock (_sync)
            {
                if (_disposed || _tasks[taskId].IsTerminal || _tasks[taskId].State == HarnessTaskState.Cancelling) return;
                var next = Changed(_tasks[taskId]);
                next.WaitingFor = activity.WaitingFor;
                next.RequestId = activity.RequestId;
                next.InputForm = activity.InputForm;
                next.InputPrompt = activity.InputPrompt;
                next.Activity = activity.Summary;
                if (activity.NativeTools != null) next.NativeTools = activity.NativeTools;
                if (activity.SessionConfiguration != null) next.SessionConfiguration = activity.SessionConfiguration;
                HarnessExecutionStep.Append(next, activity.ExecutionKind, activity.Summary, activity.ExecutionDetail, activity.ExecutionState);
                if (activity.Artifacts != null) next.Artifacts = activity.Artifacts;
                Commit(next);
            }
        }
        public async Task WaitForIdleAsync()
        {
            while (true)
            {
                Task[] workers;
                lock (_sync) { RequireOpen(); workers = _workers.Values.Concat(_instructionWorkers.Values).ToArray(); }
                if (workers.Length == 0) return;
                await Task.WhenAll(workers).ConfigureAwait(false);
            }
        }
        public void Dispose()
        {
            lock (_sync) { if (_disposed) return; _disposed = true; }
            // A host shutdown is persisted as interrupted on reopening, never as cancellation success.
            _stopping.Cancel();
            _stopping.Dispose();
        }
    }
}
