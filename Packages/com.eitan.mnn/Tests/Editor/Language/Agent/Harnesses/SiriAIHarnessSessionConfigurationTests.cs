using System;
using System.IO;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using MNN.Samples.SiriAI.Harnesses;
using NUnit.Framework;

namespace MNN.Samples.SiriAI.Tests
{
    public sealed class SiriAIHarnessSessionConfigurationTests
    {
        [Test] public void SessionSettingsStayDetachedAndDurableInEditor() => Fixtures.HarnessTestCase.Run(VerifyAsync);
        public async Task VerifyAsync()
        {
            var settings = new HarnessSessionConfiguration { SessionId = "native-context", CurrentModeId = "ask",
                Modes = new[] { new HarnessSessionChoice { Value = "ask", Name = "Ask" } },
                Options = new[] { new HarnessSessionOption { Id = "model", Name = "Model", CurrentValue = "v2",
                    Choices = new[] { new HarnessSessionChoice { Value = "v2", Name = "Reasoner Plus", Group = "Models" } } },
                    new HarnessSessionOption { Id = "thinking", Name = "Reasoning", IsBoolean = true, BooleanValue = true } } };
            var adapter = new Fixtures.ControlledHarness(); string directory = Fixtures.HarnessTestCase.NewDirectory();
            try
            {
                using (var store = new FileTaskStore(directory))
                using (var runtime = new PersistentTaskRuntime(store, new[] { adapter }))
                {
                    var task = runtime.Submit(new TaskSubmission("alice", "chat", "session-config", adapter.Id, "project", "Work"));
                    await adapter.Started.Task;
                    adapter.Publish(new HarnessActivity(HarnessWaitingFor.Permission, "permission", "Allow action", sessionConfiguration: settings));
                    settings.Options[0].Choices[0].Name = "mutated"; settings.Modes[0].Name = "mutated";
                    var detached = runtime.Get(task.Id, "alice");
                    Assert.AreEqual("Reasoner Plus", detached.SessionConfiguration.Options[0].Choices[0].Name);
                    Assert.AreEqual("Ask", detached.SessionConfiguration.Modes[0].Name);
                    detached.SessionConfiguration.Options[0].CurrentValue = "mutated";
                    var saved = store.Load()[0]; Assert.AreEqual("v2", saved.SessionConfiguration.Options[0].CurrentValue);
                    Assert.True(saved.SessionConfiguration.Options[1].BooleanValue);
                    var wire = TaskHostJson.Read<HarnessTask>(TaskHostJson.Write(saved)); Assert.AreEqual("v2", wire.SessionConfiguration.Options[0].CurrentValue);
                    adapter.Publish(new HarnessActivity(HarnessWaitingFor.Permission, "permission", "More activity"));
                    Assert.AreEqual("permission", runtime.Get(task.Id, "alice").RequestId);
                    Assert.AreEqual("v2", runtime.Get(task.Id, "alice").SessionConfiguration.Options[0].CurrentValue);
                    adapter.Finish.TrySetResult("done"); await runtime.WaitForIdleAsync();
                    Assert.AreEqual("v2", store.Load()[0].SessionConfiguration.Options[0].CurrentValue);
                    Assert.AreEqual(HarnessTaskState.Completed, runtime.Get(task.Id, "alice").State);
                }
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
            settings.Options[0].Choices = new[] { new HarnessSessionChoice { Value = "v2", Name = "A" }, new HarnessSessionChoice { Value = "v2", Name = "B" } };
            Assert.Throws<SerializationException>(() => new HarnessActivity(HarnessWaitingFor.None, null, "Invalid", sessionConfiguration: settings));
        }
    }
}
