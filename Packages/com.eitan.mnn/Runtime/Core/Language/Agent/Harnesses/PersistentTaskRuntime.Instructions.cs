using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Samples.SiriAI.Harnesses
{
    public sealed partial class PersistentTaskRuntime
    {
        private readonly Dictionary<string, Task> _instructionWorkers = new Dictionary<string, Task>();
        public Task UpdateAsync(string taskId, string ownerId, string instruction, CancellationToken cancellation, string instructionId = null, bool waitForDelivery = true) =>
            UpdateContentAsync(taskId, ownerId, instruction, null, cancellation, instructionId, waitForDelivery);
        public async Task UpdateContentAsync(string taskId, string ownerId, string instruction, HarnessContentPart[] attachments,
            CancellationToken cancellation, string instructionId = null, bool waitForDelivery = true)
        {
            attachments = HarnessContentPart.CheckedInputs(attachments);
            Task work;
            lock (_sync)
            {
                RequireOpen(); cancellation.ThrowIfCancellationRequested();
                var task = Owned(taskId, ownerId);
                instruction = HarnessText.Required(instruction, 32768);
                instructionId = instructionId == null ? Guid.NewGuid().ToString("N") : HarnessText.Required(instructionId, 128);
                string key = task.Id + "\n" + instructionId;
                var existing = task.Instructions?.FirstOrDefault(i => i.Id == instructionId);
                if (existing != null)
                {
                    if (existing.Text != instruction || !HarnessContentPart.SameInputs(existing.Attachments, attachments)) throw new InvalidOperationException("Instruction identity already belongs to different text or content.");
                    if (!_instructionWorkers.TryGetValue(key, out work)) return;
                }
                else
                {
                    if (task.State != HarnessTaskState.Running) throw new InvalidOperationException("Task is not accepting updates.");
                    if ((_adapters[task.HarnessId].Capabilities & HarnessCapabilities.Update) == 0) throw new NotSupportedException("Harness does not support steering.");
                    var adapter = _adapters[task.HarnessId];
                    var contentAdapter = adapter as IHarnessInstructionAdapter;
                    if (attachments.Length > 0 && (contentAdapter == null || (adapter.Capabilities & HarnessCapabilities.UpdateAttachments) == 0)) throw new NotSupportedException("Harness does not support new attachments on an active instruction.");
                    var admitted = new HarnessInstruction(instructionId, instruction, HarnessInstructionState.Pending, "Admitted; native delivery is not confirmed.", attachments);
                    contentAdapter?.ValidateInstruction(task, admitted);
                    var next = Changed(task);
                    next.Instructions = (task.Instructions ?? new HarnessInstruction[0]).Concat(new[] {
                        admitted }).ToArray();
                    HarnessInstruction.ValidateAll(next.Instructions);
                    Commit(next); // One durable identity before any backend side effect.
                    var snapshot = next.Copy();
                    work = Task.Run(() => DeliverInstructionAsync(snapshot, admitted, key));
                    _instructionWorkers.Add(key, work);
                }
            }
            if (!waitForDelivery) return;
            var disconnected = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellation.Register(() => disconnected.TrySetResult(true)))
            {
                if (await Task.WhenAny(work, disconnected.Task).ConfigureAwait(false) != work) cancellation.ThrowIfCancellationRequested();
                await work.ConfigureAwait(false);
            }
        }
        private async Task DeliverInstructionAsync(HarnessTask task, HarnessInstruction instruction, string key)
        {
            HarnessInstructionState state; string detail;
            try
            {
                if (_adapters[task.HarnessId] is IHarnessInstructionAdapter contentAdapter)
                    await contentAdapter.UpdateInstructionAsync(task, instruction, _stopping.Token).ConfigureAwait(false);
                else await _adapters[task.HarnessId].UpdateAsync(task, instruction.Text, _stopping.Token).ConfigureAwait(false);
                state = HarnessInstructionState.Delivered; detail = "Backend confirmed instruction delivery; task completion is separate.";
            }
            catch (Exception error) when (error is NotSupportedException || error is ArgumentException || error is InvalidOperationException)
            { state = HarnessInstructionState.Rejected; detail = "Backend did not accept this instruction."; }
            catch (Exception)
            { state = HarnessInstructionState.Uncertain; detail = "Delivery is unconfirmed; no instruction was retried."; }
            lock (_sync)
            {
                try
                {
                    if (_disposed) return;
                    var next = Changed(_tasks[task.Id]);
                    next.Instructions = next.Instructions.Select(i => i.Id == instruction.Id ? i.WithState(state, detail) : i).ToArray();
                    Commit(next);
                }
                finally { _instructionWorkers.Remove(key); }
            }
        }
        private static bool RecoverInstructions(HarnessTask task)
        {
            if (task.Instructions == null || !task.Instructions.Any(i => i.State == HarnessInstructionState.Pending)) return false;
            task.Instructions = task.Instructions.Select(i => i.State == HarnessInstructionState.Pending ?
                i.WithState(HarnessInstructionState.Uncertain, "Host restarted before delivery was confirmed; no retry was sent.") : i).ToArray();
            task.Revision++; task.UpdatedUtcTicks = DateTime.UtcNow.Ticks; return true;
        }
    }
}
