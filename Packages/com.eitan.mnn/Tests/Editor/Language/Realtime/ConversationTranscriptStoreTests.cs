using System;
using System.IO;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class ConversationTranscriptStoreTests
    {
        private string _directory;
        [SetUp] public void SetUp() { _directory = Path.GetFullPath("TestArtifacts~/TranscriptStore/" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_directory); }
        [TearDown] public void TearDown() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
        [Test]
        public void SessionHistorySpillsInBlocksAndClearRemovesOwnedFile()
        {
            using (var store = new MNNConversationTranscriptStore(_directory))
            {
                for (int i = 0; i < 10000; ++i) store.Add(MNNChatRole.User, "Message " + i + "🎙你好");
                store.UpdateLast("final🎙");
                foreach (int i in new[] { 0, 63, 64, 9998, 4096 }) Assert.AreEqual("Message " + i + "🎙你好", store[i].Text);
                Assert.AreEqual("final🎙", store[9999].Text);
                Assert.LessOrEqual(store.ResidentCount, MNNConversationTranscriptStore.BlockSize * 3);
                Assert.AreEqual(1, Directory.GetFiles(_directory).Length);
                store.Clear();
                Assert.AreEqual(0, store.Count);
                Assert.AreEqual(0, Directory.GetFiles(_directory).Length);
                store.Add(MNNChatRole.Assistant, "fresh"); Assert.AreEqual("fresh", store[0].Text);
            }
            Assert.AreEqual(0, Directory.GetFiles(_directory).Length);
        }
        [Test]
        public void PersistentJournalRestoresUpdatesAndDropsOnlyIncompleteCrashTail()
        {
            string path = Path.Combine(_directory, "conversation.bin");
            using (var store = MNNConversationTranscriptStore.OpenPersistent(path))
            {
                for (int i = 0; i < 130; ++i) store.Add(MNNChatRole.User, "turn " + i);
                store.UpdateLast("完整🎙"); store.Flush();
            }
            long validLength = new FileInfo(path).Length;
            using (var file = new FileStream(path, FileMode.Append)) file.Write(new byte[] { 22, 0, 0, 0, 1 }, 0, 5);
            using (var recovered = MNNConversationTranscriptStore.OpenPersistent(path))
            {
                Assert.AreEqual(130, recovered.Count);
                Assert.AreEqual("完整🎙", recovered[129].Text);
                Assert.AreEqual("turn 0", recovered[0].Text);
                Assert.AreEqual(5, recovered.RecoveredBytes);
                Assert.AreEqual(validLength, new FileInfo(path).Length);
                recovered.Add(MNNChatRole.Assistant, "next");
            }
            using (var reopened = MNNConversationTranscriptStore.OpenPersistent(path))
            { Assert.AreEqual(131, reopened.Count); Assert.AreEqual("next", reopened[130].Text); }
        }
        [Test]
        public void ReopeningLargeHistoryDoesNotDecodeEveryHistoricalMessage()
        {
            string path = Path.Combine(_directory, "indexed.bin");
            string text = new string('中', 256);
            using (var store = MNNConversationTranscriptStore.OpenPersistent(path))
            { for (int i = 0; i < 20000; ++i) store.Add(MNNChatRole.User, text); store.Flush(); }
            long before = GC.GetAllocatedBytesForCurrentThread();
            using (var store = MNNConversationTranscriptStore.OpenPersistent(path))
            {
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.AreEqual(20000, store.Count);
                Assert.AreEqual(text, store[0].Text);
                Assert.AreEqual(text, store[19999].Text);
                Assert.Less(allocated, 512 * 1024, "Opening must decode only the bounded tail, not all historical strings.");
            }
        }

        [Test]
        public void QuotaAndInvalidUnicodeDoNotCommitPartialEntries()
        {
            string path = Path.Combine(_directory, "bounded.bin");
            using (var store = MNNConversationTranscriptStore.OpenPersistent(path, maximumBytes: 256))
            {
                store.Add(MNNChatRole.User, "kept");
                Assert.Throws<InvalidOperationException>(() => store.Add(MNNChatRole.Assistant, new string('x', 256)));
                Assert.Throws<ArgumentException>(() => store.Add(MNNChatRole.User, "broken\uD83C"));
                Assert.AreEqual(1, store.Count); Assert.AreEqual("kept", store[0].Text);
            }
            using (var reopened = MNNConversationTranscriptStore.OpenPersistent(path)) Assert.AreEqual(1, reopened.Count);
        }
        [Test]
        public void PersistentClearCanBeReopenedAndSealedBlockUpdatesSurviveRecovery()
        {
            string path = Path.Combine(_directory, "clear.bin");
            using (var store = MNNConversationTranscriptStore.OpenPersistent(path))
            {
                for (int i = 0; i < 64; ++i) store.Add(MNNChatRole.Assistant, "old " + i);
                store.UpdateLast("sealed update🎙"); store.Add(MNNChatRole.User, "tail");
            }
            using (var restored = MNNConversationTranscriptStore.OpenPersistent(path))
            {
                Assert.AreEqual("sealed update🎙", restored[63].Text); Assert.AreEqual("tail", restored[64].Text);
                restored.Clear(); restored.Add(MNNChatRole.User, "fresh");
            }
            using (var reopened = MNNConversationTranscriptStore.OpenPersistent(path))
            { Assert.AreEqual(1, reopened.Count); Assert.AreEqual("fresh", reopened[0].Text); }
        }
        [Test]
        public void CorruptJournalSuffixDoesNotDamageThePreviousRecord()
        {
            string path = Path.Combine(_directory, "corrupt.bin"); long valid;
            using (var store = MNNConversationTranscriptStore.OpenPersistent(path))
            { store.Add(MNNChatRole.User, "retained"); store.Flush(); valid = new FileInfo(path).Length; store.Add(MNNChatRole.Assistant, "lost"); }
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite))
            { stream.Position = stream.Length - 1; stream.WriteByte(255); }
            using (var store = MNNConversationTranscriptStore.OpenPersistent(path))
            { Assert.AreEqual(1, store.Count); Assert.AreEqual("retained", store[0].Text); Assert.Greater(store.RecoveredBytes, 0); }
            Assert.AreEqual(valid, new FileInfo(path).Length);
        }
        [TestCase(0)] [TestCase(63)] [TestCase(64)] [TestCase(65)]
        [TestCase(127)] [TestCase(128)] [TestCase(129)]
        public void DisposingFlushesUncheckpointedTailAcrossBatchBoundaries(int count)
        {
            string path = Path.Combine(_directory, "batch.bin");
            using (var store = MNNConversationTranscriptStore.OpenPersistent(path))
            {
                for (int i = 0; i < count; ++i) store.Add(MNNChatRole.User, "message " + i);
                if (count > 0) store.UpdateLast("last🎙");
            }
            using (var reopened = MNNConversationTranscriptStore.OpenPersistent(path))
            {
                Assert.AreEqual(count, reopened.Count);
                for (int i = 0; i < count - 1; ++i) Assert.AreEqual("message " + i, reopened[i].Text);
                if (count > 0) Assert.AreEqual("last🎙", reopened[count - 1].Text);
            }
        }
        [Test]
        public void CachedReadsCannotMoveTheAppendCursorOrOverwriteDurableEntries()
        {
            string path = Path.Combine(_directory, "cursor.bin");
            using (var store = MNNConversationTranscriptStore.OpenPersistent(path))
            {
                for (int i = 0; i < 200; ++i)
                {
                    store.Add(MNNChatRole.Assistant, "message " + i);
                    Assert.AreEqual(i == 0 ? "message 0" : "updated 0", store[0].Text);
                    store.UpdateLast("updated " + i);
                }
                store.Flush();
            }
            using (var reopened = MNNConversationTranscriptStore.OpenPersistent(path))
            {
                Assert.AreEqual(200, reopened.Count);
                for (int i = 0; i < reopened.Count; ++i) Assert.AreEqual("updated " + i, reopened[i].Text);
            }
        }
        [Test]
        public void FailedSessionBatchKeepsEveryUnsealedEntryUntilClear()
        {
            using (var store = new MNNConversationTranscriptStore(_directory, maximumBytes: 128))
            {
                for (int i = 0; i < 64; ++i) store.Add(MNNChatRole.User, "kept " + i);
                Assert.Throws<InvalidOperationException>(() => store.Add(MNNChatRole.User, "overflow"));
                Assert.AreEqual(64, store.Count);
                for (int i = 0; i < 64; ++i) Assert.AreEqual("kept " + i, store[i].Text);
                store.Clear(); store.Add(MNNChatRole.User, "fresh"); Assert.AreEqual("fresh", store[0].Text);
            }
        }
        [Test]
        public void SessionBatchAppendDoesNotAllocatePerMessageJournalObjects()
        {
            using (var warm = new MNNConversationTranscriptStore(_directory))
                for (int i = 0; i < 65; ++i) warm.Add(MNNChatRole.User, "warm");
            using (var store = new MNNConversationTranscriptStore(_directory))
            {
                string text = "shared immutable transcript 🎙";
                long allocated = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 10000; ++i) store.Add(MNNChatRole.User, text);
                allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
                Assert.AreEqual(10000, store.Count); Assert.AreEqual(text, store[0].Text);
                Assert.Less(allocated, 256 * 1024, "Appending references must not allocate a writer/byte array per message.");
            }
        }
    }
}
