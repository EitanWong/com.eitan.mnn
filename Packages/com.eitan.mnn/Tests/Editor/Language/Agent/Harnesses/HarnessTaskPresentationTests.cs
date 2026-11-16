using System;
using System.Text;
using MNN.Samples.SiriAI.Harnesses;
using NUnit.Framework;

namespace MNN.Samples.SiriAI.Tests
{
    public sealed class HarnessTaskPresentationTests
    {
        private static HarnessTask Task(string id, HarnessTaskState state, long updated = 20,
            HarnessWaitingFor waiting = HarnessWaitingFor.None) => TaskHostJson.Read<HarnessTask>(Encoding.UTF8.GetBytes(
                "{\"Id\":\"" + id + "\",\"OwnerId\":\"owner\",\"ConversationId\":\"chat\",\"SubmissionKey\":\"key\"," +
                "\"HarnessId\":\"agent\",\"WorkspaceKey\":\"project\",\"Objective\":\"Find the cause\",\"Revision\":1," +
                "\"CreatedUtcTicks\":10,\"UpdatedUtcTicks\":" + updated + ",\"State\":" + (int)state +
                ",\"WaitingFor\":" + (int)waiting + (waiting == HarnessWaitingFor.None ? "" : ",\"RequestId\":\"request\"") + "}"));

        [Test] public void ControlsReflectConfirmedStateAndBackendCapability()
        {
            var queued = new HarnessTaskPresentation(Task("q", HarnessTaskState.Queued), HarnessCapabilities.None);
            Assert.True(queued.CanPause); Assert.True(queued.CanCancel); Assert.False(queued.CanResume);
            var paused = new HarnessTaskPresentation(Task("p", HarnessTaskState.Paused), HarnessCapabilities.None);
            Assert.True(paused.CanResume); Assert.True(paused.CanCancel); Assert.False(paused.CanPause);
            var running = new HarnessTaskPresentation(Task("r", HarnessTaskState.Running), HarnessCapabilities.Cancel);
            Assert.True(running.CanCancel); Assert.False(running.CanPause);
            Assert.AreEqual("Running agents cannot be safely paused by this host. Queued tasks can be paused.", running.PauseHintKey);
            Assert.False(new HarnessTaskPresentation(Task("r", HarnessTaskState.Running), HarnessCapabilities.None).CanCancel);
            foreach (var state in new[] { HarnessTaskState.Cancelling, HarnessTaskState.Completed, HarnessTaskState.Cancelled,
                HarnessTaskState.Failed, HarnessTaskState.Interrupted })
            {
                var row = new HarnessTaskPresentation(Task("end", state), HarnessCapabilities.Cancel);
                Assert.False(row.CanCancel); Assert.False(row.CanPause); Assert.False(row.CanResume);
            }
        }

        [Test] public void AttentionAndLiveWorkPrecedeRecentFinishedTasksWithoutInventingProgress()
        {
            var running = Task("running", HarnessTaskState.Running, 30);
            var permission = Task("permission", HarnessTaskState.Running, 20, HarnessWaitingFor.Permission);
            var input = Task("input", HarnessTaskState.Running, 25, HarnessWaitingFor.Input);
            var failed = Task("failed", HarnessTaskState.Failed, 50);
            var ordered = HarnessTaskPresentation.Order(new[] { failed, running, permission, input });
            CollectionAssert.AreEqual(new[] { "input", "permission", "running", "failed" }, Array.ConvertAll(ordered, p => p.Id));
            CollectionAssert.AreEqual(new[] { "input", "permission", "running" }, Array.ConvertAll(HarnessTaskPresentation.Active(ordered), p => p.Id));
            Assert.IsEmpty(HarnessTaskPresentation.Active(new[] { failed, Task("done", HarnessTaskState.Completed), Task("cancel", HarnessTaskState.Cancelled) }));
            var row = new HarnessTaskPresentation(permission, HarnessCapabilities.Permission);
            Assert.True(row.NeedsAttention); Assert.AreEqual("Waiting for permission", row.StatusKey);
            Assert.AreEqual("Find the cause", row.Title);
            Assert.AreEqual("No execution update yet.", row.ProgressText);
            Assert.AreEqual("Waiting for input", new HarnessTaskPresentation(input, HarnessCapabilities.Input).StatusKey);
            Assert.Throws<ArgumentNullException>(() => new HarnessTaskPresentation(null, HarnessCapabilities.None));
        }
    }
}
