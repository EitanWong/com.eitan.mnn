using System;
using MNN.Samples.SiriAI.Harnesses;
using NUnit.Framework;

namespace MNN.Samples.SiriAI.Tests
{
    public sealed class SiriAIHarnessFrontendSessionTests
    {
        [Test] public void ConversationAndInputHaveIndependentLifetimes()
        {
            var session = new HarnessFrontendSession();
            Assert.IsNull(session.Scope);
            Assert.Throws<InvalidOperationException>(() => session.CreateSubmission("work", "agent", "project"));
            session.BindConversation("chat-a");
            var first = session.Scope;
            session.BindConversation("chat-a");
            Assert.AreSame(first, session.Scope);
            var inputs = new[] { new HarnessContentPart(HarnessContentKind.Text, "input") };
            session.BindInput("turn-a", "Confirmed context", inputs);
            var submission = session.CreateSubmission("work", "agent", "project");
            Assert.AreEqual("chat-a", submission.ConversationId);
            Assert.AreEqual(submission.SubmissionKey, session.CreateSubmission("work", "agent", "project").SubmissionKey);
            Assert.AreEqual("input", submission.Attachments[0].Payload);
            submission.Attachments[0] = null;
            Assert.NotNull(session.CreateSubmission("work", "agent", "project").Attachments[0]);
            session.BindConversation("chat-b");
            Assert.AreNotSame(first, session.Scope);
            Assert.AreEqual(0, session.CreateSubmission("other", "agent", "project").Attachments.Length);
            session.BindConversation("chat-a");
            Assert.AreNotSame(first, session.Scope, "Returning to A must not revive an in-flight lease from its previous generation.");
            session.BindInputSnapshot("turn-snapshot", "Keep this exact path /workspace/important.txt", new[] { "user: preserve all files", "assistant: acknowledged" });
            var snapshot = session.CreateSubmission("Inspect the project", "agent", "project");
            Assert.AreEqual("Keep this exact path /workspace/important.txt", snapshot.Attachments[0].Payload);
            StringAssert.Contains("preserve all files", snapshot.Objective);
            session.BindInputSnapshot("long-snapshot", new string('x', 65000), new[] { new string('p', 5000), "user: recent complete constraint" });
            var longInput = session.CreateSubmission("Inspect", "agent", "project");
            Assert.AreEqual(65000, longInput.Attachments[0].Payload.Length);
            StringAssert.Contains("recent complete constraint", longInput.Objective);
            StringAssert.Contains("omitted", longInput.Objective);
            session.BindConversation(null);
            Assert.IsNull(session.Scope);
        }
    }
}
