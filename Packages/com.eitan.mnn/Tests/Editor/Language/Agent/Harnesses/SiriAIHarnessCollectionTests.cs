using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MNN.Samples.SiriAI.Harnesses;
using NUnit.Framework;

namespace MNN.Samples.SiriAI.Tests
{
    public sealed class SiriAIHarnessCollectionTests
    {
        [Test]
        public void CollectionCommitsExactArtifactsAndChildOutboxTogether() => Fixtures.HarnessTestCase.Run(VerifyAsync);

        public async Task VerifyAsync()
        {
            await CheckAsync("valid");
            await CheckAsync("write-failure");
            await CheckAsync("foreign-owner");
            await CheckAsync("foreign-run");
            await CheckAsync("stale-hash");
            await CheckAsync("missing-artifact");
        }
        private static async Task CheckAsync(string mode)
        {
            var store = new Fixtures.CollectionStore();
            var parentAdapter = new Fixtures.CollectionHarness("parent");
            var childAdapter = new Fixtures.CollectionHarness("child");
            string parentId, childId;
            using (var runtime = new PersistentTaskRuntime(store, new IHarnessAdapter[] { parentAdapter, childAdapter }, 2))
            {
                parentId = runtime.Submit(new TaskSubmission("alice", "phone", "parent", "parent", "control", "Collect output")).Id;
                childId = runtime.Submit(new TaskSubmission(mode == "foreign-owner" ? "bob" : "alice", "native", "child", "child", "project", "Produce output",
                    resultConversationId: HarnessCollectedResult.ConversationFor(mode == "foreign-run" ? "another-parent" : parentId))).Id;
                var artifact = new HarnessArtifact("report", "Report", null, new[] { new HarnessContentPart(HarnessContentKind.Text, "exact output") });
                childAdapter.Finish.SetResult(HarnessOutcome.Completed("Child result", new[] { artifact }));
                await UntilAsync(() => store.Load().Single(t => t.Id == childId).IsTerminal);
                var child = store.Load().Single(t => t.Id == childId);
                var source = HarnessCollectedResult.Capture(child);
                if (mode == "stale-hash") source = TaskHostJson.Read<HarnessCollectedResult>(Encoding.UTF8.GetBytes(
                    "{\"TaskId\":\"" + childId + "\",\"ContentHash\":\"" + new string('0', 64) + "\"}"));
                var artifacts = mode == "missing-artifact" ? new HarnessArtifact[0] : HarnessCollectedResult.MergeArtifacts(null, new[] { child });
                store.FailCollection = mode == "write-failure";
                parentAdapter.Finish.SetResult(HarnessOutcome.Completed("Parent summary", artifacts).WithCollectedResults(new[] { source }));
                await UntilAsync(() => runtime.Fault != null || store.Load().Single(t => t.Id == parentId).IsTerminal);
                if (mode == "valid")
                {
                    Assert.IsNull(runtime.Fault);
                    var parent = runtime.Get(parentId, "alice");
                    Assert.IsTrue(parent.ResultPending);
                    Assert.AreEqual(1, store.AtomicCollections);
                    Assert.AreEqual(childId, parent.Artifacts[0].SourceTaskId);
                    Assert.AreEqual("report", parent.Artifacts[0].SourceArtifactId);
                    parent.CollectedResults[0] = null;
                    Assert.IsNotNull(runtime.Get(parentId, "alice").CollectedResults[0]);
                    var read = TaskHostJson.Read<HarnessTask>(TaskHostJson.Write(runtime.Get(parentId, "alice")));
                    Assert.AreEqual(source.ContentHash, read.CollectedResults[0].ContentHash);
                }
                else
                {
                    Assert.IsNotNull(runtime.Fault, mode);
                    Assert.IsFalse(store.Load().Single(t => t.Id == parentId).IsTerminal, mode);
                    Assert.IsTrue(store.Load().Single(t => t.Id == childId).ResultPending, mode);
                    Assert.IsNull(store.Load().Single(t => t.Id == childId).ResultCollectedBy, mode);
                }
            }
            store.FailCollection = false;
            var reopenedParent = new Fixtures.CollectionHarness("parent");
            using (var reopened = new PersistentTaskRuntime(store, new IHarnessAdapter[] { reopenedParent, new Fixtures.CollectionHarness("child") }, 2))
            {
                Assert.AreEqual(0, reopenedParent.Runs);
                Assert.AreEqual(mode == "valid" ? HarnessTaskState.Completed : HarnessTaskState.Interrupted, reopened.Get(parentId, "alice").State);
                var child = store.Load().Single(t => t.Id == childId);
                Assert.AreEqual(mode != "valid", child.ResultPending);
                Assert.IsFalse(child.PlaybackStarted);
            }
        }
        private static async Task UntilAsync(Func<bool> ready)
        {
            for (int attempt = 0; attempt < 200; attempt++)
            { if (ready()) return; await Task.Delay(10); }
            Assert.Fail("Timed out waiting for collection persistence.");
        }
    }
}
