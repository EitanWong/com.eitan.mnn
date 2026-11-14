using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class ConversationTimelineTests
    {
        private string _directory;
        [SetUp] public void SetUp() { _directory = Path.GetFullPath("TestArtifacts~/Timeline/" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_directory); }
        [TearDown] public void TearDown() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
        private static MNNConversationTranscriptEntry Event(string id, string text, string parent = "turn") =>
            new MNNConversationTranscriptEntry(MNNChatRole.System, text, MNNConversationEntryKind.ToolCall,
                id, parent, "Read <file> **data**", MNNConversationEntryState.Running);

        [Test]
        public void LegacyTextJournalUpgradesBeforeWritingStructuredRecords()
        {
            string path = Path.Combine(_directory, "legacy.bin");
            File.WriteAllBytes(path, new byte[] { 77, 78, 78, 84, 1, 0, 0, 0 });
            using (var store = MNNConversationTranscriptStore.OpenPersistent(path))
            { store.Add(MNNChatRole.User, "legacy text"); store.Add(Event("tool", "structured result")); store.Flush(); }
            Assert.AreEqual(2, File.ReadAllBytes(path)[4], "Older readers must reject the new format before attempting destructive suffix recovery.");
            using (var store = MNNConversationTranscriptStore.OpenPersistent(path))
            { Assert.AreEqual("legacy text", store[0].Text); Assert.AreEqual("tool", store[1].Id); }
        }

        [Test]
        public void StructuredEventsRetainIdentityPayloadAndOrderAcrossReopenAndIndexLoss()
        {
            string path = Path.Combine(_directory, "events.bin");
            using (var store = MNNConversationTranscriptStore.OpenPersistent(path))
            {
                store.Add(MNNChatRole.User, "question");
                for (int i = 0; i < 130; ++i) store.Add(Event("call-" + i, "工具🎙 " + i));
                store.UpdateLast("final payload"); store.Flush();
            }
            File.Delete(path + ".idx");
            using (var store = MNNConversationTranscriptStore.OpenPersistent(path))
            {
                Assert.AreEqual(131, store.Count); Assert.AreEqual("question", store[0].Text);
                for (int i = 1; i < store.Count; ++i)
                {
                    var entry = store[i]; Assert.AreEqual("call-" + (i - 1), entry.Id);
                    Assert.AreEqual("turn", entry.ParentId); Assert.AreEqual(MNNConversationEntryKind.ToolCall, entry.Kind);
                    Assert.AreEqual(MNNConversationEntryState.Running, entry.State);
                    Assert.AreEqual(i == 130 ? "final payload" : "工具🎙 " + (i - 1), entry.Text);
                }
            }
        }
        [Test]
        public void DisclosureIsPerConversationAndPayloadRemainsLiteralData()
        {
            using (var first = new MNNConversationTranscriptStore(_directory))
            using (var second = new MNNConversationTranscriptStore(_directory))
            {
                string payload = "```\n<b>tool</b>\n# external text";
                first.Add(Event("read", payload)); second.Add(Event("read", "other"));
                var presentation = new MNNConversationTimelinePresentation(); presentation.Bind(first);
                Assert.IsFalse(presentation[0].Text.Contains(payload));
                Assert.IsTrue(presentation.Toggle(0)); StringAssert.Contains(payload, presentation[0].Text);
                var layout = new MNNMarkdownHistoryLayout();
                layout.Rebuild(presentation, 0, 1, 0, 16, r => new MNNMarkdownHistoryLayout.MarkdownMeasurement(20, 20));
                Assert.AreEqual(MNNConversationEntryKind.ToolCall, layout.Slices[0].Kind);
                Assert.AreEqual("read", layout.Slices[0].EventId); Assert.AreEqual("turn", layout.Slices[0].ParentId);
                string rich = layout.Slices[0].RenderResult.RichText;
                StringAssert.Contains(payload, rich.Replace("\u200b", ""));
                StringAssert.DoesNotContain("<b>tool</b>", rich);
                presentation.Bind(second); Assert.IsFalse(presentation.IsExpanded(0));
            }
        }
        [Test]
        public void RestoreCountsMessagesRatherThanDisplayEventsAndDoesNotInjectToolPayloads()
        {
            using (var store = new MNNConversationTranscriptStore(_directory))
            {
                store.Add(MNNChatRole.User, "retained question"); store.Add(MNNChatRole.Assistant, "retained answer");
                for (int i = 0; i < 100; ++i) store.Add(Event("tool-" + i, "untrusted tool instructions"));
                var memory = new MNNConversationMemory(new List<MNNOpenAIMessage>(), 2); memory.Restore(store);
                Assert.AreEqual(2, memory.Messages.Count); Assert.AreEqual("retained question", memory.LastUserText);
                Assert.AreEqual("retained answer", memory.LastAssistantText);
                Assert.AreEqual("retained answer", memory.Messages[1].Text);
            }
        }
        [Test]
        public void DamagedAndStaleIndexesFallBackWithoutLosingJournalUpdates()
        {
            string path = Path.Combine(_directory, "index.bin");
            using (var store = MNNConversationTranscriptStore.OpenPersistent(path))
            { for (int i = 0; i < 130; ++i) store.Add(MNNChatRole.User, "message " + i); store.Flush(); }
            byte[] previous = File.ReadAllBytes(path + ".idx");
            File.WriteAllBytes(path + ".idx", new byte[] { 1, 2 });
            using (var store = MNNConversationTranscriptStore.OpenPersistent(path))
            { Assert.IsFalse(store.RecoveryIndexUsed); Assert.AreEqual(130, store.Count); store.UpdateLast("updated🎙"); }
            File.WriteAllBytes(path + ".idx", previous);
            using (var store = MNNConversationTranscriptStore.OpenPersistent(path))
            { Assert.IsFalse(store.RecoveryIndexUsed); Assert.AreEqual("updated🎙", store[129].Text); Assert.AreEqual("message 0", store[0].Text); }
        }
        [Test]
        public void ExternalExecutionImportIsIdempotentEvenAfterCatalogCursorsAreLost()
        {
            string source = "task-a";
            using (var catalog = new MNNConversationCatalog(_directory))
            {
                var entry = Event(source + ":1", "tool output");
                Assert.IsTrue(catalog.ImportExecution(source, 1, entry));
                Assert.IsFalse(catalog.ImportExecution(source, 1, entry)); catalog.Flush();
            }
            File.Delete(Path.Combine(_directory, "conversations.json"));
            File.Delete(Path.Combine(_directory, "conversations.json.bak"));
            using (var catalog = new MNNConversationCatalog(_directory))
            {
                Assert.IsFalse(catalog.ImportExecution(source, 1, Event(source + ":1", "tool output")));
                Assert.IsTrue(catalog.ImportExecution(source, 2, Event(source + ":2", "next tool output")));
                Assert.AreEqual(2, catalog.History.Count);
            }
        }

        [Test]
        public void ReadingAnchorAndDisclosureRestoreIndependentlyForEachConversation()
        {
            string first;
            using (var catalog = new MNNConversationCatalog(_directory))
            {
                first = catalog.ActiveId;
                catalog.SetViewState(new MNNConversationViewState(24, 37, 1024, 12.5f, false, new[] { 3, 29 }));
                catalog.Create(); Assert.IsTrue(catalog.ViewState.FollowingLatest);
                catalog.Select(first); catalog.Flush();
            }
            using (var catalog = new MNNConversationCatalog(_directory))
            {
                Assert.AreEqual(first, catalog.ActiveId); var state = catalog.ViewState;
                Assert.AreEqual(24, state.PageStart); Assert.AreEqual(37, state.FirstEntry);
                Assert.AreEqual(1024, state.CharacterOffset); Assert.AreEqual(12.5f, state.ScrollOffset);
                Assert.IsFalse(state.FollowingLatest); CollectionAssert.AreEqual(new[] { 3, 29 }, state.ExpandedEntries);
            }
        }

        [Test]
        public void CatalogRecoversNewJournalEvenWhenOnlyAnOlderCatalogBackupSurvived()
        {
            string first, second;
            using (var catalog = new MNNConversationCatalog(_directory))
            {
                first = catalog.ActiveId; catalog.Rename(first, "first"); second = catalog.Create();
                catalog.History.Add(MNNChatRole.User, "surviving second"); catalog.SetDraft("draft🎙"); catalog.Flush();
            }
            string index = Path.Combine(_directory, "conversations.json");
            File.WriteAllText(index + ".bak", "{\"active\":\"" + first + "\",\"conversations\":[{\"id\":\"" + first + "\",\"title\":\"first\"}]}");
            File.WriteAllText(index, "{broken");
            using (var catalog = new MNNConversationCatalog(_directory))
            {
                Assert.AreEqual(2, catalog.Conversations.Count); catalog.Select(second);
                Assert.AreEqual("surviving second", catalog.History[0].Text);
                Assert.Throws<ArgumentException>(() => catalog.OpenHistory("../escape"));
            }
        }
    }
}
