using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MNN.Samples.SiriAI.Harnesses;
using NUnit.Framework;

namespace MNN.Samples.SiriAI.Tests
{
    public sealed class SiriAIHarnessAttachmentTests
    {
        [Test] public void AttachmentIdentitySurvivesAdmissionAndRestartInEditor() => Fixtures.HarnessTestCase.Run(VerifyAsync);
        public async Task VerifyAsync()
        {
            string folder = Fixtures.HarnessTestCase.NewDirectory();
            var parts = new[] { new HarnessContentPart(HarnessContentKind.Url, "https://example.org/input.png", "image/png") };
            var harness = new Fixtures.ControlledHarness();
            try
            {
                using (var store = new FileTaskStore(folder))
                using (var runtime = new PersistentTaskRuntime(store, new[] { harness }))
                {
                    var request = new TaskSubmission("alice", "chat", "key", "controlled", "workspace", "Inspect input", parts);
                    parts[0] = new HarnessContentPart(HarnessContentKind.Text, "caller changed");
                    var task = runtime.Submit(request);
                    Assert.AreEqual(HarnessContentKind.Url, task.Attachments[0].Kind);
                    Assert.AreEqual(task.Id, runtime.Submit(request).Id);
                    Assert.Throws<ArgumentException>(() => runtime.Submit(new TaskSubmission("alice", "chat", "key", "controlled", "workspace", "Inspect input", parts)));
                    task.Attachments[0] = parts[0];
                    Assert.AreEqual(HarnessContentKind.Url, runtime.Get(task.Id, "alice").Attachments[0].Kind);
                    harness.Finish.TrySetResult("inspected");await runtime.WaitForIdleAsync();
                }
                using (var store = new FileTaskStore(folder))
                using (var runtime = new PersistentTaskRuntime(store, new[] { harness }))
                    Assert.AreEqual("https://example.org/input.png", runtime.List("alice")[0].Attachments[0].Payload);
                var dto = new SubmitTaskRequest { ConversationId = "chat", SubmissionKey = "admission", HarnessId = "controlled", WorkspaceKey = "workspace", Objective = "Inspect input", Attachments = parts };
                using (var journal = new SubmissionJournal(Path.Combine(folder, "journal")))
                {
                    try { await journal.SubmitAsync(dto, (r, c) => Task.FromException<HarnessTask>(new IOException("lost response")), CancellationToken.None); }
                    catch (PendingSubmissionException) { }
                    parts[0] = new HarnessContentPart(HarnessContentKind.Text, "changed after admission");
                }
                using (var journal = new SubmissionJournal(Path.Combine(folder, "journal")))
                {
                    string payload = null;
                    try { await journal.ReplayAsync((r, c) => { payload = r.Attachments[0].Payload; throw new IOException("still disconnected"); }, CancellationToken.None); }
                    catch (PendingSubmissionException) { }
                    Assert.AreEqual("caller changed", payload);
                }
                string legacyFolder = Path.Combine(folder, "legacy-journal");
                Directory.CreateDirectory(legacyFolder);
                var legacy = new SubmitTaskRequest { ConversationId = "chat", SubmissionKey = "legacy", HarnessId = "controlled", WorkspaceKey = "workspace", Objective = "Resume original admission" };
                File.WriteAllBytes(Path.Combine(legacyFolder, "submissions.json"), TaskHostJson.Write(new[] { legacy }));
                using (var journal = new SubmissionJournal(legacyFolder))
                {
                    bool sent = false;
                    try { await journal.SubmitAsync(legacy, (r, c) => { sent = true;throw new IOException("offline"); }, CancellationToken.None); }
                    catch (PendingSubmissionException) { }
                    Assert.IsTrue(sent, "A pre-attachment journal must reuse its pending admission identity.");
                }
                Assert.Throws<ArgumentException>(() => new TaskSubmission("a", "c", "k", "h", "w", "o", new[] { new HarnessContentPart(HarnessContentKind.Text, new string('x', 262144)), new HarnessContentPart(HarnessContentKind.Text, "overflow") }));
            }
            finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
        }
    }
}
