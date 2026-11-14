using System;
using System.Collections.Generic;
using System.Linq;

namespace MNN.Samples.SiriAI.Harnesses
{
    public sealed partial class PersistentTaskRuntime
    {
        private HarnessTask[] CollectionSources(HarnessTask parent, HarnessCollectedResult[] sources, bool persisted)
        {
            if (!parent.IsTerminal || parent.State == HarnessTaskState.Cancelled) throw new InvalidOperationException("Collection requires a deliverable terminal parent result.");
            return sources.Select(source =>
            {
                var child = Owned(source.TaskId, parent.OwnerId);
                if (!child.IsTerminal || child.Id == parent.Id || child.ResultConversationId != HarnessCollectedResult.ConversationFor(parent.Id) ||
                    HarnessCollectedResult.Capture(child).ContentHash != source.ContentHash ||
                    (persisted ? child.ResultCollectedBy != parent.Id || child.ResultPending : child.ResultCollectedBy != null && child.ResultCollectedBy != parent.Id))
                    throw new InvalidOperationException("Collected result does not match the exact original child content.");
                return child;
            }).ToArray();
        }
        private static void RequireCollectedArtifacts(HarnessTask parent, HarnessTask[] children)
        {
            var expected = HarnessCollectedResult.MergeArtifacts(null, children).OrderBy(a => a.Id).ToArray();
            var actual = (parent.Artifacts ?? new HarnessArtifact[0]).Where(a => a.SourceTaskId != null).OrderBy(a => a.Id).ToArray();
            if (!TaskHostJson.Write(expected).SequenceEqual(TaskHostJson.Write(actual)))
                throw new InvalidOperationException("Parent result does not contain every exact collected child artifact.");
        }
        private void CommitCollected(HarnessTask parent, HarnessCollectedResult[] sources)
        {
            if (sources.Length == 0) { Commit(parent); return; }
            var children = CollectionSources(parent, sources, false);
            RequireCollectedArtifacts(parent, children);
            parent.CollectedResults = sources.ToArray();
            var next = new Dictionary<string, HarnessTask>(_tasks, StringComparer.Ordinal) { [parent.Id] = parent };
            foreach (var child in children)
            {
                var collected = Changed(child);
                collected.ResultPending = false; collected.ResultCollectedBy = parent.Id;
                collected.LeaseId = null; collected.LeaseUntilUtcTicks = 0;
                next[child.Id] = collected;
            }
            // Parent artifacts and child outbox transitions share the same durable file replacement.
            _store.Save(next.Values.Select(task => task.Copy()).ToArray());
            _tasks = next;
        }
        private void ValidateCollections()
        {
            foreach (var task in _tasks.Values.Where(t => t.CollectedResults?.Length > 0))
                RequireCollectedArtifacts(task, CollectionSources(task, HarnessCollectedResult.Checked(task.CollectedResults), true));
            foreach (var child in _tasks.Values.Where(t => t.ResultCollectedBy != null))
                if (!_tasks.TryGetValue(child.ResultCollectedBy, out var parent) || parent.OwnerId != child.OwnerId ||
                    parent.CollectedResults?.Any(source => source.TaskId == child.Id) != true)
                    throw new InvalidOperationException("Collected child has no matching durable parent manifest.");
        }
    }
}
