using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MNN.Samples.SiriAI.Harnesses;
using NUnit.Framework;

namespace MNN.Samples.SiriAI.Tests
{
    public sealed class SiriAITaskRuntimeTests
    {
        [Test]
        public void ActiveResultDeliveryCanRenewOnlyItsMatchingLeaseInEditor() => Fixtures.HarnessTestCase.Run(ActiveResultDeliveryCanRenewOnlyItsMatchingLease);

        public async Task ActiveResultDeliveryCanRenewOnlyItsMatchingLease()
        {
            var harness = new Fixtures.ControlledHarness();
            using (var runtime = new PersistentTaskRuntime(new MemoryTaskStore(), new[] { harness }))
            {
                var task = runtime.Submit(Request());
                harness.Finish.TrySetResult("done");
                await runtime.WaitForIdleAsync();
                var lease = runtime.ClaimResult("alice", "chat", "phone", TimeSpan.FromSeconds(1));
                Assert.Throws<UnauthorizedAccessException>(() => runtime.RenewResult(task.Id, "alice", "other", TimeSpan.FromMinutes(1)));
                runtime.RenewResult(task.Id, "alice", lease.LeaseId, TimeSpan.FromMinutes(1));
                var renewed = runtime.Get(task.Id, "alice");
                Assert.Greater(renewed.LeaseUntilUtcTicks, lease.LeaseUntilUtcTicks);
                Assert.AreEqual(lease.LeaseId, renewed.LeaseId);
                runtime.AcknowledgeResult(task.Id, "alice", lease.LeaseId, ResultReceipt.PlaybackStarted);
                runtime.RenewResult(task.Id, "alice", lease.LeaseId, TimeSpan.FromMinutes(1));
                Assert.False(runtime.Get(task.Id, "alice").ResultPending);
                Assert.False(runtime.Get(task.Id, "alice").PlaybackCompleted);
            }
        }

        private static TaskSubmission Request(string key = "call-1", string owner = "alice", string lane = "project") =>
            new TaskSubmission(owner, "chat", key, "controlled", lane, "Create the requested artifact.");

        [Test]
        public void SubmissionReturnsBeforeWorkAndDisconnectDoesNotCancelItInEditor() => Fixtures.HarnessTestCase.Run(SubmissionReturnsBeforeWorkAndDisconnectDoesNotCancelIt);

        public async Task SubmissionReturnsBeforeWorkAndDisconnectDoesNotCancelIt()
        {
            var harness = new Fixtures.ControlledHarness();
            using (var runtime = new PersistentTaskRuntime(new MemoryTaskStore(), new[] { harness }))
            {
                var task = runtime.Submit(Request());
                await harness.Started.Task;
                Assert.AreEqual(HarnessTaskState.Running, runtime.Get(task.Id, "alice").State);
                var second = runtime.Submit(Request("second"));
                Assert.AreEqual(HarnessTaskState.Queued, second.State);
                Assert.AreEqual(1, harness.Runs);
                harness.Finish.TrySetResult("Artifact ready");
                await runtime.WaitForIdleAsync();
                Assert.AreEqual("Artifact ready", runtime.Get(task.Id, "alice").Result);
                Assert.AreEqual(HarnessTaskState.Completed, runtime.Get(second.Id, "alice").State);
            }
        }

        [Test]
        public void DuplicateAdmissionIsIdempotentAndOwnerIsEnforcedInEditor() => Fixtures.HarnessTestCase.Run(DuplicateAdmissionIsIdempotentAndOwnerIsEnforced);

        public async Task DuplicateAdmissionIsIdempotentAndOwnerIsEnforced()
        {
            var harness = new Fixtures.ControlledHarness();
            using (var runtime = new PersistentTaskRuntime(new MemoryTaskStore(), new[] { harness }))
            {
                var first = runtime.Submit(Request());
                Assert.AreEqual(first.Id, runtime.Submit(Request()).Id);
                Assert.Throws<UnauthorizedAccessException>(() => runtime.Get(first.Id, "bob"));
                Assert.Throws<ArgumentException>(() => runtime.Submit(new TaskSubmission("alice", "chat", "call-1", "controlled", "project", "A different objective")));
                harness.Finish.TrySetResult("done");
                await runtime.WaitForIdleAsync();
                Assert.AreEqual(1, harness.Runs);
            }
        }

        [Test]
        public void CompletionIsDurableAndDeliveryRequiresMatchingLeaseInEditor() => Fixtures.HarnessTestCase.Run(CompletionIsDurableAndDeliveryRequiresMatchingLease);

        public async Task CompletionIsDurableAndDeliveryRequiresMatchingLease()
        {
            var store = new MemoryTaskStore();
            var harness = new Fixtures.ControlledHarness();
            string id;
            using (var runtime = new PersistentTaskRuntime(store, new[] { harness }))
            {
                id = runtime.Submit(Request()).Id;
                harness.Finish.TrySetResult("durable result");
                await runtime.WaitForIdleAsync();
            }
            using (var runtime = new PersistentTaskRuntime(store, new[] { harness }))
            {
                Assert.AreEqual("durable result", runtime.Get(id, "alice").Result);
                var claim = runtime.ClaimResult("alice", "chat", "phone", TimeSpan.FromMinutes(1));
                Assert.IsNotNull(claim);
                Assert.IsNull(runtime.ClaimResult("alice", "chat", "desktop", TimeSpan.FromMinutes(1)));
                Assert.Throws<UnauthorizedAccessException>(() => runtime.AcknowledgeResult(id, "alice", "wrong", ResultReceipt.PlaybackStarted));
                runtime.AcknowledgeResult(id, "alice", claim.LeaseId, ResultReceipt.ContextInjected);
                runtime.ReleaseResult(id, "alice", claim.LeaseId);
                var again = runtime.ClaimResult("alice", "chat", "desktop", TimeSpan.FromMinutes(1));
                Assert.IsTrue(again.ContextInjected);
                runtime.AcknowledgeResult(id, "alice", again.LeaseId, ResultReceipt.PlaybackStarted);
                Assert.IsNull(runtime.ClaimResult("alice", "chat", "phone", TimeSpan.FromMinutes(1)));
            }
        }

        [Test]
        public void CancellationIsNotReportedUntilBackendConfirmsInEditor() => Fixtures.HarnessTestCase.Run(CancellationIsNotReportedUntilBackendConfirms);

        public async Task CancellationIsNotReportedUntilBackendConfirms()
        {
            var harness = new Fixtures.ControlledHarness();
            using (var runtime = new PersistentTaskRuntime(new MemoryTaskStore(), new[] { harness }))
            {
                var task = runtime.Submit(Request());
                await harness.Started.Task;
                var cancel = runtime.CancelAsync(task.Id, "alice", CancellationToken.None);
                Assert.AreEqual(HarnessTaskState.Cancelling, runtime.Get(task.Id, "alice").State);
                Assert.IsFalse(cancel.IsCompleted);
                harness.Cancelled.TrySetResult(true);
                await cancel;
                harness.Finish.TrySetCanceled();
                await runtime.WaitForIdleAsync();
                Assert.AreEqual(HarnessTaskState.Cancelled, runtime.Get(task.Id, "alice").State);
            }
        }

        [Test]
        public void LocalCancellationWaitsForObservedOutcomeAndCannotOverwriteCompletionInEditor() => Fixtures.HarnessTestCase.Run(LocalCancellationWaitsForObservedOutcomeAndCannotOverwriteCompletion);

        public async Task LocalCancellationWaitsForObservedOutcomeAndCannotOverwriteCompletion()
        {
            foreach (var outcome in new[] { HarnessOutcome.Cancelled("HTTP aborted; provider outcome unknown."), HarnessOutcome.Completed("Already completed.") })
            {
                var harness = new Fixtures.LocalRequestHarness();
                var store = new MemoryTaskStore();
                string id;
                using (var runtime = new PersistentTaskRuntime(store, new[] { harness }))
                {
                    id = runtime.Submit(Request()).Id;
                    await harness.Started.Task;
                    var cancel = runtime.CancelAsync(id, "alice", CancellationToken.None);
                    await harness.CancellationRequested.Task;
                    Assert.AreEqual(HarnessTaskState.Cancelling, runtime.Get(id, "alice").State);
                    Assert.IsFalse(cancel.IsCompleted, "Requesting a local abort is not a terminal result.");
                    harness.Finish.TrySetResult(outcome);
                    await cancel;
                    Assert.AreEqual(outcome.State, runtime.Get(id, "alice").State);
                    Assert.AreEqual(outcome.Text, runtime.Get(id, "alice").Result);
                }
                using (var reopened = new PersistentTaskRuntime(store, new[] { harness }))
                {
                    Assert.AreEqual(outcome.State, reopened.Get(id, "alice").State);
                    Assert.AreEqual(outcome.Text, reopened.Get(id, "alice").Result);
                }
            }
        }

        [Test]
        public void AdmissionFailureNeverExecutesAndUnknownBackendIsRejectedInEditor() => Fixtures.HarnessTestCase.Run(AdmissionFailureNeverExecutesAndUnknownBackendIsRejected);

        public async Task AdmissionFailureNeverExecutesAndUnknownBackendIsRejected()
        {
            var harness = new Fixtures.ControlledHarness();
            var store = new MemoryTaskStore();
            using (var runtime = new PersistentTaskRuntime(store, new[] { harness }))
            {
                store.FailWrites = true;
                Assert.Throws<IOException>(() => runtime.Submit(Request()));
                Assert.AreEqual(0, harness.Runs);
                store.FailWrites = false;
                Assert.Throws<ArgumentException>(() => runtime.Submit(new TaskSubmission("alice", "chat", "x", "missing", "p", "Do work")));
            }
            await Task.CompletedTask;
        }

        [Test]
        public void DifferentLanesCanRunWhileSameWorkspaceRemainsSerialInEditor() => Fixtures.HarnessTestCase.Run(DifferentLanesCanRunWhileSameWorkspaceRemainsSerial);

        public async Task DifferentLanesCanRunWhileSameWorkspaceRemainsSerial()
        {
            var harness = new Fixtures.ControlledHarness();
            using (var runtime = new PersistentTaskRuntime(new MemoryTaskStore(), new[] { harness }, 2))
            {
                runtime.Submit(Request("one", lane: "p"));
                runtime.Submit(Request("two", lane: "p"));
                runtime.Submit(Request("three", lane: "q"));
                await harness.TwoStarted.Task;
                Assert.AreEqual(2, harness.Runs);
                harness.Finish.TrySetResult("done");
                await runtime.WaitForIdleAsync();
                Assert.AreEqual(3, harness.Runs);
            }
        }

        [Test]
        public void FileStoreHasExclusiveWriterAndSurvivesReopenInEditor() => Fixtures.HarnessTestCase.Run(FileStoreHasExclusiveWriterAndSurvivesReopen);

        public async Task FileStoreHasExclusiveWriterAndSurvivesReopen()
        {
            string artifactRoot = Environment.GetEnvironmentVariable("SIRIAI_HARNESS_TEST_ARTIFACTS") ??
                Path.Combine(TestContext.CurrentContext.WorkDirectory ?? Directory.GetCurrentDirectory(), "TestArtifacts~", "HarnessChecks");
            string folder = Path.Combine(artifactRoot, "task-store-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                var harness = new Fixtures.ControlledHarness();
                using (var store = new FileTaskStore(folder))
                using (var runtime = new PersistentTaskRuntime(store, new[] { harness }))
                {
                    Assert.Throws<IOException>(() => new FileTaskStore(folder));
                    runtime.Submit(Request());
                    harness.Finish.TrySetResult("saved to disk");
                    await runtime.WaitForIdleAsync();
                }
                using (var store = new FileTaskStore(folder))
                using (var runtime = new PersistentTaskRuntime(store, new[] { harness }))
                    Assert.AreEqual("saved to disk", runtime.List("alice")[0].Result);
            }
            finally { Directory.Delete(folder, true); }
        }

        [Test]
        public void WaitingForInputUsesExactRequestAndDoesNotFinishWorkInEditor() => Fixtures.HarnessTestCase.Run(WaitingForInputUsesExactRequestAndDoesNotFinishWork);

        public async Task WaitingForInputUsesExactRequestAndDoesNotFinishWork()
        {
            var harness = new Fixtures.ControlledHarness();
            using (var runtime = new PersistentTaskRuntime(new MemoryTaskStore(), new[] { harness }))
            {
                var task = runtime.Submit(Request());
                await harness.Started.Task;
                harness.Publish(new HarnessActivity(HarnessWaitingFor.Input, "question-1", "Which output folder?"));
                Assert.AreEqual(HarnessWaitingFor.Input, runtime.Get(task.Id, "alice").WaitingFor);
                bool rejected = false;
                try { await runtime.RespondAsync(task.Id, "alice", "old", "Reports", false, CancellationToken.None); }
                catch (InvalidOperationException) { rejected = true; }
                Assert.IsTrue(rejected, "A stale input request must be rejected.");
                await runtime.RespondAsync(task.Id, "alice", "question-1", "Reports", false, CancellationToken.None);
                Assert.AreEqual("Reports", harness.LastReply);
                Assert.AreEqual(HarnessWaitingFor.None, runtime.Get(task.Id, "alice").WaitingFor);
                harness.Finish.TrySetResult("report");
                await runtime.WaitForIdleAsync();
            }
        }
    }
}
