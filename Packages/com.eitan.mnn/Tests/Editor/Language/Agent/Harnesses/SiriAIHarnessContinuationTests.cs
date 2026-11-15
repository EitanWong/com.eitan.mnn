using System;
using System.Threading;
using System.Threading.Tasks;
using MNN.Samples.SiriAI.Harnesses;
using NUnit.Framework;

namespace MNN.Samples.SiriAI.Tests
{
    public sealed class SiriAIHarnessContinuationTests
    {
        [Test] public void ContinuationRetainsOriginalResultAndRequiresOriginalContext() => Fixtures.HarnessTestCase.Run(VerifyAsync);
        public async Task VerifyAsync()
        {
            var adapter = new ContinuingHarness();
            using (var runtime = new PersistentTaskRuntime(new MemoryTaskStore(), new[] { adapter }))
            {
                var first = runtime.Submit(new TaskSubmission("owner", "chat", "first", "continue", "project", "initial work"));
                await runtime.WaitForIdleAsync();
                var original = runtime.Get(first.Id, "owner");
                var request = new TaskSubmission("owner", "chat", "next", "continue", "project", "follow up", continuedFromTaskId: first.Id);
                var next = runtime.Submit(request);
                await runtime.WaitForIdleAsync();
                var finished = runtime.Get(next.Id, "owner");
                Assert.AreEqual(first.Id, finished.ContinuedFromTaskId);
                Assert.AreEqual("native-session-1", adapter.ObservedContinuation);
                Assert.AreEqual("follow up", finished.Result);
                Assert.AreEqual(next.Id, runtime.Submit(request).Id);
                Assert.AreEqual(original.Revision, runtime.Get(first.Id, "owner").Revision);
                Assert.AreEqual(original.Result, runtime.Get(first.Id, "owner").Result);
                Assert.True(runtime.Get(first.Id, "owner").ResultPending);
                Assert.Throws<UnauthorizedAccessException>(() => runtime.Submit(new TaskSubmission("other", "chat", "foreign", "continue", "project", "work", continuedFromTaskId: first.Id)));
                Assert.Throws<ArgumentException>(() => runtime.Submit(new TaskSubmission("owner", "elsewhere", "wrong-chat", "continue", "project", "work", continuedFromTaskId: first.Id)));
                Assert.Throws<ArgumentException>(() => runtime.Submit(new TaskSubmission("owner", "chat", "wrong-project", "continue", "different", "work", continuedFromTaskId: first.Id)));
            }
            var unsupported = new Fixtures.ControlledHarness(); unsupported.Finish.SetResult("finished");
            using (var runtime = new PersistentTaskRuntime(new MemoryTaskStore(), new[] { unsupported }))
            {
                var first = runtime.Submit(new TaskSubmission("owner", "chat", "first", "controlled", "project", "work"));
                await runtime.WaitForIdleAsync();
                Assert.Throws<NotSupportedException>(() => runtime.Submit(new TaskSubmission("owner", "chat", "next", "controlled", "project", "more", continuedFromTaskId: first.Id)));
            }
        }
        private sealed class ContinuingHarness : IRecoverableHarnessAdapter, IHarnessContinuationAdapter
        {
            public string Id => "continue";
            public HarnessCapabilities Capabilities => HarnessCapabilities.Continue;
            public string ObservedContinuation;
            public string CreateContinuationCheckpoint(HarnessTask original) => original.BackendState;
            public bool CanRecover(string checkpoint) => false;
            public Task<HarnessOutcome> RunDurableAsync(HarnessTask task, Action<HarnessActivity> activity, Action<string> checkpoint, CancellationToken stopping)
            {
                ObservedContinuation = task.ContinuationState;
                checkpoint("native-session-1");
                return Task.FromResult(HarnessOutcome.Completed(task.Objective));
            }
            public Task<HarnessOutcome> RunAsync(HarnessTask task, Action<HarnessActivity> activity, CancellationToken stopping) => throw new NotSupportedException();
            public Task<bool> CancelAsync(HarnessTask task, CancellationToken token) => throw new NotSupportedException();
            public Task RespondAsync(HarnessTask task, string id, string reply, bool allow, CancellationToken token) => throw new NotSupportedException();
            public Task UpdateAsync(HarnessTask task, string text, CancellationToken token) => throw new NotSupportedException();
        }
    }
}
