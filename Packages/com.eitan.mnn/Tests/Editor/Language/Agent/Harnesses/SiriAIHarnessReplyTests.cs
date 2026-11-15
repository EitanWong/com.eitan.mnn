using System;
using System.Text;
using MNN.Samples.SiriAI.Harnesses;
using NUnit.Framework;

namespace MNN.Samples.SiriAI.Tests
{
    public sealed class SiriAIHarnessReplyTests
    {
        [Test] public void VoiceReplyRequiresTheExactCurrentRequestAndAnExplicitPermissionDecision()
        {
            var task = TaskHostJson.Read<HarnessTask>(Encoding.UTF8.GetBytes(
                "{\"Id\":\"task\",\"ConversationId\":\"chat\",\"State\":1,\"WaitingFor\":2,\"RequestId\":\"request\"}"));
            var reply = new HarnessTaskReply { TaskId = "task", RequestId = "request", Text = "Allow this request" };
            Assert.Throws<ArgumentException>(() => reply.CreateRequest(task, "chat"));
            reply.Allow = true;
            Assert.True(reply.CreateRequest(task, "chat").Allow);
            Assert.Throws<InvalidOperationException>(() => reply.CreateRequest(task, "other-chat"));
            reply.RequestId = "stale";
            Assert.Throws<InvalidOperationException>(() => reply.CreateRequest(task, "chat"));
        }
    }
}
