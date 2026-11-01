using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MNN.Unity.Tests
{
    public class ModelPackagingTests
    {
        private string _root;
        private string Models => Path.Combine(_root, "StreamingAssets/MNN/Models");
        private string Streaming => Path.Combine(_root, "StreamingAssets");
        private string Cache => Path.Combine(_root, "Cache");
        private string Work => Path.Combine(_root, "Staging");

        [SetUp]
        public void SetUp()
        {
            _root = Path.GetFullPath("TestArtifacts~/ModelPackagingTests/" + Guid.NewGuid().ToString("N"));
            Write("small/model.mnn", "weights");
            Write("small/tokenizer/tokens.txt", "tokens");
            Write("large/model.mnn", "large weights");
            Write("small/model.mnn.meta", "guid: fixture");
            Write("small/download.mnn-parts/0.part", "partial");
            Write("small/.cache/state", "cache");
            File.WriteAllText(Models + ".meta", "original Models GUID");
        }

        [TearDown]
        public void TearDown() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

        private void Write(string relative, string content)
        {
            var path = Path.Combine(Models, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, content);
        }

        private MNNModelDeliveryManifest Manifest(params string[] paths) => new MNNModelDeliveryManifest
        {
            files = paths.Select(path => new MNNModelDeliveryFile
            {
                path = path, bundled = true, bytes = new FileInfo(Path.Combine(Models, path)).Length,
                sha256 = MNNModelDeliveryManifest.ComputeSha256(Path.Combine(Models, path))
            }).ToArray()
        };

        [Test]
        public void DefaultPackagingModeExcludesAllLocalModels()
        {
            Assert.IsEmpty(MNNModelBuildPlan.Create(Models, default, null).IncludedFiles);
            Assert.IsTrue(File.Exists(Path.Combine(Models, "large/model.mnn")));
        }

        [Test]
        public void SelectedDirectoryIncludesDependenciesAndDeduplicatesOverlappingRules()
        {
            var plan = MNNModelBuildPlan.Create(Models, MNNModelPackagingMode.Selected,
                new[] { "small", "small/model.mnn", "small" });
            CollectionAssert.AreEqual(new[] { "small/model.mnn", "small/tokenizer/tokens.txt" }, plan.IncludedFiles);
            Assert.AreEqual(13, plan.IncludedBytes);
        }

        [Test]
        public void SelectedFileDoesNotImplicitlyShipOtherModels()
        {
            CollectionAssert.AreEqual(new[] { "small/model.mnn" },
                MNNModelBuildPlan.Create(Models, MNNModelPackagingMode.Selected, new[] { "small/model.mnn" }).IncludedFiles);
        }

        [Test]
        public void AllExcludesUnityMetadataAndIncompleteDownloads()
        {
            Assert.AreEqual(3, MNNModelBuildPlan.Create(Models, MNNModelPackagingMode.All, null).IncludedFiles.Length);
        }

        [TestCase("../outside")]
        [TestCase("/absolute")]
        [TestCase("small/../large")]
        [TestCase("small\\model.mnn")]
        [TestCase("")]
        public void UnsafeSelectionsAreRejected(string path)
        {
            Assert.Throws<ArgumentException>(() => MNNModelBuildPlan.Create(Models, MNNModelPackagingMode.Selected, new[] { path }));
        }

        [Test]
        public void MissingSelectionFailsInsteadOfSilentlyOmittingModel()
        {
            Assert.Throws<FileNotFoundException>(() => MNNModelBuildPlan.Create(Models, MNNModelPackagingMode.Selected, new[] { "missing" }));
        }

        [Test]
        public void StagingRestoresAllFilesAndOriginalGuidAfterInterruptedBuild()
        {
            var staging = new MNNModelBuildStaging(Models, Work);
            staging.Begin(Manifest("small/model.mnn"));
            Assert.IsFalse(Directory.Exists(Models));
            Assert.IsFalse(File.Exists(Models + ".meta"));
            Assert.AreEqual("weights", File.ReadAllText(staging.SourceFile("small/model.mnn")));
            // Simulate a domain reload: recovery uses only the on-disk journal.
            new MNNModelBuildStaging(Models, Work).Restore();
            Assert.AreEqual("large weights", File.ReadAllText(Path.Combine(Models, "large/model.mnn")));
            Assert.AreEqual("original Models GUID", File.ReadAllText(Models + ".meta"));
            Assert.IsFalse(staging.HasTransaction);
            staging.Restore();
        }

        [Test]
        public void RecoveryDoesNotOverwriteFilesRecreatedDuringBuild()
        {
            var staging = new MNNModelBuildStaging(Models, Work);
            staging.Begin(Manifest());
            Write("new.mnn", "user edit");
            Assert.Throws<IOException>(() => staging.Restore());
            Assert.AreEqual("user edit", File.ReadAllText(Path.Combine(Models, "new.mnn")));
            Assert.IsTrue(staging.HasTransaction);
            Directory.Delete(Models, true);
            staging.Restore();
        }

        [Test]
        public void NewBuildCannotOverwritePendingRecovery()
        {
            var staging = new MNNModelBuildStaging(Models, Work);
            staging.Begin(Manifest());
            Assert.Throws<IOException>(() => new MNNModelBuildStaging(Models, Work).Begin(Manifest()));
            staging.Restore();
        }

        [Test]
        public void ManifestRejectsDuplicateCaseVariantsAndFutureSchemas()
        {
            var manifest = Manifest("small/model.mnn", "small/model.mnn");
            manifest.files[1].path = "SMALL/model.mnn";
            Assert.Throws<InvalidDataException>(() => manifest.Validate());
            manifest = Manifest();
            manifest.schemaVersion = 2;
            Assert.Throws<InvalidDataException>(() => manifest.Validate());
        }

        [Test]
        public void RemoteManifestOnlyRequiresCatalogFieldsAndCannotClaimBundledFiles()
        {
            var hash = MNNModelDeliveryManifest.ComputeSha256(Path.Combine(Models, "small/model.mnn"));
            var remote = MNNModelBuildPlan.ReadRemoteManifest("{\"schemaVersion\":1,\"files\":[{" +
                "\"path\":\"small/model.mnn\",\"url\":\"https://example.invalid/model.mnn\",\"bytes\":7," +
                "\"sha256\":\"" + hash + "\",\"bundled\":true}]}");
            Assert.IsFalse(remote.files[0].bundled);
            Assert.AreEqual(1, remote.files.Length);
            Assert.Throws<InvalidDataException>(() => MNNModelBuildPlan.ReadRemoteManifest("{\"schemaVersion\":99,\"files\":[]}"));
        }

        [UnityTest]
        public IEnumerator DirectoryPreparationCopiesAllDependenciesAndRepairsStaleCache()
        {
            var delivery = new MNNModelDelivery(Manifest("small/model.mnn", "small/tokenizer/tokens.txt"), Streaming, Cache);
            var task = delivery.PrepareDirectoryAsync("small");
            while (!task.IsCompleted) yield return null;
            Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
            Assert.AreEqual("tokens", File.ReadAllText(Path.Combine(task.Result, "tokenizer/tokens.txt")));
            File.WriteAllText(Path.Combine(task.Result, "model.mnn"), "corrupt");
            var repair = delivery.PrepareDirectoryAsync("small");
            while (!repair.IsCompleted) yield return null;
            Assert.IsFalse(repair.IsFaulted, repair.Exception?.ToString());
            Assert.AreEqual("weights", File.ReadAllText(Path.Combine(task.Result, "model.mnn")));
        }

        [UnityTest]
        public IEnumerator AutomaticStorageReturnsReadableBundleWithoutCopying()
        {
            var delivery = new MNNModelDelivery(Manifest("small/model.mnn"), Streaming, Cache);
            var task = delivery.PrepareFileAsync("small/model.mnn");
            while (!task.IsCompleted) yield return null;
            Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
            Assert.AreEqual(Path.Combine(Models, "small/model.mnn"), task.Result);
            Assert.IsFalse(Directory.Exists(Cache));
        }

        [UnityTest]
        public IEnumerator UrlStreamingAssetsUsesBoundedFileDownloadAndEscapesPath()
        {
            Write("模型 with space/model.mnn", "url weights");
            var delivery = new MNNModelDelivery(Manifest("模型 with space/model.mnn"), new Uri(Streaming + "/").AbsoluteUri, Cache);
            var task = delivery.PrepareFileAsync("模型 with space/model.mnn");
            while (!task.IsCompleted) yield return null;
            Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
            Assert.AreEqual("url weights", File.ReadAllText(task.Result));
            StringAssert.StartsWith(Cache, task.Result);
        }

        [UnityTest]
        public IEnumerator UrlModelDirectoryPreparesAndDiscoversNestedPiperData()
        {
            // Exercise the URL transport used by APK/browser StreamingAssets
            // without assuming that the source can be enumerated as a directory.
            const string relative = "Piper with space/assets/share/espeak-ng-data";
            var files = MNNPiper.RequiredDataFiles.Select(file => relative + "/" + file).ToArray();
            foreach (string file in files) Write(file, "fixture");
            var delivery = new MNNModelDelivery(Manifest(files), new Uri(Streaming + "/").AbsoluteUri, Cache);
            var task = delivery.PrepareDirectoryAsync("Piper with space");
            while (!task.IsCompleted) yield return null;
            Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
            Assert.AreEqual(Path.Combine(Cache, relative), MNNPiper.ResolveDataDirectory(task.Result));
            Directory.Delete(Path.Combine(Models, "Piper with space"), true);
            var offline = delivery.PrepareDirectoryAsync("Piper with space");
            while (!offline.IsCompleted) yield return null;
            Assert.IsFalse(offline.IsFaulted, offline.Exception?.ToString());
            Assert.AreEqual(task.Result, offline.Result, "Verified dependency files must be reused without fetching the URL again.");
        }

        [UnityTest]
        public IEnumerator CorruptBundleNeverPublishesPartialModel()
        {
            var manifest = Manifest("small/model.mnn");
            manifest.bundledStorage = MNNBundledModelStorage.PersistentData;
            Write("small/model.mnn", "corrupt");
            var delivery = new MNNModelDelivery(manifest, Streaming, Cache);
            var task = delivery.PrepareFileAsync("small/model.mnn");
            while (!task.IsCompleted) yield return null;
            Assert.IsInstanceOf<InvalidDataException>(task.Exception?.GetBaseException());
            Assert.IsEmpty(Directory.GetFiles(Cache, "*", SearchOption.AllDirectories));
        }

        [UnityTest]
        public IEnumerator DownloadsDisabledFailsWithoutMakingNetworkRequest()
        {
            var manifest = Manifest("small/model.mnn");
            manifest.allowDownloads = false;
            manifest.files[0].bundled = false;
            manifest.files[0].url = "https://example.invalid/model.mnn";
            var delivery = new MNNModelDelivery(manifest, Streaming, Cache);
            var task = delivery.PrepareFileAsync("small/model.mnn");
            while (!task.IsCompleted) yield return null;
            Assert.IsInstanceOf<InvalidOperationException>(task.Exception?.GetBaseException());
            Assert.IsFalse(Directory.Exists(Cache));
        }

        [UnityTest]
        public IEnumerator VerifiedRemoteCacheWorksOfflineWithDownloadsDisabled()
        {
            var manifest = Manifest("small/model.mnn");
            manifest.allowDownloads = false;
            manifest.files[0].bundled = false;
            manifest.files[0].url = "https://example.invalid/model.mnn";
            Directory.CreateDirectory(Path.Combine(Cache, "small"));
            File.Copy(Path.Combine(Models, "small/model.mnn"), Path.Combine(Cache, "small/model.mnn"));
            var task = new MNNModelDelivery(manifest, Streaming, Cache).PrepareFileAsync("small/model.mnn");
            while (!task.IsCompleted) yield return null;
            Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
            Assert.AreEqual(Path.Combine(Cache, "small/model.mnn"), task.Result);
        }

        [UnityTest]
        public IEnumerator CancelledPreparationLeavesNoFiles()
        {
            var delivery = new MNNModelDelivery(Manifest("small/model.mnn"), Streaming, Cache);
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                var task = delivery.PrepareFileAsync("small/model.mnn", token: cancellation.Token);
                while (!task.IsCompleted) yield return null;
                Assert.IsTrue(task.IsCanceled);
                Assert.IsFalse(Directory.Exists(Cache));
            }
        }
    }
}
