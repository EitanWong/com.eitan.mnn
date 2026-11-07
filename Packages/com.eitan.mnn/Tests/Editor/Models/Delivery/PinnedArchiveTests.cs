using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class PinnedArchiveTests
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.GetFullPath("TestArtifacts~/PinnedArchiveTests/" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        private string Archive(string path, params string[] names)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                foreach (string name in names)
                {
                    using (var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false)))
                        writer.Write(name.EndsWith("config.json", StringComparison.Ordinal) ? "{}" : "weights");
                }
            }
            return path;
        }

        private MNNModelAssetFile Required(string path, string content)
        {
            string temporary = Path.Combine(_root, "hash-input");
            File.WriteAllText(temporary, content, new UTF8Encoding(false));
            string hash = MNNModelDeliveryManifest.ComputeSha256(temporary);
            File.Delete(temporary);
            return new MNNModelAssetFile { path = path, url = "https://example.com/" + path, bytes = Encoding.UTF8.GetByteCount(content), sha256 = hash };
        }

        [Test]
        public void ManifestInstallsCustomArchiveNameAndExactNestedFilesWithoutNetwork()
        {
            string models = Path.Combine(_root, "Models");
            string archive = Archive(Path.Combine(models, "custom/payload/custom-model.zip"), "graphs/model.mnn", "tokenizer/config.json");
            var manifest = new MNNPinnedModelManifest
            {
                modelId = "custom",
                revision = "fixture",
                archive = new MNNModelAssetFile { path = "payload/custom-model.zip", url = "https://example.com/model.zip", bytes = new FileInfo(archive).Length, sha256 = MNNModelDeliveryManifest.ComputeSha256(archive) },
                files = new[] { Required("graphs/model.mnn", "weights"), Required("tokenizer/config.json", "{}") }
            };
            manifest.EnsureAsync(models, CancellationToken.None).GetAwaiter().GetResult();
            Assert.AreEqual("weights", File.ReadAllText(Path.Combine(models, "custom/graphs/model.mnn")));
            Assert.AreEqual("{}", File.ReadAllText(Path.Combine(models, "custom/tokenizer/config.json")));
            Assert.True(File.Exists(archive));
            manifest.EnsureAsync(models, CancellationToken.None).GetAwaiter().GetResult();
        }

        private MNNModelAssetFile[] Files() => new[] { Required("graphs/model.mnn", "weights"), Required("tokenizer/config.json", "{}") };
        private void Install(string archive, MNNModelAssetFile[] files = null, CancellationToken token = default, Action<long> extracted = null) =>
            MNNModelArchiveInstaller.Install(archive, MNNModelDeliveryManifest.ComputeSha256(archive), Path.Combine(_root, "Installed"), files ?? Files(), token, extracted);

        [Test]
        public void PublicArchiveContractRequiresPositiveFileSizes()
        {
            string archive = Archive(Path.Combine(_root, "empty.zip"), "graphs/model.mnn", "tokenizer/config.json");
            var files = Files();
            files[1] = Required("tokenizer/config.json", "");
            Assert.Throws<InvalidDataException>(() => Install(archive, files));
            Assert.False(Directory.Exists(Path.Combine(_root, "Installed")));
        }

        [TestCase("")]
        [TestCase("release/")]
        public void ExactNestedLayoutOrSingleWrapperIsPreserved(string prefix)
        {
            string archive = Archive(Path.Combine(_root, "custom.zip"), prefix + "graphs/model.mnn", prefix + "tokenizer/config.json");
            Install(archive);
            Install(archive);
            Assert.AreEqual("weights", File.ReadAllText(Path.Combine(_root, "Installed/graphs/model.mnn")));
            Assert.AreEqual("{}", File.ReadAllText(Path.Combine(_root, "Installed/tokenizer/config.json")));
            Assert.False(Directory.Exists(Path.Combine(_root, "Installed.mnn-extracting")));
            Assert.False(Directory.Exists(Path.Combine(_root, "Installed.mnn-previous")));
        }

        [TestCase("../model.mnn")]
        [TestCase("..\\model.mnn")]
        [TestCase("/model.mnn")]
        [TestCase("C:/model.mnn")]
        [TestCase("root/../model.mnn")]
        [TestCase("root//model.mnn")]
        [TestCase("root./model.mnn")]
        [TestCase("root /model.mnn")]
        [TestCase("root:stream/model.mnn")]
        public void UnsafeEntriesNeverPublish(string entry)
        {
            string archive = Archive(Path.Combine(_root, "unsafe.zip"), entry, "tokenizer/config.json");
            Assert.Throws<InvalidDataException>(() => Install(archive));
            Assert.False(Directory.Exists(Path.Combine(_root, "Installed")));
        }

        [TestCase("graphs/model.mnn", "graphs/MODEL.MNN", "tokenizer/config.json")]
        [TestCase("graphs/model.mnn", "graphs/model.mnn", "tokenizer/config.json")]
        [TestCase("graphs/model.mnn", "tokenizer/config.json", "unexpected")]
        [TestCase("graphs/model.mnn")]
        public void DuplicateUnexpectedAndMissingEntriesAreRejected(params string[] entries)
        {
            string archive = Archive(Path.Combine(_root, "invalid.zip"), entries);
            Assert.Throws<InvalidDataException>(() => Install(archive));
        }

        [Test]
        public void SymlinkEntriesAndExpandedByteBudgetAreRejected()
        {
            string archive = Archive(Path.Combine(_root, "link.zip"), "graphs/model.mnn", "tokenizer/config.json");
            using (var zip = ZipFile.Open(archive, ZipArchiveMode.Update)) zip.Entries[0].ExternalAttributes = unchecked((int)0xa1ff0000);
            Assert.Throws<InvalidDataException>(() => Install(archive));
            archive = Archive(Path.Combine(_root, "oversized.zip"), "graphs/model.mnn", "tokenizer/config.json");
            Assert.Throws<InvalidDataException>(() => MNNModelArchiveInstaller.InstallCore(archive,
                MNNModelDeliveryManifest.ComputeSha256(archive), Path.Combine(_root, "Installed"), Files(), CancellationToken.None, null, 8, 8, true));
        }

        [Test]
        public void FileChecksumFailurePreservesPreviousInstallation()
        {
            string archive = Archive(Path.Combine(_root, "hash.zip"), "graphs/model.mnn", "tokenizer/config.json");
            Directory.CreateDirectory(Path.Combine(_root, "Installed"));
            File.WriteAllText(Path.Combine(_root, "Installed/old"), "old");
            var files = Files(); files[0].sha256 = new string('0', 64);
            Assert.Throws<InvalidDataException>(() => Install(archive, files));
            Assert.AreEqual("old", File.ReadAllText(Path.Combine(_root, "Installed/old")));
            Assert.False(File.Exists(Path.Combine(_root, "Installed/graphs/model.mnn")));
        }

        [Test]
        public void CancelledExtractionKeepsPreviousFilesAndCanRetry()
        {
            string archive = Archive(Path.Combine(_root, "cancel.zip"), "graphs/model.mnn", "tokenizer/config.json");
            Directory.CreateDirectory(Path.Combine(_root, "Installed"));
            File.WriteAllText(Path.Combine(_root, "Installed/old"), "old");
            using (var cancel = new CancellationTokenSource())
                Assert.Throws<OperationCanceledException>(() => Install(archive, token: cancel.Token, extracted: _ => cancel.Cancel()));
            Assert.AreEqual("old", File.ReadAllText(Path.Combine(_root, "Installed/old")));
            Assert.False(Directory.Exists(Path.Combine(_root, "Installed.mnn-extracting")));
            Install(archive);
            Assert.False(File.Exists(Path.Combine(_root, "Installed/old")));
        }

        [Test]
        public void InterruptedDirectoryPublicationRestoresTheRetainedArchiveBeforeRetry()
        {
            string destination = Path.Combine(_root, "Installed");
            string previous = destination + ".mnn-previous";
            string extracting = destination + ".mnn-extracting";
            string archive = Archive(Path.Combine(extracting, "payload/custom.zip"), "graphs/model.mnn", "tokenizer/config.json");
            Directory.CreateDirectory(previous);
            File.WriteAllText(Path.Combine(previous, "old"), "old");
            string hash = MNNModelDeliveryManifest.ComputeSha256(archive);
            MNNModelArchiveInstaller.Install(Path.Combine(destination, "payload/custom.zip"), hash, destination, Files());
            Assert.AreEqual("weights", File.ReadAllText(Path.Combine(destination, "graphs/model.mnn")));
            Assert.True(File.Exists(Path.Combine(destination, "payload/custom.zip")));
            Assert.False(Directory.Exists(previous));
            Assert.False(Directory.Exists(extracting));
        }

        [Test]
        public void ArchiveChecksumFailureAndEntryBudgetNeverPublish()
        {
            string archive = Archive(Path.Combine(_root, "checksum.zip"), "graphs/model.mnn", "tokenizer/config.json");
            Assert.Throws<InvalidDataException>(() => MNNModelArchiveInstaller.Install(archive, new string('0', 64), Path.Combine(_root, "Installed"), Files()));
            Assert.Throws<InvalidDataException>(() => MNNModelArchiveInstaller.InstallCore(archive,
                MNNModelDeliveryManifest.ComputeSha256(archive), Path.Combine(_root, "Installed"), Files(), CancellationToken.None, null, 1, 1024, true));
            Assert.False(Directory.Exists(Path.Combine(_root, "Installed")));
        }

        [Test]
        public void ManifestExplicitArchiveDirectoryPreservesCosyVoiceLayout()
        {
            string models = Path.Combine(_root, "Models");
            string archive = Archive(Path.Combine(models, "custom/bundle.zip"), "release/graphs/model.mnn", "release/tokenizer/config.json");
            var manifest = new MNNPinnedModelManifest
            {
                modelId = "custom",
                revision = "fixture",
                archiveDirectory = "model",
                archive = new MNNModelAssetFile { path = "bundle.zip", url = "https://example.com/bundle.zip", bytes = new FileInfo(archive).Length, sha256 = MNNModelDeliveryManifest.ComputeSha256(archive) },
                files = new[] { Required("model/graphs/model.mnn", "weights"), Required("model/tokenizer/config.json", "{}") }
            };
            manifest.EnsureAsync(models, CancellationToken.None).GetAwaiter().GetResult();
            Assert.AreEqual("weights", File.ReadAllText(Path.Combine(models, "custom/model/graphs/model.mnn")));
            Assert.True(File.Exists(archive));
        }
    }
}
