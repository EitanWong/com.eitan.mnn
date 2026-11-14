using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MNN.Samples.SiriAI.Harnesses;
using NUnit.Framework;

namespace MNN.Samples.SiriAI.Tests
{
    public sealed class SiriAIHarnessPauseTests
    {
        [Test] public void QueuedPauseIsDurableAndNeverReplaysWork() => Fixtures.HarnessTestCase.Run(VerifyAsync);
        public async Task VerifyAsync()
        {
            var store = new MemoryTaskStore();
            var adapter = new Fixtures.ControlledHarness();
            string heldId;
            using (var runtime = new PersistentTaskRuntime(store, new[] { adapter }))
            {
                runtime.Submit(Request("first"));
                await adapter.Started.Task;
                var held = runtime.Submit(Request("held")); heldId = held.Id;
                Control(runtime, "Pause", heldId, "alice");
                Assert.AreEqual("Paused", runtime.Get(heldId, "alice").State.ToString());
                Control(runtime, "Pause", heldId, "alice");
                Assert.Throws<UnauthorizedAccessException>(() => Control(runtime, "Resume", heldId, "bob"));
                adapter.Finish.TrySetResult("done");
                await runtime.WaitForIdleAsync();
                Assert.AreEqual(1, adapter.Runs, "Paused queued work must not be scheduled.");
            }
            var restored = new Fixtures.ControlledHarness();
            using (var runtime = new PersistentTaskRuntime(store, new[] { restored }))
            {
                Assert.AreEqual("Paused", runtime.Get(heldId, "alice").State.ToString());
                Assert.AreEqual(0, restored.Runs, "Restart must preserve the hold.");
                Control(runtime, "Resume", heldId, "alice");
                await restored.Started.Task;
                Control(runtime, "Resume", heldId, "alice");
                Assert.Throws<NotSupportedException>(() => Control(runtime, "Pause", heldId, "alice"));
                Assert.AreEqual(HarnessTaskState.Running, runtime.Get(heldId, "alice").State);
                restored.Finish.TrySetResult("resumed once"); await runtime.WaitForIdleAsync();
                Assert.AreEqual(1, restored.Runs);
                Assert.True(runtime.Get(heldId, "alice").ResultPending);

            }
            var blocking = new Fixtures.ControlledHarness();
            using (var runtime = new PersistentTaskRuntime(new MemoryTaskStore(), new[] { blocking }))
            {
                runtime.Submit(Request("blocking")); await blocking.Started.Task;
                var held = runtime.Submit(Request("cancel-held"));
                Control(runtime, "Pause", held.Id, "alice");
                await runtime.CancelAsync(held.Id, "alice", CancellationToken.None);
                Assert.AreEqual(HarnessTaskState.Cancelled, runtime.Get(held.Id, "alice").State);
                Assert.Throws<InvalidOperationException>(() => Control(runtime, "Resume", held.Id, "alice"));
                blocking.Finish.TrySetResult("done"); await runtime.WaitForIdleAsync();
                Assert.AreEqual(1, blocking.Runs);
            }
        }
        [Test] public void CancellationAdmissionDoesNotWaitForBackendInEditor() => Fixtures.HarnessTestCase.Run(CancellationAdmissionAsync);
        public async Task CancellationAdmissionAsync()
        {
            var adapter = new Fixtures.ControlledHarness();
            using (var runtime = new PersistentTaskRuntime(new MemoryTaskStore(), new[] { adapter }))
            {
                var task = runtime.Submit(Request("cancel")); await adapter.Started.Task;
                Assert.AreEqual(HarnessTaskState.Cancelling, runtime.RequestCancel(task.Id, "alice").State);
                Assert.False(adapter.Cancelled.Task.IsCompleted, "Frontend admission must not await backend confirmation.");
                Assert.False(runtime.Get(task.Id, "alice").IsTerminal);
                adapter.Cancelled.TrySetResult(true);
                await runtime.CancelAsync(task.Id, "alice", CancellationToken.None);
                Assert.AreEqual(HarnessTaskState.Cancelled, runtime.Get(task.Id, "alice").State);
                adapter.Finish.TrySetResult("late result"); await runtime.WaitForIdleAsync();
                Assert.AreEqual(HarnessTaskState.Cancelled, runtime.Get(task.Id, "alice").State);
            }
        }
        private static TaskSubmission Request(string key) => new TaskSubmission("alice", "chat", key, "controlled", "project", key);
        private static void Control(PersistentTaskRuntime runtime, string name, string id, string owner)
        {
            var method = typeof(PersistentTaskRuntime).GetMethod(name, new[] { typeof(string), typeof(string) });
            Assert.NotNull(method, name + " must be implemented by the authoritative task runtime.");
            try { method.Invoke(runtime, new object[] { id, owner }); }
            catch (TargetInvocationException error) { throw error.InnerException; }
        }
    }
}
