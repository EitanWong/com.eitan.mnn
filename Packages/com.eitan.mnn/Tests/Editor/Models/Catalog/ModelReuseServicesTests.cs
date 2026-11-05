using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class ModelReuseServicesTests
    {
        private static void Run(Func<Task> action) => Task.Run(action).GetAwaiter().GetResult();
        private string _root;
        private static MNNRemoteModel Model(string name = "chat") => new MNNRemoteModel
        {
            provider = MNNModelSource.HuggingFace, repositoryId = "publisher/" + name,
            isRepository = true, category = "LargeLanguageModel", tags = new[] { "original" },
            inputSpec = new MNNModelInputSpec { shape = new[] { 1, 32 } }
        };

        [SetUp] public void SetUp()
        {
            _root = Path.GetFullPath("TestArtifacts~/ModelReuse/" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }
        [TearDown] public void TearDown() => Directory.Delete(_root, true);

        [Test] public void IdentityNormalizesRepositoryAndDefaultRevisionButKeepsSourceAndRevisionDistinct()
        {
            var a = Model();
            var b = Model("CHAT"); b.revision = "main";
            Assert.AreEqual(MNNModelRepositoryIdentity.FromModel(a), MNNModelRepositoryIdentity.FromModel(b));
            b.revision = "release";
            Assert.AreNotEqual(MNNModelRepositoryIdentity.FromModel(a), MNNModelRepositoryIdentity.FromModel(b));
            a.repositoryId = "../escape";
            Assert.False(MNNModelRepositoryIdentity.TryCreate(a, out _));
        }

        [Test] public void SharedUiCacheLoadsDiskInBackgroundBeforePublishingReady() => Run(async () =>
        {
            string path = Path.Combine(_root, "ready.json");
            await new MNNOnlineModelCache(path, _ => Task.FromResult(new MNNOnlineModelCache.Discovery
                { Succeeded = true, Models = new[] { Model("ready") } })).LoadAsync();
            var cache = MNNOnlineModelCache.ForPath(path);
            await cache.Ready;
            Assert.AreEqual("publisher/ready", cache.Read().models.Single().repositoryId);
            Assert.AreSame(cache, MNNOnlineModelCache.ForPath(path));
        });

        [Test] public void CacheOwnsDiscoveryAndPublishedNestedSnapshots() => Run(async () =>
        {
            var remote = Model();
            var cache = new MNNOnlineModelCache(Path.Combine(_root, "cache.json"), _ => Task.FromResult(
                new MNNOnlineModelCache.Discovery { Succeeded = true, Models = new[] { remote } }));
            var first = await cache.LoadAsync();
            remote.tags[0] = "caller"; remote.inputSpec.shape[0] = 9;
            first.models[0].tags[0] = "reader"; first.models[0].inputSpec.shape[0] = 8;
            var again = cache.Read();
            Assert.AreEqual("original", again.models[0].tags[0]);
            Assert.AreEqual(1, again.models[0].inputSpec.shape[0]);
            Assert.AreEqual("original", new MNNOnlineModelCache(Path.Combine(_root, "cache.json")).Read().models[0].tags[0]);
        });

        [Test] public void CancelledCacheWaiterDoesNotCancelSharedDiscovery() => Run(async () =>
        {
            int calls = 0;
            var response = new TaskCompletionSource<MNNOnlineModelCache.Discovery>(TaskCreationOptions.RunContinuationsAsynchronously);
            var cache = new MNNOnlineModelCache(Path.Combine(_root, "cache.json"), _ => { ++calls; return response.Task; });
            using (var cancelled = new CancellationTokenSource())
            {
                var first = cache.LoadAsync(token: cancelled.Token);
                var second = cache.LoadAsync();
                cancelled.Cancel();
                Assert.Throws<OperationCanceledException>(() => first.GetAwaiter().GetResult());
                response.SetResult(new MNNOnlineModelCache.Discovery { Succeeded = true, Models = new[] { Model() } });
                Assert.AreEqual(1, (await second).models.Length);
                Assert.AreEqual(1, calls);
            }
        });

        [Test] public void CacheMergesLargePagesByRepositoryIdentityAndPreservesUnknownMetadata() => Run(async () =>
        {
            var first = Enumerable.Range(0, 10000).Select(i => Model("model" + i)).ToArray();
            var update = Model("MODEL8"); update.runtimeAvailable = false; update.fileSize = 42;
            var cache = new MNNOnlineModelCache(Path.Combine(_root, "cache.json"), _ => Task.FromResult(
                new MNNOnlineModelCache.Discovery { Succeeded = true, Models = first, ModelScopeNextPage = 2 }),
                (_, __) => Task.FromResult(new MNNOnlineModelCache.Discovery { Succeeded = true, Models = new[] { update } }));
            await cache.LoadAsync();
            var result = await cache.LoadMoreAsync();
            Assert.AreEqual(10000, result.models.Length);
            var changed = result.models.Single(m => m.repositoryId.EndsWith("MODEL8", StringComparison.Ordinal));
            Assert.False(changed.runtimeAvailable); Assert.AreEqual(42, changed.fileSize);
        });

        [Test] public void InstallationDetectsSameSizeCorruptionAndRebuildsInsteadOfAdoptingIt() => Run(async () =>
        {
            var model = Model(); string directory = Path.Combine(_root, "model"); int writes = 0;
            Task Download(CancellationToken token)
            {
                ++writes; Directory.CreateDirectory(directory); File.WriteAllBytes(Path.Combine(directory, "graph.mnn"), new byte[] { 1, 2, 3, 4 });
                return Task.CompletedTask;
            }
            await MNNModelInstallation.EnsureAsync(directory, model, download: Download);
            Assert.True(MNNModelInstallation.IsInstalled(directory, model));
            File.WriteAllBytes(Path.Combine(directory, "graph.mnn"), new byte[] { 4, 3, 2, 1 });
            Assert.False(MNNModelInstallation.IsInstalled(directory, model));
            Assert.True(MNNModelInstallation.HasRecordedFiles(directory, model), "A cheap UI hint must remain distinct from integrity proof.");
            await MNNModelInstallation.EnsureAsync(directory, model, download: Download, canAdoptLocal: () => true);
            Assert.AreEqual(2, writes, "An invalid receipt must prevent local-file adoption.");
            Assert.True(MNNModelInstallation.IsInstalled(directory, model));
        });

        [Test] public void ConcurrentInstallationsUseOneWriterAndCancelledWaiterDoesNotSplitTheGate() => Run(async () =>
        {
            var model = Model(); var directory = Path.Combine(_root, "model");
            var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var finish = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            int writers = 0;
            async Task Download(CancellationToken token)
            {
                Interlocked.Increment(ref writers); started.TrySetResult(true); await finish.Task;
                Directory.CreateDirectory(directory); File.WriteAllText(Path.Combine(directory, "graph.mnn"), "weights");
            }
            var first = MNNModelInstallation.EnsureAsync(directory, model, download: Download);
            await started.Task;
            using (var cancel = new CancellationTokenSource())
            {
                var second = MNNModelInstallation.EnsureAsync(directory, model, token: cancel.Token, download: Download);
                cancel.Cancel(); Assert.Catch<OperationCanceledException>(() => second.GetAwaiter().GetResult());
                var third = MNNModelInstallation.EnsureAsync(directory, model, download: Download);
                finish.SetResult(true);
                await Task.WhenAll(first, third);
            }
            Assert.AreEqual(1, writers);
            Assert.AreEqual(0, MNNModelPathGate.ActivePathCount);
        });

        [Test] public void FailedInstallationLeavesNoReceiptAndCanRetry() => Run(async () =>
        {
            string directory = Path.Combine(_root, "failed");
            Task Fail(CancellationToken token)
            {
                Directory.CreateDirectory(directory); File.WriteAllText(Path.Combine(directory, "unfinished.part"), "partial");
                return Task.FromException(new IOException("disk or network failure"));
            }
            Assert.Throws<IOException>(() => MNNModelInstallation.EnsureAsync(directory, Model(), download: Fail).GetAwaiter().GetResult());
            Assert.False(MNNModelInstallation.HasReceipt(directory));
            await MNNModelInstallation.EnsureAsync(directory, Model(), download: token =>
            { File.WriteAllText(Path.Combine(directory, "graph.mnn"), "complete"); return Task.CompletedTask; });
            Assert.True(MNNModelInstallation.IsInstalled(directory, Model()));
            Assert.AreEqual(0, MNNModelPathGate.ActivePathCount);
        });

        [Test] public void PinnedManifestCapturesFieldsBeforeAnyAwaitAndReusesVerifiedFiles() => Run(async () =>
        {
            string directory = Path.Combine(_root, "pinned"); Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "graph.mnn"), "data");
            var file = new MNNModelAssetFile { path = "graph.mnn", bytes = 4,
                sha256 = MNNModelDeliveryManifest.ComputeSha256(Path.Combine(directory, "graph.mnn")), url = "https://example.invalid/graph.mnn" };
            var manifest = new MNNPinnedModelManifest { modelId = "pinned", revision = "exact", files = new[] { file } };
            var install = manifest.CaptureInstaller(_root);
            file.sha256 = new string('0', 64); file.path = "../../escaped";
            await install(CancellationToken.None);
            Assert.AreEqual("data", File.ReadAllText(Path.Combine(directory, "graph.mnn")));
            Assert.AreEqual(0, MNNModelPathGate.ActivePathCount);
        });

        [Test] public void CatalogEntriesCannotMutateTheInternalIdentityIndexOrNestedMetadata()
        {
            var catalog = new MNNConversationModelCatalog(); var remote = Model("detached");
            var entry = catalog.AddCatalogModel(remote); entry.modelId = "changed"; entry.remote.tags[0] = "changed";
            var again = catalog.AddCatalogModel(remote);
            Assert.AreNotEqual("changed", again.modelId);
            Assert.AreEqual("original", again.remote.tags[0]);
            Assert.AreEqual(1, catalog.Entries.Count(e => e.repository == "publisher/detached"));
        }

        [Test] public void DirectorySeparatorAliasesCannotAcquireTwoInstallationWriters() => Run(async () =>
        {
            string directory = Path.Combine(_root, "alias");
            var first = await MNNModelPathGate.EnterAsync(directory, CancellationToken.None);
            Task<IDisposable> second = null;
            try
            {
                second = MNNModelPathGate.EnterAsync(directory + Path.DirectorySeparatorChar, CancellationToken.None);
                Assert.False(second.IsCompleted, "Both names identify the same installation directory.");
                Assert.AreEqual(1, MNNModelPathGate.ActivePathCount);
            }
            finally
            {
                first.Dispose();
                if (second != null) (await second).Dispose();
            }
            Assert.AreEqual(0, MNNModelPathGate.ActivePathCount);
        });

        [Test] public void CancellationWhileWaitingForInstallationReportsItsTerminalState() => Run(async () =>
        {
            string directory = Path.Combine(_root, "waiting");
            using (await MNNModelPathGate.EnterAsync(directory, CancellationToken.None))
            using (var cancel = new CancellationTokenSource())
            {
                var phase = MNNModelInstallation.Phase.Waiting;
                var waiting = MNNModelInstallation.EnsureAsync(directory, Model(), token: cancel.Token,
                    state: (value, _) => phase = value);
                cancel.Cancel();
                Assert.Catch<OperationCanceledException>(() => waiting.GetAwaiter().GetResult());
                Assert.AreEqual(MNNModelInstallation.Phase.Cancelled, phase);
            }
        });

        [Test] public void ConcurrentCatalogUpdatesPublishEveryEntryAndDetachedWarnings()
        {
            var catalog = new MNNConversationModelCatalog();
            int before = catalog.Entries.Count;
            Parallel.For(0, 2000, i =>
            {
                catalog.AddCatalogModel(Model("concurrent" + i));
                if (i % 32 == 0) Assert.That(catalog.Entries.Count, Is.GreaterThanOrEqualTo(before));
            });
            Assert.AreEqual(before + 2000, catalog.Entries.Count);
            catalog.ApplyOnlineModels(Array.Empty<MNNRemoteModel>(), new[] { "first" });
            var oldWarnings = catalog.DiscoveryWarnings;
            catalog.ApplyOnlineModels(Array.Empty<MNNRemoteModel>(), new[] { "second" });
            Assert.AreEqual("first", oldWarnings.Single());
            Assert.AreEqual("second", catalog.DiscoveryWarnings.Single());
        }

        [Test] public void MalformedSuccessfulDiscoveryCompletesWithAnErrorAndAllowsRetry() => Run(async () =>
        {
            int attempts = 0;
            var cache = new MNNOnlineModelCache(Path.Combine(_root, "malformed.json"), _ =>
                Task.FromResult(new MNNOnlineModelCache.Discovery
                { Succeeded = true, Models = ++attempts == 1 ? null : new[] { Model() } }));
            var failed = cache.LoadAsync();
            Assert.Throws<InvalidDataException>(() => failed.GetAwaiter().GetResult());
            Assert.AreEqual(1, (await cache.LoadAsync(true)).models.Length);
            Assert.AreEqual(2, attempts);
        });

        [Test] public void TransferOptionsAreSnapshotsAndRejectInvalidBudgets()
        {
            var original = new MNNModelTransferOptions { MaxConnections = 2, RetryCount = 1 };
            var snapshot = original.Snapshot(); original.MaxConnections = 16;
            Assert.AreEqual(2, snapshot.MaxConnections);
            original.RetryCount = -1;
            Assert.Throws<ArgumentOutOfRangeException>(() => original.Snapshot());
        }
    }
}
