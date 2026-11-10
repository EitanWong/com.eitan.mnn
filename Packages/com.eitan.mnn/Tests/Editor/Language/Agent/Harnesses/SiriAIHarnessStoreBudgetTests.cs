using System;
using System.IO;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using MNN.Samples.SiriAI.Harnesses;
using NUnit.Framework;

namespace MNN.Samples.SiriAI.Tests
{
    public sealed class SiriAIHarnessStoreBudgetTests
    {
        [Test]
        public void OversizedWritePreservesCommittedResultAndRestartInEditor() => Fixtures.HarnessTestCase.Run(OversizedWritePreservesCommittedResultAndRestart);

        public async Task OversizedWritePreservesCommittedResultAndRestart()
        {
            string directory = Fixtures.HarnessTestCase.NewDirectory();
            try
            {
                var first = await CompletedTasks(1, "original durable result");
                using (var store = new FileTaskStore(directory)) store.Save(first);
                string file = Path.Combine(directory, "tasks.json");
                byte[] original = File.ReadAllBytes(file);
                // These valid snapshots fit the record limit but their UTF-8 document exceeds 64 MiB.
                var oversized = await CompletedTasks(400, new string('界', 65536));
                using (var store = new FileTaskStore(directory))
                    Assert.Throws<SerializationException>(() => store.Save(oversized));
                CollectionAssert.AreEqual(original, File.ReadAllBytes(file), "Rejected writes must retain the prior commit.");
                Assert.AreEqual(0, Directory.GetFiles(directory, "*.tmp").Length);
                using (var store = new FileTaskStore(directory))
                using (var runtime = new PersistentTaskRuntime(store, new IHarnessAdapter[0]))
                {
                    var restored = runtime.Get(first[0].Id, "alice");
                    Assert.AreEqual("original durable result", restored.Result);
                    Assert.IsTrue(restored.ResultPending);
                }
                using (var store = new FileTaskStore(directory)) store.Save(new HarnessTask[0]);
                using (var store = new FileTaskStore(directory)) Assert.AreEqual(0, store.Load().Length);
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }

        private static async Task<HarnessTask[]> CompletedTasks(int count, string result)
        {
            var adapter = new Fixtures.ControlledHarness();
            var memory = new MemoryTaskStore();
            adapter.Finish.TrySetResult(result);
            using (var runtime = new PersistentTaskRuntime(memory, new[] { adapter }))
            {
                for (int i = 0; i < count; ++i)
                    runtime.Submit(new TaskSubmission("alice", "chat", "budget-" + i, adapter.Id, "workspace", "Retain result " + i));
                await runtime.WaitForIdleAsync();
            }
            return memory.Load();
        }
    }
}
