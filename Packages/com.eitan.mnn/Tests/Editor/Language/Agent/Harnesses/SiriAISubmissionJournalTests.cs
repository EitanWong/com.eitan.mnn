using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MNN.Samples.SiriAI.Harnesses;
using NUnit.Framework;

namespace MNN.Samples.SiriAI.Tests
{
    public sealed class SiriAISubmissionJournalTests
    {
        [Test]
        public void LocalWriteFailurePreventsTransmissionAndExplicitRejectionDoesNotReplayInEditor() => Fixtures.HarnessTestCase.Run(LocalWriteFailurePreventsTransmissionAndExplicitRejectionDoesNotReplay);

        public async Task LocalWriteFailurePreventsTransmissionAndExplicitRejectionDoesNotReplay()
        {
            string directory = Fixtures.HarnessTestCase.NewDirectory();
            var request = new SubmitTaskRequest { SubmissionKey = "call", ConversationId = "chat", HarnessId = "controlled",
                WorkspaceKey = "project", Objective = "First objective" };
            int sent = 0;
            using (var journal = new SubmissionJournal(directory))
            {
                string obstruction = Path.Combine(directory, "submissions.json");
                Directory.CreateDirectory(obstruction);
                bool failed = false;
                try { await journal.SubmitAsync(request, (r, ct) => { ++sent; throw new Exception("Must not transmit"); }, CancellationToken.None); }
                catch (IOException) { failed = true; }
                Assert.True(failed);
                Assert.AreEqual(0, sent);
                Assert.AreEqual(0, journal.PendingCount);
                Directory.Delete(obstruction);
                try { await journal.SubmitAsync(request, (r, ct) => { ++sent; throw new TaskHostRequestException(400, "Rejected objective"); }, CancellationToken.None); }
                catch (TaskHostRequestException) { }
                Assert.AreEqual(1, sent);
                Assert.AreEqual(0, journal.PendingCount);
                await journal.ReplayAsync((r, ct) => { ++sent; throw new Exception("Rejected admissions must not replay"); }, CancellationToken.None);
                Assert.AreEqual(1, sent);
            }
        }

        [Test]
        public void LostAdmissionResponseIsReplayedWithTheOriginalIdentityAfterReopenInEditor() => Fixtures.HarnessTestCase.Run(LostAdmissionResponseIsReplayedWithTheOriginalIdentityAfterReopen);

        public async Task LostAdmissionResponseIsReplayedWithTheOriginalIdentityAfterReopen()
        {
            string directory = Fixtures.HarnessTestCase.NewDirectory();
            var adapter = new Fixtures.ControlledHarness();
            using (var host = new PersistentTaskRuntime(new MemoryTaskStore(), new[] { adapter }))
            {
                int requests = 0;
                string accepted = null;
                Func<SubmitTaskRequest, CancellationToken, Task<HarnessTask>> send = (outgoing, token) =>
                {
                    ++requests;
                    var task = host.Submit(new TaskSubmission("owner", outgoing.ConversationId, outgoing.SubmissionKey,
                        outgoing.HarnessId, outgoing.WorkspaceKey, outgoing.Objective));
                    accepted = task.Id;
                    if (requests == 1) throw new IOException("Admission reply lost after commit.");
                    return Task.FromResult(task);
                };
                var request = new SubmitTaskRequest { SubmissionKey = "invocation-1", ConversationId = "chat",
                    HarnessId = "controlled", WorkspaceKey = "project", Objective = "Build the report" };
                using (var journal = new SubmissionJournal(directory))
                {
                    bool failed = false;
                    try { await journal.SubmitAsync(request, send, CancellationToken.None); }
                    catch (PendingSubmissionException) { failed = true; }
                    Assert.True(failed);
                    Assert.AreEqual(1, journal.PendingCount);
                    Assert.Throws<IOException>(() => new SubmissionJournal(directory), "Only one foreground writer may own a journal.");
                }
                using (var reopened = new SubmissionJournal(directory))
                {
                    Assert.AreEqual(1, reopened.PendingCount);
                    await reopened.ReplayAsync(send, CancellationToken.None);
                    Assert.AreEqual(0, reopened.PendingCount);
                    Assert.AreEqual(1, host.List("owner").Length);
                    Assert.AreEqual(accepted, host.List("owner")[0].Id);
                    request.SubmissionKey = "invocation-2";
                    await reopened.SubmitAsync(request, send, CancellationToken.None);
                    Assert.AreEqual(2, host.List("owner").Length, "Intentional repeated objectives remain separate requests.");
                }
                adapter.Finish.TrySetResult("report ready");
                await host.WaitForIdleAsync();
            }
        }

        [Test]
        public void PendingIdentityCannotBeReassignedToAnotherObjectiveInEditor() => Fixtures.HarnessTestCase.Run(PendingIdentityCannotBeReassignedToAnotherObjective);

        public async Task PendingIdentityCannotBeReassignedToAnotherObjective()
        {
            string directory = Fixtures.HarnessTestCase.NewDirectory();
            using (var journal = new SubmissionJournal(directory))
            {
                var request = new SubmitTaskRequest { SubmissionKey = "call", ConversationId = "chat", HarnessId = "controlled",
                    WorkspaceKey = "project", Objective = "First objective" };
                int calls = 0;
                Func<SubmitTaskRequest, CancellationToken, Task<HarnessTask>> send = (r, ct) =>
                { ++calls; throw new IOException("offline"); };
                try { await journal.SubmitAsync(request, send, CancellationToken.None); } catch (PendingSubmissionException) { }
                request.Objective = "Other objective";
                bool rejected = false;
                try { await journal.SubmitAsync(request, send, CancellationToken.None); } catch (ArgumentException) { rejected = true; }
                Assert.True(rejected);
                Assert.AreEqual(1, calls);
                Assert.AreEqual(1, journal.PendingCount);
            }
        }
    }
}
