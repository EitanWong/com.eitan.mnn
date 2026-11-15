using System;
using System.Threading.Tasks;
using MNN.Samples.SiriAI.Harnesses;
using NUnit.Framework;

namespace MNN.Samples.SiriAI.Tests
{
    public sealed class SiriAIHarnessDeliveryFailureTests
    {
        [Test] public void FailedPresentationDoesNotStarveLaterResults() => Fixtures.HarnessTestCase.Run(VerifyAsync);
        public async Task VerifyAsync()
        {
            var store = new MemoryTaskStore();
            var adapter = new Fixtures.ControlledHarness(); adapter.Finish.SetResult("verified result");
            string firstId;
            using (var runtime = new PersistentTaskRuntime(store, new[] { adapter }))
            {
                firstId = runtime.Submit(new TaskSubmission("owner", "chat", "one", "controlled", "project", "first")).Id;
                var second = runtime.Submit(new TaskSubmission("owner", "chat", "two", "controlled", "project", "second"));
                await runtime.WaitForIdleAsync();
                var first = runtime.ClaimResult("owner", "chat", "client", TimeSpan.FromSeconds(60));
                Assert.AreEqual(firstId, first.Id);
                Assert.Throws<UnauthorizedAccessException>(() => runtime.FailResultDelivery(first.Id, "owner", "wrong", "Failed"));
                runtime.FailResultDelivery(first.Id, "owner", first.LeaseId, "Presenter failed");
                var next = runtime.ClaimResult("owner", "chat", "client", TimeSpan.FromSeconds(60));
                Assert.AreEqual(second.Id, next.Id, "A failing oldest result must not monopolize the outbox.");
                runtime.AcknowledgeResult(next.Id, "owner", next.LeaseId, ResultReceipt.TextDisplayed);
                var pending = runtime.Get(firstId, "owner");
                Assert.True(pending.ResultPending); Assert.False(pending.PlaybackStarted);
                Assert.AreEqual(1, pending.DeliveryAttempts); Assert.AreEqual("Presenter failed", pending.DeliveryError);
                Assert.AreEqual(HarnessTaskState.Completed, pending.State);
            }
            using (var reopened = new PersistentTaskRuntime(store, new[] { adapter }))
            {
                Assert.AreEqual(1, reopened.Get(firstId, "owner").DeliveryAttempts);
                reopened.RetryResultDelivery(firstId, "owner");
                Assert.AreEqual(firstId, reopened.ClaimResult("owner", "chat", "client", TimeSpan.FromSeconds(60)).Id);
            }
        }
    }
}
