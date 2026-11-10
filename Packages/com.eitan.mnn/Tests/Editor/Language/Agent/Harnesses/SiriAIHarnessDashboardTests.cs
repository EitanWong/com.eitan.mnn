using System;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MNN.Samples.SiriAI.Harnesses;
using NUnit.Framework;

namespace MNN.Samples.SiriAI.Tests
{
    public sealed class SiriAIHarnessDashboardTests
    {
        [Test] public void StructuredInputPreservesTypesAndSnapshotOwnershipInEditor() => Fixtures.HarnessTestCase.Run(StructuredInputPreservesTypesAndSnapshotOwnership);
        public async Task StructuredInputPreservesTypesAndSnapshotOwnership()
        {
            var form = new HarnessInputForm { Fields = new[] {
                new HarnessInputField { Name = "name", Type = "string", Label = "Name", Required = true },
                new HarnessInputField { Name = "count", Type = "integer", Label = "Count", Required = true, Defaults = new[] { "2" } },
                new HarnessInputField { Name = "enabled", Type = "boolean", Label = "Enabled", Required = true, Defaults = new[] { "false" } },
                new HarnessInputField { Name = "tags", Type = "array", Label = "Tags", Required = true,
                    Choices = new[] { new HarnessInputChoice { Value = "中", Label = "Chinese" } }, Defaults = new[] { "中" } },
                new HarnessInputField { Name = "suggestion", Type = "string", Label = "Suggestion", Required = true, AllowOther = true, Secret = true,
                    Choices = new[] { new HarnessInputChoice { Value = "preset", Label = "Preset", Description = "A suggested answer" } } },
                new HarnessInputField { Name = "note", Type = "string", Label = "Note" } } };
            var draft = form.CreateDraft();
            Assert.IsNull(draft["note"], "An absent optional value must stay absent.");
            draft["name"] = new[] { "用户\"\\\n" };
            draft["suggestion"] = new[] { "custom answer" };
            draft["tags"] = new[] { "unlisted" }; Assert.Throws<ArgumentException>(() => form.EncodeValues(draft));
            draft["tags"] = new[] { "中" };
            var encoded = form.EncodeValues(draft);
            StringAssert.Contains("custom answer", encoded);
            var values = TaskHostJson.Read<FormAnswer>(Encoding.UTF8.GetBytes(encoded));
            Assert.AreEqual(draft["name"][0], values.Name); Assert.AreEqual(2, values.Count);
            Assert.IsFalse(values.Enabled); CollectionAssert.AreEqual(new[] { "中" }, values.Tags);
            Assert.IsFalse(encoded.Contains("note"));
            draft["count"] = new[] { "2.5" }; Assert.Throws<ArgumentException>(() => form.EncodeValues(draft));
            draft["count"] = new[] { "3" };
            var adapter = new Fixtures.ControlledHarness();
            string directory = Fixtures.HarnessTestCase.NewDirectory();
            try
            {
                using (var store = new FileTaskStore(directory))
                using (var runtime = new PersistentTaskRuntime(store, new[] { adapter }))
                {
                    var task = runtime.Submit(new TaskSubmission("alice", "chat", "form-key", adapter.Id, "project", "Work"));
                    await adapter.Started.Task;
                    adapter.Publish(new HarnessActivity(HarnessWaitingFor.Input, "form-1", "Settings", inputForm: form));
                    form.Fields[1].Defaults[0] = "99";
                    var snapshot = runtime.Get(task.Id, "alice");
                    snapshot.InputForm.Fields[1].Defaults[0] = "88";
                    snapshot.InputForm.Fields[3].Choices[0].Value = "mutated";
                    snapshot.InputForm.Fields[4].Choices[0].Description = "changed";
                    var reopened = store.Load()[0];
                    Assert.True(reopened.InputForm.Fields[4].AllowOther); Assert.True(reopened.InputForm.Fields[4].Secret);
                    Assert.AreEqual("A suggested answer", reopened.InputForm.Fields[4].Choices[0].Description);
                    Assert.AreEqual("2", reopened.InputForm.Fields[1].Defaults[0]);
                    Assert.AreEqual("中", reopened.InputForm.Fields[3].Choices[0].Value);
                    var dashboard = new HarnessTaskDashboard(); long generation = dashboard.ResetScope();
                    dashboard.Apply(generation, runtime.List("alice"));
                    var decline = dashboard.CreateReply(generation, task.Id, "form-1", "Decline", false, HarnessInputAction.Decline);
                    Assert.AreEqual(HarnessInputAction.Decline, decline.InputAction);
                    await runtime.RespondAsync(task.Id, "alice", "form-1", encoded, false, CancellationToken.None);
                    Assert.IsNull(runtime.Get(task.Id, "alice").InputForm);
                    adapter.Publish(new HarnessActivity(HarnessWaitingFor.Input, "form-2", "Again", inputForm: reopened.InputForm));
                    dashboard.Apply(generation, runtime.List("alice"));
                    Assert.Throws<InvalidOperationException>(() => dashboard.CreateReply(generation, task.Id, "form-1", encoded, false));
                    adapter.Finish.TrySetResult("done"); await runtime.WaitForIdleAsync();
                    Assert.IsNull(runtime.Get(task.Id, "alice").InputForm);
                }
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }
        [Test] public void ExternalInputPersistsAndFencesRepliesInEditor() => Fixtures.HarnessTestCase.Run(ExternalInputPersistsAndFencesReplies);
        public async Task ExternalInputPersistsAndFencesReplies()
        {
            foreach (var url in new[] { "file:///tmp/a", "javascript:alert(1)", "https://user:secret@host.test", "https://host.test/a b" })
                Assert.Throws<SerializationException>(() => new HarnessInputPrompt(HarnessInputMode.Url, url, "flow"));
            Assert.Throws<SerializationException>(() => new HarnessInputPrompt(HarnessInputMode.Text, "https://host.test"));
            Assert.Throws<ArgumentException>(() => new HarnessActivity(HarnessWaitingFor.Permission, "p", "Permission", inputPrompt: new HarnessInputPrompt(HarnessInputMode.Text)));
            var prompt = new HarnessInputPrompt(HarnessInputMode.Url, "https://example.invalid/授权?state=原样", "flow");
            var adapter = new Fixtures.ControlledHarness(); string directory = Fixtures.HarnessTestCase.NewDirectory();
            try
            {
                using (var store = new FileTaskStore(directory))
                using (var runtime = new PersistentTaskRuntime(store, new[] { adapter }))
                {
                    var task = runtime.Submit(new TaskSubmission("alice", "chat", "external-input", adapter.Id, "project", "Work"));
                    await adapter.Started.Task;
                    adapter.Publish(new HarnessActivity(HarnessWaitingFor.Input, "url-1", "Open link", inputPrompt: prompt));
                    var reopened = store.Load()[0];
                    Assert.AreEqual(prompt.Url, reopened.InputPrompt.Url); Assert.AreEqual("flow", reopened.InputPrompt.NativeId);
                    Assert.False(reopened.InputPrompt.BackendCompleted);
                    var dashboard = new HarnessTaskDashboard(); long generation = dashboard.ResetScope();
                    dashboard.Apply(generation, runtime.List("alice"));
                    Assert.AreEqual(prompt.Url, dashboard.InputUrl(generation, task.Id, "url-1"));
                    Assert.AreEqual("", dashboard.CreateReply(generation, task.Id, "url-1", "", false).Text);
                    Assert.AreEqual(HarnessInputAction.Decline, dashboard.CreateReply(generation, task.Id, "url-1", "", false, HarnessInputAction.Decline).InputAction);
                    Assert.Throws<InvalidOperationException>(() => dashboard.InputUrl(generation, task.Id, "old"));
                    await runtime.RespondAsync(task.Id, "alice", "url-1", "", false, CancellationToken.None);
                    Assert.IsNull(runtime.Get(task.Id, "alice").InputPrompt);
                    adapter.Publish(new HarnessActivity(HarnessWaitingFor.Input, "text-1", "Text", inputPrompt: new HarnessInputPrompt(HarnessInputMode.Text)));
                    dashboard.Apply(generation, runtime.List("alice"));
                    Assert.Throws<InvalidOperationException>(() => dashboard.InputUrl(generation, task.Id, "url-1"));
                    Assert.Throws<InvalidOperationException>(() => dashboard.InputUrl(generation, task.Id, "text-1"));
                    dashboard.ResetScope(); Assert.Throws<InvalidOperationException>(() => dashboard.CreateReply(generation, task.Id, "text-1", "", false));
                    adapter.Finish.TrySetResult("done"); await runtime.WaitForIdleAsync();
                    Assert.IsNull(runtime.Get(task.Id, "alice").InputPrompt);
                }
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }
        [DataContract] private sealed class FormAnswer
        {
            [DataMember(Name = "name")] public string Name;
            [DataMember(Name = "count")] public long Count;
            [DataMember(Name = "enabled")] public bool Enabled;
            [DataMember(Name = "tags")] public string[] Tags;
        }
        [Test]
        public void HostProfilesAndSelectionSurviveReopenWithoutCredentialFields()
        {
            string directory = Fixtures.HarnessTestCase.NewDirectory();
            using (var catalog = new HarnessProfileCatalog(directory))
            {
                catalog.Save(new HarnessHostProfile("desktop", "My desktop", "http://127.0.0.1:8765", "codex", "project"), true);
                catalog.Save(new HarnessHostProfile("remote", "Remote host", "https://host.example.test/agent/", "hermes", "notes"), false);
                Assert.AreEqual("desktop", catalog.SelectedId);
                catalog.Select("remote");
            }
            using (var reopened = new HarnessProfileCatalog(directory))
            {
                Assert.AreEqual("remote", reopened.SelectedId);
                Assert.AreEqual(2, reopened.Profiles.Length);
                Assert.AreEqual("hermes", reopened.Get("remote").HarnessId);
                Assert.AreEqual("https://host.example.test/agent/", reopened.Get("remote").Address);
                Assert.Throws<ArgumentException>(() => reopened.Select("unknown"));
            }
            string persisted = File.ReadAllText(Path.Combine(directory, "profiles.json"));
            Assert.False(persisted.ToLowerInvariant().Contains("token"));
            Assert.False(persisted.ToLowerInvariant().Contains("password"));
            Assert.Throws<ArgumentException>(() => new HarnessHostProfile("bad", "Bad", "https://user:secret@host.test", "codex", "project"));
            Assert.Throws<ArgumentException>(() => HarnessHostAddress.Parse("http://remote.test", false));
            Assert.Throws<ArgumentException>(() => HarnessHostAddress.Parse("http://127.0.0.1:8765", true));
            Assert.AreEqual("https://remote.test/", HarnessHostAddress.Parse("https://remote.test", true).AbsoluteUri);
        }

        [Test]
        public void ProfileGenerationAndExactPromptIdentityFenceOldResponsesInEditor() => Fixtures.HarnessTestCase.Run(ProfileGenerationAndExactPromptIdentityFenceOldResponses);

        public async Task ProfileGenerationAndExactPromptIdentityFenceOldResponses()
        {
            var adapter = new Fixtures.ControlledHarness();
            using (var runtime = new PersistentTaskRuntime(new MemoryTaskStore(), new[] { adapter }))
            {
                var task = runtime.Submit(new TaskSubmission("alice", "chat", "call", "controlled", "project", "Do the work"));
                await adapter.Started.Task;
                var dashboard = new HarnessTaskDashboard();
                long first = dashboard.ResetScope();
                adapter.Publish(new HarnessActivity(HarnessWaitingFor.Permission, "prompt-1", "Write file?"));
                var old = runtime.List("alice");
                Assert.True(dashboard.Apply(first, old));
                var reply = dashboard.CreateReply(first, task.Id, "prompt-1", "Allow once", true);
                Assert.AreEqual("prompt-1", reply.RequestId);
                Assert.True(reply.Allow);
                adapter.Publish(new HarnessActivity(HarnessWaitingFor.Permission, "prompt-2", "Delete another file?"));
                dashboard.Apply(first, runtime.List("alice"));
                dashboard.Apply(first, old);
                Assert.AreEqual("prompt-2", dashboard.Tasks[0].RequestId, "An older refresh must not replace a new permission request.");
                Assert.Throws<InvalidOperationException>(() => dashboard.CreateReply(first, task.Id, "prompt-1", "Allow once", true));
                long second = dashboard.ResetScope();
                Assert.AreEqual(0, dashboard.Tasks.Length);
                Assert.False(dashboard.Apply(first, old));
                Assert.Throws<InvalidOperationException>(() => dashboard.CreateReply(first, task.Id, "prompt-2", "Allow once", true));
                Assert.True(dashboard.Apply(second, runtime.List("alice")));
                Assert.AreEqual("prompt-2", dashboard.CreateReply(second, task.Id, "prompt-2", "Deny", false).RequestId);
                adapter.Finish.TrySetResult("done");
                await runtime.WaitForIdleAsync();
            }
        }

        [Test]
        public void UnchangedTaskPollsDoNotReportDashboardChanges()
        {
            var dashboard = new HarnessTaskDashboard(); long generation = dashboard.ResetScope();
            Assert.False(dashboard.Apply(generation, new HarnessTask[0]), "An empty repeated poll changes nothing.");
            var task = TaskHostJson.Read<HarnessTask>(Encoding.UTF8.GetBytes("{\"Id\":\"one\",\"OwnerId\":\"alice\",\"ConversationId\":\"chat\",\"SubmissionKey\":\"key\",\"HarnessId\":\"codex\",\"WorkspaceKey\":\"project\",\"Objective\":\"work\",\"Revision\":1,\"CreatedUtcTicks\":1,\"UpdatedUtcTicks\":1}"));
            Assert.True(dashboard.Apply(generation, new[] { task }));
            Assert.False(dashboard.Apply(generation, new[] { task }), "Same revision must not trigger a settings rebuild.");
        }
        [Test]
        public void RemovingProfilesIsPersistentIdempotentAndDoesNotSelectAnotherHost()
        {
            string directory = Fixtures.HarnessTestCase.NewDirectory();
            try
            {
                using (var catalog = new HarnessProfileCatalog(directory))
                {
                    catalog.Save(new HarnessHostProfile("one", "One", "https://one.example/", "codex", "project"), true);
                    catalog.Save(new HarnessHostProfile("two", "Two", "https://two.example/", "claude", "project"), false);
                    var remove = typeof(HarnessProfileCatalog).GetMethod("Remove"); Assert.NotNull(remove);
                    Assert.AreEqual(true, remove.Invoke(catalog, new object[] { "one" }));
                    Assert.IsNull(catalog.SelectedId, "Deleting the selected profile must not silently connect another host.");
                    Assert.AreEqual(false, remove.Invoke(catalog, new object[] { "one" }));
                }
                using (var reopened = new HarnessProfileCatalog(directory))
                { Assert.AreEqual(1, reopened.Profiles.Length); Assert.AreEqual("Two", reopened.Get("two").Name); Assert.IsNull(reopened.SelectedId); }
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }
    }
}
