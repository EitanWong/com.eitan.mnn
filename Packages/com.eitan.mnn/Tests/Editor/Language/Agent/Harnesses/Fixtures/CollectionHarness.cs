using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MNN.Samples.SiriAI.Harnesses;

namespace MNN.Samples.SiriAI.Tests.Fixtures
{
    internal sealed class CollectionHarness : IHarnessAdapter
    {
        public string Id { get; }
        public HarnessCapabilities Capabilities => HarnessCapabilities.None;
        internal readonly TaskCompletionSource<HarnessOutcome> Finish = new TaskCompletionSource<HarnessOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int Runs;
        internal CollectionHarness(string id) { Id = id; }
        public Task<HarnessOutcome> RunAsync(HarnessTask task, Action<HarnessActivity> activity, CancellationToken stopping)
        { Interlocked.Increment(ref Runs); return Finish.Task; }
        public Task<bool> CancelAsync(HarnessTask task, CancellationToken cancellation) => Task.FromResult(false);
        public Task RespondAsync(HarnessTask task, string id, string reply, bool allow, CancellationToken cancellation) => throw new NotSupportedException();
        public Task UpdateAsync(HarnessTask task, string text, CancellationToken cancellation) => throw new NotSupportedException();
    }
    internal sealed class CollectionStore : IHarnessTaskStore
    {
        private readonly MemoryTaskStore _inner = new MemoryTaskStore();
        internal bool FailCollection;
        internal int AtomicCollections;
        public HarnessTask[] Load() => _inner.Load();
        public void Save(HarnessTask[] tasks)
        {
            var parent = tasks.SingleOrDefault(t => t.CollectedResults?.Length > 0);
            if (parent != null)
            {
                if (FailCollection) throw new IOException("Injected durable collection write failure.");
                foreach (var source in parent.CollectedResults)
                {
                    var child = tasks.Single(t => t.Id == source.TaskId);
                    if (child.ResultPending || child.ResultCollectedBy != parent.Id) throw new IOException("Parent/child collection was split across saves.");
                }
                AtomicCollections++;
            }
            _inner.Save(tasks);
        }
    }
}
