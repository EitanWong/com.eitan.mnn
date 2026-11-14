using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using MNN.Samples.SiriAI.Harnesses;
using NUnit.Framework;

namespace MNN.Samples.SiriAI.Tests
{
    public sealed class SiriAIHarnessNativeToolStateTests
    {
        [Test] public void NativeToolsStayDetachedAndDurable() => Fixtures.HarnessTestCase.Run(VerifyAsync);
        public async Task VerifyAsync()
        {
            var omitted = new[] { "rawInput" };
            var call = new HarnessNativeToolCall("tool", "Native title", "read", "completed", "{\"toolCallId\":\"tool\",\"rawOutput\":42}", omitted);
            var input = new[] { call }; var tools = new HarnessNativeToolState(input, true);
            omitted[0] = "mutation"; input[0] = null;
            Assert.AreEqual("rawInput", tools.Calls[0].OmittedFields[0]);
            var activity = new HarnessActivity(HarnessWaitingFor.Permission, "permission", "Needs permission", nativeTools: tools);
            tools.Calls[0] = null; var copied = activity.NativeTools.Calls; copied[0] = null;
            var adapter = new Fixtures.ControlledHarness(); string directory = Fixtures.HarnessTestCase.NewDirectory();
            try
            {
                using (var store = new FileTaskStore(directory))
                using (var runtime = new PersistentTaskRuntime(store, new[] { adapter }))
                {
                    var task = runtime.Submit(new TaskSubmission("alice", "chat", "tool-state", adapter.Id, "project", "Work"));
                    await adapter.Started.Task; adapter.Publish(activity);
                    var detached = runtime.Get(task.Id, "alice"); var calls = detached.NativeTools.Calls; calls[0] = null;
                    detached.NativeTools.Calls[0].OmittedFields[0] = "changed";
                    Assert.AreEqual("Native title", store.Load()[0].NativeTools.Calls[0].Title);
                    Assert.AreEqual("rawInput", runtime.Get(task.Id, "alice").NativeTools.Calls[0].OmittedFields[0]);
                    var wire = TaskHostJson.Read<HarnessTask>(TaskHostJson.Write(runtime.Get(task.Id, "alice")));
                    Assert.AreEqual("completed", wire.NativeTools.Calls[0].Status); Assert.True(wire.NativeTools.Truncated);
                    var dashboard = new HarnessTaskDashboard(); long generation = dashboard.ResetScope(); Assert.True(dashboard.Apply(generation, new[] { wire }));
                    dashboard.Tasks[0].NativeTools.Calls[0] = null;
                    Assert.AreEqual("tool", dashboard.Tasks[0].NativeTools.Calls[0].ToolCallId);
                    dashboard.ResetScope(); Assert.False(dashboard.Apply(generation, new[] { wire })); Assert.IsEmpty(dashboard.Tasks);
                    adapter.Publish(new HarnessActivity(HarnessWaitingFor.Permission, "permission", "Still waiting"));
                    Assert.AreEqual("permission", runtime.Get(task.Id, "alice").RequestId);
                    Assert.AreEqual(call.Payload, runtime.Get(task.Id, "alice").NativeTools.Calls[0].Payload);
                    adapter.Finish.TrySetResult("fresh result"); await runtime.WaitForIdleAsync();
                    Assert.AreEqual("fresh result", runtime.Get(task.Id, "alice").Result);
                    Assert.AreEqual(call.Payload, store.Load()[0].NativeTools.Calls[0].Payload);
                }
                using (var store = new FileTaskStore(directory))
                    Assert.AreEqual("Native title", store.Load()[0].NativeTools.Calls[0].Title);
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
            Assert.Throws<SerializationException>(() => new HarnessNativeToolState(new[] { call, call }));
            Assert.Throws<SerializationException>(() => new HarnessNativeToolState(Enumerable.Range(0, 32).Select(i =>
                new HarnessNativeToolCall("t" + i, "title", "read", "completed", new string('x', 4096))).ToArray()));
        }
    }
}
