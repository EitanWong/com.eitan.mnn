using System;

namespace MNN.Samples.SiriAI.Harnesses
{
    public sealed partial class PersistentTaskRuntime
    {
        private void BindContinuation(HarnessTask next)
        {
            if (next.ContinuedFromTaskId == null) return;
            var original = Owned(next.ContinuedFromTaskId, next.OwnerId);
            if (original.State != HarnessTaskState.Completed)
                throw new InvalidOperationException("Only a completed task can be continued; use the pending request or running-task controls otherwise.");
            if (original.HarnessId != next.HarnessId || original.WorkspaceKey != next.WorkspaceKey ||
                original.ConversationId != next.ConversationId || original.DeliveryConversationId != next.DeliveryConversationId ||
                original.IsolatedContext != next.IsolatedContext)
                throw new ArgumentException("Continuation must retain the original harness, workspace and conversation binding.");
            var adapter = _adapters[next.HarnessId] as IHarnessContinuationAdapter;
            if (adapter == null || (adapter.Capabilities & HarnessCapabilities.Continue) == 0)
                throw new NotSupportedException("This harness does not support verified original-session continuation.");
            next.ContinuationState = HarnessText.Required(adapter.CreateContinuationCheckpoint(original.Copy()), 16384);
        }
    }
}
