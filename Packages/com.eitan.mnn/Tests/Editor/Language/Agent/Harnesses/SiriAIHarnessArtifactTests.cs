using System;
using System.Threading.Tasks;
using MNN.Samples.SiriAI.Harnesses;
using NUnit.Framework;

namespace MNN.Samples.SiriAI.Tests
{
    public sealed class SiriAIHarnessArtifactTests
    {
        [Test]
        public void ArtifactsRemainTypedAndDurableWithoutSharingSnapshotArrays() => Fixtures.HarnessTestCase.Run(VerifyAsync);

        public async Task VerifyAsync()
        {
            string directory = Fixtures.HarnessTestCase.NewDirectory();
            string id;
            var artifact = new HarnessArtifact("report", "Report", "A generated report", new[]
            {
                new HarnessContentPart(HarnessContentKind.Text, "report text", "text/plain", "report.txt"),
                new HarnessContentPart(HarnessContentKind.Url, "https://files.example.test/report.pdf", "application/pdf", "report.pdf"),
                new HarnessContentPart(HarnessContentKind.Json, "{\"count\":2}", "application/json"),
                new HarnessContentPart(HarnessContentKind.Base64, "AQID", "application/octet-stream", "sample.bin")
            });
            var adapter = new Fixtures.ControlledHarness { Artifacts = new[] { artifact } };
            using (var store = new FileTaskStore(directory))
            using (var runtime = new PersistentTaskRuntime(store, new[] { adapter }))
            {
                id = runtime.Submit(new TaskSubmission("alice", "chat", "artifacts", "controlled", "project", "Create report")).Id;
                adapter.Finish.TrySetResult("Report ready");
                await runtime.WaitForIdleAsync();
                var task = runtime.Get(id, "alice");
                Assert.AreEqual(4, task.Artifacts[0].Parts.Length);
                task.Artifacts[0] = null;
                Assert.AreEqual("report", runtime.Get(id, "alice").Artifacts[0].Id);
            }
            using (var store = new FileTaskStore(directory))
            using (var runtime = new PersistentTaskRuntime(store, new[] { new Fixtures.ControlledHarness() }))
            {
                var task = runtime.Get(id, "alice");
                Assert.AreEqual(HarnessTaskState.Completed, task.State);
                Assert.AreEqual("Report ready", task.Result);
                Assert.AreEqual("AQID", task.Artifacts[0].Parts[3].Payload);
                Assert.AreEqual(HarnessContentKind.Json, task.Artifacts[0].Parts[2].Kind);
                var decoded = TaskHostJson.Read<HarnessTask>(TaskHostJson.Write(task));
                Assert.AreEqual("application/pdf", decoded.Artifacts[0].Parts[1].MediaType);
            }
            Assert.Throws<ArgumentException>(() => new HarnessContentPart(HarnessContentKind.Url, "javascript:alert(1)"));
            Assert.Throws<ArgumentException>(() => new HarnessContentPart(HarnessContentKind.Base64, "not base64"));
        }
    }
}
