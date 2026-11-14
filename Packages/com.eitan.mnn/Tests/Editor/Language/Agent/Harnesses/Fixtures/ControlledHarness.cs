using System;
using System.Threading;
using System.Threading.Tasks;
using MNN.Samples.SiriAI.Harnesses;

namespace MNN.Samples.SiriAI.Tests.Fixtures
{
    internal sealed class ControlledHarness : IHarnessAdapter
    {
        public string Id => "controlled";
        public HarnessCapabilities Capabilities => HarnessCapabilities.Cancel | HarnessCapabilities.Input | HarnessCapabilities.Permission | HarnessCapabilities.Update | HarnessCapabilities.Attachments;
        internal readonly TaskCompletionSource<bool> Started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource<bool> TwoStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource<string> Finish = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource<bool> Cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int Runs;
        internal string LastReply;
        internal Action<HarnessActivity> Publish;
        internal HarnessArtifact[] Artifacts;

        public async Task<HarnessOutcome> RunAsync(HarnessTask task, Action<HarnessActivity> activity, CancellationToken stopping)
        {
            Publish = activity;
            int count = Interlocked.Increment(ref Runs);
            Started.TrySetResult(true);
            if (count >= 2) TwoStarted.TrySetResult(true);
            return HarnessOutcome.Completed(await Finish.Task, Artifacts);
        }
        public Task<bool> CancelAsync(HarnessTask task, CancellationToken cancellation) => Cancelled.Task;
        public Task RespondAsync(HarnessTask task, string requestId, string reply, bool allow, CancellationToken cancellation)
        { LastReply = reply; return Task.CompletedTask; }
        public Task UpdateAsync(HarnessTask task, string instruction, CancellationToken cancellation) => Task.CompletedTask;
    }

    internal sealed class LocalRequestHarness : ILocalCancellationHarnessAdapter
    {
        public string Id => "controlled";
        public HarnessCapabilities Capabilities => HarnessCapabilities.Cancel;
        public string CancellationScope => "http-request";
        internal readonly TaskCompletionSource<bool> Started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource<bool> CancellationRequested = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource<HarnessOutcome> Finish = new TaskCompletionSource<HarnessOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<HarnessOutcome> RunCancellableAsync(HarnessTask task, Action<HarnessActivity> activity, CancellationToken requested, CancellationToken stopping)
        {
            using (requested.Register(() => CancellationRequested.TrySetResult(true)))
            {
                Started.TrySetResult(true);
                return await Finish.Task;
            }
        }
        public Task<HarnessOutcome> RunAsync(HarnessTask task, Action<HarnessActivity> activity, CancellationToken stopping) => throw new InvalidOperationException("Runtime must supply a task-owned cancellation token.");
        public Task<bool> CancelAsync(HarnessTask task, CancellationToken cancellation) => throw new InvalidOperationException("A local abort is not backend-confirmed cancellation.");
        public Task RespondAsync(HarnessTask task, string requestId, string reply, bool allow, CancellationToken cancellation) => throw new NotSupportedException();
        public Task UpdateAsync(HarnessTask task, string instruction, CancellationToken cancellation) => throw new NotSupportedException();
    }
}
