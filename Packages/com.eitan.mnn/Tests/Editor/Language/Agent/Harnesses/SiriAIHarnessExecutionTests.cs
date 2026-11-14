using System.Threading.Tasks;
using MNN.Samples.SiriAI.Harnesses;
using NUnit.Framework;

namespace MNN.Samples.SiriAI.Tests
{
    public sealed class SiriAIHarnessExecutionTests
    {
        [Test] public void ExecutionStepsSurviveHostRestartAndRemainInOrder() => Fixtures.HarnessTestCase.Run(VerifyAsync);
        public async Task VerifyAsync()
        {
            string directory = Fixtures.HarnessTestCase.NewDirectory(), id;
            var adapter = new Fixtures.ControlledHarness();
            using (var store = new FileTaskStore(directory))
            using (var runtime = new PersistentTaskRuntime(store, new[] { adapter }))
            {
                id = runtime.Submit(new TaskSubmission("alice", "chat", "execution", "controlled", "workspace", "run tools")).Id;
                await adapter.Started.Task;
                adapter.Publish(new HarnessActivity(HarnessWaitingFor.None, null, "Read file",
                    executionKind: HarnessExecutionKind.ToolCall, executionDetail: "{\"path\":\"input.txt\"}"));
                adapter.Publish(new HarnessActivity(HarnessWaitingFor.None, null, "Read file",
                    executionKind: HarnessExecutionKind.ToolResult, executionDetail: "file content", executionState: "Completed"));
                adapter.Finish.TrySetResult("done"); await runtime.WaitForIdleAsync();
                var task = runtime.Get(id, "alice"); Assert.AreEqual(3, task.Execution.Length);
                task.Execution[0] = null; Assert.IsNotNull(runtime.Get(id, "alice").Execution[0]);
            }
            using (var store = new FileTaskStore(directory))
            using (var runtime = new PersistentTaskRuntime(store, new[] { new Fixtures.ControlledHarness() }))
            {
                var task = TaskHostJson.Read<HarnessTask>(TaskHostJson.Write(runtime.Get(id, "alice")));
                Assert.AreEqual(3, task.Execution.Length);
                Assert.AreEqual(HarnessExecutionKind.ToolCall, task.Execution[0].Kind);
                Assert.AreEqual("{\"path\":\"input.txt\"}", task.Execution[0].Text);
                Assert.AreEqual("file content", task.Execution[1].Text);
                Assert.AreEqual("Completed", task.Execution[1].State);
                Assert.AreEqual(3, task.Execution[2].Sequence); Assert.AreEqual("done", task.Execution[2].Text);
            }
        }
    }
}
