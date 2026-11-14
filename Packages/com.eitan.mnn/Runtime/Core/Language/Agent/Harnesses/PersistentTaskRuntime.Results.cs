using System;
using System.Linq;

namespace MNN.Samples.SiriAI.Harnesses
{
    public sealed partial class PersistentTaskRuntime
    {
        public void RenewResult(string taskId, string ownerId, string leaseId, TimeSpan duration)
        {
            if (duration < TimeSpan.FromSeconds(1) || duration > TimeSpan.FromMinutes(5)) throw new ArgumentOutOfRangeException(nameof(duration));
            lock (_sync)
            {
                RequireOpen();
                var next = Changed(Leased(taskId, ownerId, leaseId));
                next.LeaseUntilUtcTicks = DateTime.UtcNow.Ticks + duration.Ticks;
                Commit(next);
            }
        }

        public HarnessTask ClaimResult(string ownerId, string conversationId, string clientId, TimeSpan duration)
        {
            HarnessText.Required(ownerId, 128);
            HarnessText.Required(conversationId, 128);
            HarnessText.Required(clientId, 128);
            if (duration < TimeSpan.FromSeconds(1) || duration > TimeSpan.FromMinutes(5)) throw new ArgumentOutOfRangeException(nameof(duration));
            lock (_sync)
            {
                RequireOpen();
                long now = DateTime.UtcNow.Ticks;
                var item = _tasks.Values.Where(task => task.OwnerId == ownerId && task.DeliveryConversationId == conversationId &&
                    task.ResultPending && (task.LeaseId == null || task.LeaseUntilUtcTicks <= now)).OrderBy(task => task.CreatedUtcTicks).FirstOrDefault();
                if (item == null) return null;
                var next = Changed(item);
                next.LeaseId = Guid.NewGuid().ToString("N");
                next.LeaseUntilUtcTicks = now + duration.Ticks;
                Commit(next);
                return next.Copy();
            }
        }
        public void AcknowledgeResult(string taskId, string ownerId, string leaseId, ResultReceipt receipt)
        {
            if (!Enum.IsDefined(typeof(ResultReceipt), receipt)) throw new ArgumentOutOfRangeException(nameof(receipt));
            lock (_sync)
            {
                RequireOpen();
                var item = Leased(taskId, ownerId, leaseId);
                if (!item.PlaybackStarted && (receipt == ResultReceipt.PlaybackCompleted || receipt == ResultReceipt.Interrupted))
                    throw new InvalidOperationException("Playback has not started.");
                var next = Changed(item);
                if (receipt == ResultReceipt.ContextInjected) next.ContextInjected = true;
                if (receipt == ResultReceipt.PlaybackStarted) { next.PlaybackStarted = true; next.ResultPending = false; }
                if (receipt == ResultReceipt.PlaybackCompleted) next.PlaybackCompleted = true;
                if (receipt == ResultReceipt.Interrupted) next.PlaybackInterrupted = true;
                if (receipt == ResultReceipt.TextDisplayed) next.ResultPending = false;
                Commit(next);
            }
        }
        public void ReleaseResult(string taskId, string ownerId, string leaseId)
        {
            lock (_sync)
            {
                RequireOpen();
                var item = Leased(taskId, ownerId, leaseId);
                var next = Changed(item);
                next.LeaseId = null;
                next.LeaseUntilUtcTicks = 0;
                Commit(next);
            }
        }
        private HarnessTask Leased(string taskId, string ownerId, string leaseId)
        {
            var task = Owned(taskId, ownerId);
            if (string.IsNullOrEmpty(leaseId) || task.LeaseId != leaseId || task.LeaseUntilUtcTicks <= DateTime.UtcNow.Ticks)
                throw new UnauthorizedAccessException("Result lease is missing or expired.");
            return task;
        }
    }
}
