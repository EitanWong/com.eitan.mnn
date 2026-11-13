using System;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Samples.SiriAI.Harnesses
{
    public sealed partial class PersistentTaskRuntime
    {
        /// <summary>Hold only work that has not started. Never cancel and replay an executing side effect.</summary>
        public HarnessTask Pause(string taskId, string ownerId)
        {
            lock (_sync)
            {
                RequireOpen();
                var task = Owned(taskId, ownerId);
                if (task.State == HarnessTaskState.Paused) return task.Copy();
                if (task.State != HarnessTaskState.Queued)
                    throw new NotSupportedException("Only queued tasks can be paused. This backend does not provide confirmed execution suspension.");
                var next = Changed(task);
                next.State = HarnessTaskState.Paused;
                Commit(next);
                return next.Copy();
            }
        }
        public HarnessTask Resume(string taskId, string ownerId)
        {
            lock (_sync)
            {
                RequireOpen();
                var task = Owned(taskId, ownerId);
                if (task.State == HarnessTaskState.Queued || task.State == HarnessTaskState.Running) return task.Copy();
                if (task.State != HarnessTaskState.Paused)
                    throw new InvalidOperationException("Only a paused task can be resumed. Finished work cannot be replayed.");
                var next = Changed(task);
                next.State = HarnessTaskState.Queued;
                Commit(next);
                Schedule();
                return _tasks[taskId].Copy();
            }
        }
        /// <summary>Admit cancellation without occupying a foreground turn until the backend exits.</summary>
        public HarnessTask RequestCancel(string taskId, string ownerId)
        {
            lock (_sync)
            {
                var pending = CancelAsync(taskId, ownerId, CancellationToken.None);
                _ = ObserveCancellationAsync(taskId, pending);
                return Owned(taskId, ownerId).Copy();
            }
        }
        private async Task ObserveCancellationAsync(string taskId, Task pending)
        {
            try { await pending.ConfigureAwait(false); }
            catch (Exception error)
            {
                lock (_sync)
                {
                    if (_disposed || _tasks[taskId].IsTerminal) return;
                    var next = Changed(_tasks[taskId]);
                    next.Activity = "Cancellation unconfirmed: " + HarnessExecutionStep.Clip(error.Message, 4000);
                    HarnessExecutionStep.Append(next, HarnessExecutionKind.Error, "Cancellation unconfirmed", next.Activity, "Failed");
                    try { Commit(next); }
                    catch (Exception persistenceError) { _fault = persistenceError; }
                }
            }
        }
        public Task CancelAsync(string taskId, string ownerId, CancellationToken cancellation)
        {
            lock (_sync)
            {
                RequireOpen();
                cancellation.ThrowIfCancellationRequested();
                var task = Owned(taskId, ownerId);
                if (task.IsTerminal) return Task.CompletedTask;
                if (_cancellations.TryGetValue(task.Id, out var current)) return current;
                var next = Changed(task);
                if (task.State == HarnessTaskState.Queued || task.State == HarnessTaskState.Paused)
                { next.State = HarnessTaskState.Cancelled; Commit(next); return Task.CompletedTask; }
                if ((_adapters[task.HarnessId].Capabilities & HarnessCapabilities.Cancel) == 0)
                    throw new NotSupportedException("This harness does not support confirmed cancellation.");
                next.State = HarnessTaskState.Cancelling;
                Commit(next);
                // Do not propagate a disconnected client's token into an admitted cancellation.
                var work = Task.Run(() => CancelCoreAsync(next.Copy()));
                _cancellations.Add(task.Id, work);
                return work;
            }
        }
        private async Task CancelCoreAsync(HarnessTask task)
        {
            try
            {
                if (_adapters[task.HarnessId] is IHarnessCancellationOutcomeAdapter native)
                {
                    var outcome = await native.CancelWithOutcomeAsync(task, _stopping.Token).ConfigureAwait(false);
                    lock (_sync) { Complete(task.Id, outcome); }
                    return;
                }
                if (_adapters[task.HarnessId] is ILocalCancellationHarnessAdapter)
                {
                    Task worker;
                    lock (_sync)
                    {
                        if (_disposed || _tasks[task.Id].IsTerminal) return;
                        _localCancellations[task.Id].Cancel();
                        worker = _workers[task.Id];
                    }
                    // Only Run may decide whether completion or the local abort won the race.
                    await worker.ConfigureAwait(false);
                    return;
                }
                bool confirmed = await _adapters[task.HarnessId].CancelAsync(task, _stopping.Token).ConfigureAwait(false);
                lock (_sync)
                {
                    if (_disposed || _tasks[task.Id].IsTerminal) return;
                    if (!confirmed) throw new InvalidOperationException("Backend cancellation has not been confirmed.");
                    var next = Changed(_tasks[task.Id]);
                    next.State = HarnessTaskState.Cancelled;
                    next.WaitingFor = HarnessWaitingFor.None;
                    next.RequestId = null;
                    next.InputForm = null;
                    next.InputPrompt = null;
                    Commit(next);
                }
            }
            finally { lock (_sync) _cancellations.Remove(task.Id); }
        }
        public async Task RespondAsync(string taskId, string ownerId, string requestId, string reply, bool allow, CancellationToken cancellation,
            HarnessInputAction inputAction = HarnessInputAction.Answer)
        {
            HarnessTask task;
            lock (_sync)
            {
                RequireOpen();
                task = Owned(taskId, ownerId).Copy();
                if (task.State != HarnessTaskState.Running || task.WaitingFor == HarnessWaitingFor.None || task.RequestId != requestId)
                    throw new InvalidOperationException("This input or permission request is no longer pending.");
                if (task.InputPrompt != null) HarnessInputPrompt.ReplyText(reply);
                else HarnessText.Required(reply, 8192);
                if (!Enum.IsDefined(typeof(HarnessInputAction), inputAction)) throw new ArgumentException("Unknown task input action.");
                if (inputAction != HarnessInputAction.Answer && (task.WaitingFor != HarnessWaitingFor.Input || !(_adapters[task.HarnessId] is IHarnessInputActionAdapter)))
                    throw new NotSupportedException("This pending request does not support the selected input action.");
            }
            if (task.WaitingFor == HarnessWaitingFor.Input && _adapters[task.HarnessId] is IHarnessInputActionAdapter input)
                await input.RespondInputAsync(task, requestId, reply, inputAction, cancellation).ConfigureAwait(false);
            else await _adapters[task.HarnessId].RespondAsync(task, requestId, reply, allow, cancellation).ConfigureAwait(false);
            lock (_sync)
            {
                RequireOpen();
                var current = Owned(taskId, ownerId);
                if (current.IsTerminal || current.RequestId != requestId) return;
                var next = Changed(current);
                next.WaitingFor = HarnessWaitingFor.None;
                next.RequestId = null;
                next.InputForm = null;
                next.InputPrompt = null;
                Commit(next);
            }
        }
    }
}
