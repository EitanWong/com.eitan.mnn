using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public class CosyVoiceInstallerTests
    {
        private string _root, _archive;
        private readonly Dictionary<string, long> _required = new Dictionary<string, long>{{"llm.mnn", 7}, {"config.json", 2}};
        [SetUp]
        public void SetUp()
        {
            _required["llm.mnn"] = 7;
            _required["config.json"] = 2;
            _root = Path.GetFullPath(Path.Combine("TestArtifacts~/CosyVoiceInstallerTests", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(_root);
            _archive = Path.Combine(_root, MNNCosyVoiceModel.Archive);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, true);
        }

        private void Create(params string[] names)
        {
            using (var zip = ZipFile.Open(_archive, ZipArchiveMode.Create))
                foreach (string name in names)
                    using (var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false)))
                        writer.Write(name.EndsWith("config.json", StringComparison.Ordinal) ? "{}" : "weights");
        }

        private void Install(CancellationToken token = default, Action<long> extracted = null) => MNNCosyVoiceInstaller.InstallArchive(_root, MNNCosyVoiceInstaller.Hash(_archive), _required, token, extracted);
        [TestCase("")]
        [TestCase("release/")]
        public void CompleteFlatOrWrappedArchiveIsInstalledAndCanRetry(string prefix)
        {
            Create(prefix + "llm.mnn", prefix + "config.json");
            Install();
            Install();
            Assert.AreEqual("weights", File.ReadAllText(Path.Combine(_root, "model/llm.mnn")));
            Assert.AreEqual("{}", File.ReadAllText(Path.Combine(_root, "model/config.json")));
            Assert.IsTrue(File.Exists(_archive), "Retained until the whole download job publishes.");
            Assert.IsFalse(Directory.Exists(Path.Combine(_root, ".cosyvoice-extracting")));
        }

        [TestCase("")]
        [TestCase("release/")]
        public void DeclaredEmptyFilesAreInstalledAndCanRetry(string prefix)
        {
            _required["config.json"] = 0;
            using (var zip = ZipFile.Open(_archive, ZipArchiveMode.Create))
            {
                using (var writer = new StreamWriter(zip.CreateEntry(prefix + "llm.mnn").Open(), new UTF8Encoding(false)))
                    writer.Write("weights");
                zip.CreateEntry(prefix + "config.json");
            }
            Install();
            Install();
            Assert.AreEqual("weights", File.ReadAllText(Path.Combine(_root, "model/llm.mnn")));
            Assert.AreEqual(0, new FileInfo(Path.Combine(_root, "model/config.json")).Length);
            Assert.IsTrue(File.Exists(_archive));
        }

        [TestCase("../llm.mnn")]
        [TestCase("..\\llm.mnn")]
        [TestCase("/llm.mnn")]
        [TestCase("C:/llm.mnn")]
        [TestCase("root/../llm.mnn")]
        [TestCase("root//llm.mnn")]
        [TestCase("root./llm.mnn")]
        [TestCase("root /llm.mnn")]
        [TestCase("root:stream/llm.mnn")]
        [TestCase("./llm.mnn")]
        public void UnsafePathsNeverEscapeOrPublish(string path)
        {
            Create(path, "config.json");
            Assert.Throws<InvalidDataException>(() => Install());
            Assert.IsFalse(Directory.Exists(Path.Combine(_root, "model")));
            Assert.IsFalse(Directory.Exists(Path.Combine(_root, ".cosyvoice-extracting")));
        }

        [TestCase("llm.mnn", "LLM.MNN", "config.json")]
        [TestCase("llm.mnn", "llm.mnn", "config.json")]
        [TestCase("a/llm.mnn", "b/config.json")]
        [TestCase("llm.mnn", "nested/config.json")]
        [TestCase("llm.mnn", "config.json", "extra")]
        [TestCase("llm.mnn")]
        public void MissingDuplicateOrUnexpectedFilesAreRejected(params string[] names)
        {
            Create(names);
            Assert.Throws<InvalidDataException>(() => Install());
        }

        [TestCase("release/")]
        [TestCase("release/", "RELEASE/")]
        [TestCase("release//")]
        [TestCase("release/llm.mnn/")]
        [TestCase("unused/")]
        public void DirectoryEntriesCannotHideDataDuplicatesOrUnexpectedPaths(params string[] directories)
        {
            Create("release/llm.mnn", "release/config.json");
            using (var zip = ZipFile.Open(_archive, ZipArchiveMode.Update))
                foreach (string path in directories)
                    zip.CreateEntry(path);
            if (directories.Length == 1 && directories[0] == "release/")
                Install();
            else
                Assert.Throws<InvalidDataException>(() => Install());
        }

        [Test]
        public void WrongSizeIsRejectedBeforePublishing()
        {
            Create("llm.mnn", "config.json");
            _required["llm.mnn"] = 8;
            Assert.Throws<InvalidDataException>(() => Install());
        }

        [Test]
        public void ChecksumMismatchPreservesPreviousStagedModel()
        {
            Create("llm.mnn", "config.json");
            Directory.CreateDirectory(Path.Combine(_root, "model"));
            File.WriteAllText(Path.Combine(_root, "model/old"), "old");
            Assert.Throws<InvalidDataException>(() => MNNCosyVoiceInstaller.InstallArchive(_root, new string ('0', 64), _required));
            Assert.AreEqual("old", File.ReadAllText(Path.Combine(_root, "model/old")));
        }

        [Test]
        public void CorruptArchiveIsRejected()
        {
            File.WriteAllText(_archive, "not a zip");
            Assert.Throws<InvalidDataException>(() => Install());
            Assert.IsFalse(Directory.Exists(Path.Combine(_root, "model")));
        }

        [Test]
        public void UnixSymlinkEntryIsRejected()
        {
            Create("llm.mnn", "config.json");
            using (var zip = ZipFile.Open(_archive, ZipArchiveMode.Update))
                zip.Entries[0].ExternalAttributes = unchecked((int)0xa1ff0000);
            Assert.Throws<InvalidDataException>(() => Install());
        }

        [Test]
        public void TooManyEntriesAreRejected()
        {
            Create(Enumerable.Range(0, 257).Select(i => i.ToString()).ToArray());
            Assert.Throws<InvalidDataException>(() => Install());
        }

        [Test]
        public void CancellationDuringExtractionCleansPartialFilesAndRetrySucceeds()
        {
            Create("llm.mnn", "config.json");
            Directory.CreateDirectory(Path.Combine(_root, "model"));
            File.WriteAllText(Path.Combine(_root, "model/old"), "old");
            using (var cancellation = new CancellationTokenSource())
                Assert.Throws<OperationCanceledException>(() => Install(cancellation.Token, _ => cancellation.Cancel()));
            Assert.AreEqual("old", File.ReadAllText(Path.Combine(_root, "model/old")));
            Assert.IsFalse(Directory.Exists(Path.Combine(_root, ".cosyvoice-extracting")));
            Install();
            Assert.IsTrue(File.Exists(Path.Combine(_root, "model/llm.mnn")));
            Assert.IsFalse(File.Exists(Path.Combine(_root, "model/old")));
        }

        [Test]
        public void AlreadyCancelledAndMissingArchivesFailWithoutPublishing()
        {
            Assert.Throws<FileNotFoundException>(() => MNNCosyVoiceInstaller.Install(_root, default));
            using (var cancelled = new CancellationTokenSource())
            {
                cancelled.Cancel();
                Assert.Throws<OperationCanceledException>(() => MNNCosyVoiceInstaller.Install(_root, cancelled.Token));
            }
        }

        [Test]
        public void OtherRepositoriesDoNotExtractArchives()
        {
            Create("../llm.mnn");
            MNNModelDownloadTasks.InstallDownloadedArchives(_root, MNNModelProvider.HuggingFace, "other/model");
            MNNModelDownloadTasks.InstallDownloadedArchives(_root, MNNModelProvider.ModelScope, MNNCosyVoiceModel.Repository);
            Assert.IsTrue(File.Exists(_archive));
            Assert.IsFalse(Directory.Exists(Path.Combine(_root, "model")));
        }

        [Test]
        public void ReleaseContractIncludesManifestItself()
        {
            Assert.AreEqual(18, MNNCosyVoiceModel.RequiredFiles.Length);
            Assert.AreEqual(MNNCosyVoiceModel.RequiredFiles.Length, MNNCosyVoiceModel.RequiredSizes.Length);
            CollectionAssert.Contains(MNNCosyVoiceModel.RequiredFiles, "manifest.json");
        }
    }
}
