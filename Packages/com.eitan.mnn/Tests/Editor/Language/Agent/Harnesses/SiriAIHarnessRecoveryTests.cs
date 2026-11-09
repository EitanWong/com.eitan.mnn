using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MNN.Samples.SiriAI.Harnesses;
using NUnit.Framework;

namespace MNN.Samples.SiriAI.Tests
{
    public sealed class SiriAIHarnessRecoveryTests
    {
        [Test] public void ConfirmedRemoteHandleSurvivesHostRestartInEditor() => Fixtures.HarnessTestCase.Run(ConfirmedRemoteHandleSurvivesHostRestart);
        [Test] public void ChangedBackendCannotReplayPreviousWorkInEditor() => Fixtures.HarnessTestCase.Run(ChangedBackendCannotReplayPreviousWork);
        [Test] public void QueuedWorkWithRemovedHarnessDoesNotPreventRestartInEditor() => Fixtures.HarnessTestCase.Run(QueuedWorkWithRemovedHarnessDoesNotPreventRestart);
        public Task ConfirmedRemoteHandleSurvivesHostRestart() => Restart(false);
        public Task ChangedBackendCannotReplayPreviousWork() => Restart(true);
        public async Task QueuedWorkWithRemovedHarnessDoesNotPreventRestart()
        {
            var saved = new MemoryTaskStore();
            var adapter = new Fixtures.ControlledHarness();
            string id;
            using (var runtime = new PersistentTaskRuntime(new MemoryTaskStore(), new[] { adapter }))
            {
                runtime.Submit(new TaskSubmission("alice", "chat", "blocker", adapter.Id, "workspace", "First work"));
                await adapter.Started.Task;
                var queued = runtime.Submit(new TaskSubmission("alice", "chat", "queued", adapter.Id, "workspace", "Pending work"));
                Assert.AreEqual(HarnessTaskState.Queued, queued.State);
                id = queued.Id;
                saved.Save(new[] { queued });
                adapter.Finish.TrySetResult("Finished original host");
                await runtime.WaitForIdleAsync();
            }
            using (var restarted = new PersistentTaskRuntime(saved, new IHarnessAdapter[0]))
            {
                var task = restarted.Get(id, "alice");
                Assert.AreEqual(HarnessTaskState.Interrupted, task.State);
                Assert.IsTrue(task.ResultPending);
                StringAssert.Contains("no longer configured", task.Result);
            }
        }
        private static async Task Restart(bool changedBackend)
        {
            string folder = Fixtures.HarnessTestCase.NewDirectory();
            string id;
            var first = new RecoverableFixture(false);
            try
            {
                using (var store = new FileTaskStore(folder))
                using (var runtime = new PersistentTaskRuntime(store, new[] { first }))
                {
                    id = runtime.Submit(new TaskSubmission("alice", "chat", "key", "recoverable", "workspace", "Build a report")).Id;
                    await first.Started.Task;
                    Assert.AreEqual(HarnessTaskState.Running, runtime.Get(id, "alice").State);
                }
                await first.Stopped.Task;
                var next = new RecoverableFixture(true, changedBackend);
                using (var store = new FileTaskStore(folder))
                using (var runtime = new PersistentTaskRuntime(store, new[] { next }))
                {
                    await runtime.WaitForIdleAsync();
                    var task = runtime.Get(id, "alice");
                    if (changedBackend)
                    {
                        Assert.AreEqual(HarnessTaskState.Interrupted, task.State);
                        Assert.AreEqual(0, next.Runs, "A mismatched backend must not receive the old objective.");
                        Assert.IsTrue(task.ResultPending);
                    }
                    else
                    {
                        Assert.AreEqual(HarnessTaskState.Completed, task.State);
                        Assert.AreEqual("remote result after restart", task.Result);
                        Assert.AreEqual(id, next.LocalId);
                        Assert.AreEqual("confirmed-handle", next.RestoredHandle);
                        Assert.AreEqual(1, next.Runs);
                    }
                    Assert.Throws<UnauthorizedAccessException>(() => runtime.Get(id, "bob"));
                }
            }
            finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
        }
        private sealed class RecoverableFixture : IRecoverableHarnessAdapter
        {
            private readonly bool _finish;
            private readonly bool _changed;
            internal readonly TaskCompletionSource<bool> Started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            internal readonly TaskCompletionSource<bool> Stopped = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            internal int Runs;
            internal string LocalId, RestoredHandle;
            internal RecoverableFixture(bool finish, bool changed = false) { _finish = finish; _changed = changed; }
            public string Id => "recoverable";
            public HarnessCapabilities Capabilities => HarnessCapabilities.None;
            public bool CanRecover(string checkpoint) => !_changed && checkpoint == "confirmed-handle";
            public async Task<HarnessOutcome> RunDurableAsync(HarnessTask task, Action<HarnessActivity> activity, Action<string> checkpoint, CancellationToken stopping)
            {
                Runs++; LocalId = task.Id; RestoredHandle = task.BackendState;
                try
                {
                    if (_finish) return HarnessOutcome.Completed("remote result after restart");
                    checkpoint("confirmed-handle"); Started.TrySetResult(true);
                    await Task.Delay(Timeout.Infinite, stopping);
                    throw new InvalidOperationException("Unexpected completion.");
                }
                finally { Stopped.TrySetResult(true); }
            }
            public Task<HarnessOutcome> RunAsync(HarnessTask task, Action<HarnessActivity> activity, CancellationToken stopping) => throw new InvalidOperationException("Durable entry required.");
            public Task<bool> CancelAsync(HarnessTask task, CancellationToken cancellation) => throw new NotSupportedException();
            public Task RespondAsync(HarnessTask task, string requestId, string reply, bool allow, CancellationToken cancellation) => throw new NotSupportedException();
            public Task UpdateAsync(HarnessTask task, string instruction, CancellationToken cancellation) => throw new NotSupportedException();
        }
    }
}
