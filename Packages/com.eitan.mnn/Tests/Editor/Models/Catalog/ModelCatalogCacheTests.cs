using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using UnityEngine;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public class ModelCatalogCacheTests
    {
        private string _directory;
        private DateTime _now;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.GetFullPath("TestArtifacts~/ModelCatalog/CacheTests/" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            _now = new DateTime(2026, 10, 18, 0, 0, 0, DateTimeKind.Utc);
        }

        [TearDown]
        public void TearDown() => Directory.Delete(_directory, true);

        [UnityEngine.TestTools.UnityTest]
        public IEnumerator MemoryAndDiskHitsAvoidDiscoveryAndDoNotShareInstallationOrNestedState()
        {
            int calls = 0;
            Func<MNNModelProvider, Task<MNNModelRepository>> discover = provider => Task.FromResult(Repository(provider, "model-" + ++calls));
            var cache = new MNNModelCatalogCache(_directory, discover, () => _now);
            var first = cache.LoadAsync(MNNModelProvider.ModelScope);
            yield return Wait(first);
            first.Result.models[0].isInstalled = true;
            first.Result.models[0].localPath = "local-project";
            first.Result.models[0].tags[0] = "modified";
            first.Result.models[0].tasks[0] = "modified";
            first.Result.models[0].mnnFiles[0] = "modified";
            first.Result.models[0].inputSpec.shape[0] = 999;
            first.Result.models[0].outputSpec.shape[0] = 999;
            var memory = cache.LoadAsync(MNNModelProvider.ModelScope);
            yield return Wait(memory);
            Assert.AreEqual(1, calls);
            Assert.False(memory.Result.models[0].isInstalled);
            Assert.IsNull(memory.Result.models[0].localPath);
            Assert.AreEqual("mnn", memory.Result.models[0].tags[0]);
            Assert.AreEqual("text-generation", memory.Result.models[0].tasks[0]);
            Assert.AreEqual("model.mnn", memory.Result.models[0].mnnFiles[0]);
            Assert.AreEqual(1, memory.Result.models[0].inputSpec.shape[0]);
            Assert.AreEqual(1, memory.Result.models[0].outputSpec.shape[0]);
            var reopened = new MNNModelCatalogCache(_directory, discover, () => _now);
            var disk = reopened.LoadAsync(MNNModelProvider.ModelScope);
            yield return Wait(disk);
            Assert.AreEqual(1, calls, "Reopening the manager or reloading the domain must not fetch a fresh snapshot again.");
            Assert.AreEqual("model-1", disk.Result.models[0].name);
            Assert.True(reopened.IsFresh(disk.Result));
        }

        [UnityEngine.TestTools.UnityTest]
        public IEnumerator ExpiredCacheIsReadableDuringOneCoalescedBackgroundRefresh()
        {
            int calls = 0;
            var release = new TaskCompletionSource<MNNModelRepository>(TaskCreationOptions.RunContinuationsAsynchronously);
            var cache = new MNNModelCatalogCache(_directory, provider => Interlocked.Increment(ref calls) == 1
                ? Task.FromResult(Repository(provider, "old")) : release.Task, () => _now);
            var initial = cache.LoadAsync(MNNModelProvider.ModelScope);
            yield return Wait(initial);
            _now = _now.AddHours(24);
            Assert.True(cache.TryRead(MNNModelProvider.ModelScope, out var stale));
            Assert.False(cache.IsFresh(stale));
            var requests = Enumerable.Range(0, 16).Select(_ => cache.LoadAsync(MNNModelProvider.ModelScope)).ToArray();
            yield return Until(() => Volatile.Read(ref calls) == 2);
            Assert.True(requests.All(task => !task.IsCompleted));
            Assert.True(cache.TryRead(MNNModelProvider.ModelScope, out stale));
            Assert.AreEqual("old", stale.models[0].name);
            release.SetResult(Repository(MNNModelProvider.ModelScope, "new"));
            yield return Wait(Task.WhenAll(requests));
            Assert.AreEqual(2, calls);
            Assert.True(requests.All(task => task.Result.models[0].name == "new"));
            Assert.AreNotSame(requests[0].Result.models[0], requests[1].Result.models[0]);
        }

        [UnityEngine.TestTools.UnityTest]
        public IEnumerator ForceRefreshBypassesFreshSnapshotAndFailurePreservesMemoryAndDisk()
        {
            int calls = 0;
            bool fail = false;
            var cache = new MNNModelCatalogCache(_directory, provider =>
            {
                ++calls;
                return fail ? Task.FromException<MNNModelRepository>(new IOException("offline"))
                    : Task.FromResult(Repository(provider, "model-" + calls));
            }, () => _now);
            var first = cache.LoadAsync(MNNModelProvider.HuggingFace);
            yield return Wait(first);
            var force = cache.LoadAsync(MNNModelProvider.HuggingFace, true);
            yield return Wait(force);
            Assert.AreEqual(2, calls);
            Assert.AreEqual("model-2", force.Result.models[0].name);
            string path = Directory.GetFiles(_directory, "*.json").Single();
            byte[] before = File.ReadAllBytes(path);
            fail = true;
            var failed = cache.LoadAsync(MNNModelProvider.HuggingFace, true);
            yield return Until(() => failed.IsCompleted);
            Assert.True(failed.IsFaulted);
            StringAssert.Contains("offline", failed.Exception.ToString());
            CollectionAssert.AreEqual(before, File.ReadAllBytes(path));
            Assert.True(cache.TryRead(MNNModelProvider.HuggingFace, out var previous));
            Assert.AreEqual("model-2", previous.models[0].name);
            fail = false;
            var retry = cache.LoadAsync(MNNModelProvider.HuggingFace, true);
            yield return Wait(retry);
            Assert.AreEqual("model-4", retry.Result.models[0].name);
            Assert.IsEmpty(Directory.GetFiles(_directory, "*.tmp"));
        }

        [UnityEngine.TestTools.UnityTest]
        public IEnumerator ProvidersDiscoverConcurrentlyAndOneFailureDoesNotEraseTheOther()
        {
            var started = new CountdownEvent(2);
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var cache = new MNNModelCatalogCache(_directory, async provider =>
            {
                started.Signal();
                await release.Task;
                if (provider == MNNModelProvider.HuggingFace)
                    throw new IOException("HF offline");
                return Repository(provider, "complete");
            }, () => _now);
            var ms = cache.LoadAsync(MNNModelProvider.ModelScope);
            var hf = cache.LoadAsync(MNNModelProvider.HuggingFace);
            yield return Until(() => started.CurrentCount == 0);
            release.SetResult(true);
            yield return Wait(ms);
            yield return Until(() => hf.IsCompleted);
            Assert.True(hf.IsFaulted);
            StringAssert.Contains("HF offline", hf.Exception.ToString());
            Assert.True(cache.TryRead(MNNModelProvider.ModelScope, out var retained));
            Assert.AreEqual("complete", retained.models[0].name);
            Assert.False(cache.TryRead(MNNModelProvider.HuggingFace, out _));
            started.Dispose();
        }

        [UnityEngine.TestTools.UnityTest]
        public IEnumerator CorruptObsoleteAndCrossProviderSnapshotsFallBackToDiscovery()
        {
            foreach (string invalid in new[] {"not json", "{}", "{\"schema\":999}",
                "{\"schema\":" + MNNModelCatalogCache.SchemaVersion + ",\"provider\":1,\"savedUtcTicks\":10,\"models\":[],\"warnings\":[]}",
                "{\"schema\":" + MNNModelCatalogCache.SchemaVersion + ",\"provider\":0,\"savedUtcTicks\":10,\"models\":[null],\"warnings\":[]}"})
            {
                File.WriteAllText(Path.Combine(_directory, "ModelScope-v" + MNNModelCatalogCache.SchemaVersion + ".json"), invalid);
                int calls = 0;
                var cache = new MNNModelCatalogCache(_directory, provider =>
                {
                    ++calls;
                    return Task.FromResult(Repository(provider, "recovered"));
                }, () => _now);
                Assert.False(cache.TryRead(MNNModelProvider.ModelScope, out _));
                var load = cache.LoadAsync(MNNModelProvider.ModelScope);
                yield return Wait(load);
                Assert.AreEqual(1, calls);
                Assert.AreEqual("recovered", load.Result.models[0].name);
            }
        }

        [UnityEngine.TestTools.UnityTest]
        public IEnumerator PreviousSchemaIsDisplayableButRefreshesAndSurvivesAnOfflineUpgrade()
        {
            var snapshot = new MNNModelCatalogCache.Snapshot {schema = 6, provider = MNNModelProvider.ModelScope,
                savedUtcTicks = _now.Ticks, models = Repository(MNNModelProvider.ModelScope, "previous-schema").models.ToArray(), warnings = Array.Empty<string>()};
            string old = JsonUtility.ToJson(snapshot);
            File.WriteAllText(Path.Combine(_directory, "ModelScope-v6.json"), old);
            File.WriteAllText(Path.Combine(_directory, "ModelScope-v7.json"), "corrupt");
            File.WriteAllText(Path.Combine(_directory, "ModelScope-v999.json"), old);
            bool offline = true;
            int calls = 0;
            var cache = new MNNModelCatalogCache(_directory, provider =>
            {
                ++calls;
                return offline ? Task.FromException<MNNModelRepository>(new IOException("rate limited"))
                    : Task.FromResult(Repository(provider, "current"));
            }, () => _now);
            Assert.True(cache.TryRead(MNNModelProvider.ModelScope, out var previous));
            Assert.AreEqual("previous-schema", previous.models.Single().name);
            Assert.False(cache.IsFresh(previous), "A schema change must refresh even when its timestamp is recent.");
            var failed = cache.LoadAsync(MNNModelProvider.ModelScope);
            yield return Until(() => failed.IsCompleted);
            Assert.True(failed.IsFaulted);
            Assert.True(cache.TryRead(MNNModelProvider.ModelScope, out previous));
            Assert.AreEqual("previous-schema", previous.models.Single().name);
            Assert.AreEqual(old, File.ReadAllText(Path.Combine(_directory, "ModelScope-v6.json")));
            offline = false;
            var refreshed = cache.LoadAsync(MNNModelProvider.ModelScope);
            yield return Wait(refreshed);
            Assert.AreEqual(2, calls);
            Assert.True(cache.IsFresh(refreshed.Result));
            Assert.AreEqual("current", refreshed.Result.models.Single().name);
            var reopened = new MNNModelCatalogCache(_directory, provider => throw new Exception("Must read the new cache"), () => _now);
            Assert.True(reopened.TryRead(MNNModelProvider.ModelScope, out var disk));
            Assert.True(reopened.IsFresh(disk));
        }

        [UnityEngine.TestTools.UnityTest]
        public IEnumerator PreviousAudioCategoriesMigrateKeywordsWhileOffline()
        {
            var provider = MNNModelProvider.HuggingFace;
            var models = new[]
            {
                new MNNModelInfo {id = "hf:kws", provider = provider, repositoryId = MNNKwsModel.Repository,
                    category = "AudioProcessing", tasks = new[] {"automatic-speech-recognition"}},
                new MNNModelInfo {id = "hf:custom", provider = provider, repositoryId = "author/custom-MNN",
                    category = "SpeechRecognition", tasks = new[] {"keyword-spotting"}},
                new MNNModelInfo {id = "hf:vad", provider = provider, repositoryId = MNNSileroModel.Repository,
                    category = "AudioProcessing"}
            };
            var snapshot = new MNNModelCatalogCache.Snapshot {schema = MNNModelCatalogCache.SchemaVersion - 1,
                provider = provider, savedUtcTicks = _now.Ticks, models = models, warnings = Array.Empty<string>()};
            File.WriteAllText(Path.Combine(_directory, "HuggingFace-v" + snapshot.schema + ".json"), JsonUtility.ToJson(snapshot));
            var cache = new MNNModelCatalogCache(_directory,
                _ => Task.FromException<MNNModelRepository>(new IOException("offline")), () => _now);
            Assert.True(cache.TryRead(provider, out var visible));
            CollectionAssert.AreEqual(new[] {"KeywordSpotting", "KeywordSpotting", "VoiceActivityDetection"}, visible.models.Select(model => model.category));
            var failed = cache.LoadAsync(provider);
            yield return Until(() => failed.IsCompleted);
            Assert.True(failed.IsFaulted);
            Assert.True(cache.TryRead(provider, out visible));
            Assert.AreEqual("KeywordSpotting", visible.models[0].category);
        }

        [Test]
        public void PreviousAudioCatalogIsSplitOfflineWithoutChangingRuntimeAvailability()
        {
            var old = new MNNModelCatalogCache.Snapshot { schema = 28, provider = MNNModelProvider.ModelScope,
                savedUtcTicks = _now.Ticks, models = new[]
                {
                    new MNNModelInfo { repositoryId = "EitanWang/Silero-v5.1-MNN", category = "AudioProcessing", runtimeAvailable = true },
                    new MNNModelInfo { repositoryId = "EitanWang/TurnSense-SmolLM2-135M-MNN", category = "AudioProcessing", runtimeAvailable = false },
                    new MNNModelInfo { repositoryId = "publisher/custom-MNN", category = "AudioProcessing", tasks = new[] { "speech-enhancement" } },
                    new MNNModelInfo { repositoryId = "EitanWang/SpExPlus-WSJ02Mix-8K-MNN", category = "AudioProcessing", tasks = new[] { "speech-separation" } },
                    new MNNModelInfo { repositoryId = "publisher/other-MNN", category = "ImageClassification" }
                }, warnings = Array.Empty<string>() };
            foreach (var model in old.models) model.id = model.repositoryId;
            File.WriteAllText(Path.Combine(_directory, "ModelScope-v28.json"), JsonUtility.ToJson(old));
            int requests = 0;
            var cache = new MNNModelCatalogCache(_directory, provider => { ++requests; throw new IOException("offline"); }, () => _now);
            Assert.True(cache.TryRead(MNNModelProvider.ModelScope, out var migrated));
            CollectionAssert.AreEqual(new[] { "VoiceActivityDetection", "TurnDetection", "SpeechEnhancement", "TargetSpeakerExtraction", "ImageClassification" },
                migrated.models.Select(model => model.category));
            Assert.True(migrated.RequiresRefresh);
            Assert.True(migrated.models[0].runtimeAvailable);
            Assert.False(migrated.models[1].runtimeAvailable);
            Assert.Zero(requests);
            Assert.That(File.ReadAllText(Path.Combine(_directory, "ModelScope-v28.json")), Does.Contain("AudioProcessing"), "Migration must preserve the old fallback on disk.");
        }

        [UnityEngine.TestTools.UnityTest]
        public IEnumerator DiskWriteFailureKeepsSuccessfulDiscoveryInMemory()
        {
            string blocked = Path.Combine(_directory, "not-a-directory");
            File.WriteAllText(blocked, "block");
            int calls = 0;
            var cache = new MNNModelCatalogCache(blocked, provider =>
            {
                ++calls;
                return Task.FromResult(Repository(provider, "usable"));
            }, () => _now);
            var first = cache.LoadAsync(MNNModelProvider.ModelScope);
            yield return Wait(first);
            StringAssert.Contains("could not save", first.Result.warnings.Single());
            var next = cache.LoadAsync(MNNModelProvider.ModelScope);
            yield return Wait(next);
            Assert.AreEqual(1, calls);
            Assert.AreEqual("usable", next.Result.models[0].name);
        }

        [UnityEngine.TestTools.UnityTest]
        public IEnumerator ManagerOpensFromCacheAndRefreshButtonForcesBothProviders()
        {
            int calls = 0;
            bool failHf = false;
            var cache = new MNNModelCatalogCache(_directory, provider =>
            {
                int round = Interlocked.Increment(ref calls);
                if (failHf && provider == MNNModelProvider.HuggingFace)
                    return Task.FromException<MNNModelRepository>(new IOException("HF offline"));
                return Task.FromResult(Repository(provider, "round-" + round));
            }, () => _now);
            yield return Wait(Task.WhenAll(cache.LoadAsync(MNNModelProvider.ModelScope), cache.LoadAsync(MNNModelProvider.HuggingFace)));
            var previousShared = MNNModelCatalogCache.Shared;
            MNNModelManagerWindow window = null;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            Func<string, object> get = field => typeof(MNNModelManagerWindow).GetField(field, flags).GetValue(window);
            Action refresh = () => typeof(MNNModelManagerWindow).GetMethod("RefreshRepository", flags).Invoke(window, null);
            try
            {
                MNNModelCatalogCache.Shared = cache;
                window = ScriptableObject.CreateInstance<MNNModelManagerWindow>();
                yield return Until(() => !(bool)get("_isLoadingModels"));
                int expectedCount = MNNModelRepository.GetDefaultRepository().models.Count + 2;
                Assert.AreEqual(expectedCount, ((MNNModelRepository)get("_repository")).models.Count);
                Assert.AreEqual(2, calls, "Opening the actual manager should use the fresh cache.");
                refresh();
                yield return Until(() => !(bool)get("_isLoadingModels"));
                Assert.AreEqual(4, calls, "The toolbar refresh path must discover both providers even while cache is fresh.");
                string hf = ((MNNModelRepository)get("_repository")).models.Single(model => model.provider == MNNModelProvider.HuggingFace).name;
                failHf = true;
                refresh();
                yield return Until(() => !(bool)get("_isLoadingModels"));
                Assert.AreEqual(6, calls);
                var models = ((MNNModelRepository)get("_repository")).models;
                Assert.AreEqual(expectedCount, models.Count);
                Assert.AreEqual(hf, models.Single(model => model.provider == MNNModelProvider.HuggingFace).name);
                StringAssert.Contains("HF offline", (string)get("_repositoryError"));
            }
            finally
            {
                if (window != null)
                    UnityEngine.Object.DestroyImmediate(window);
                MNNModelCatalogCache.Shared = previousShared;
            }
        }

        [UnityEngine.TestTools.UnityTest]
        public IEnumerator ReopenedManagerShowsExpiredCacheAndJoinsTheRefreshStartedBeforeClosing()
        {
            int calls = 0;
            bool refresh = false;
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var cache = new MNNModelCatalogCache(_directory, async provider =>
            {
                Interlocked.Increment(ref calls);
                if (refresh)
                    await release.Task;
                return Repository(provider, refresh ? "new" : "old");
            }, () => _now);
            yield return Wait(Task.WhenAll(cache.LoadAsync(MNNModelProvider.ModelScope), cache.LoadAsync(MNNModelProvider.HuggingFace)));
            _now = _now.AddHours(25);
            refresh = true;
            var previousShared = MNNModelCatalogCache.Shared;
            MNNModelManagerWindow first = null, reopened = null;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            Func<MNNModelManagerWindow, MNNModelRepository> repository = window => (MNNModelRepository)
                typeof(MNNModelManagerWindow).GetField("_repository", flags).GetValue(window);
            try
            {
                MNNModelCatalogCache.Shared = cache;
                first = ScriptableObject.CreateInstance<MNNModelManagerWindow>();
                Assert.True(repository(first).models.Where(model => !MNNDtlnAecModel.IsRepository(model.repositoryId) && !MNNVerifiedTtsModel.IsCosyVoice300MRepository(model.repositoryId) && !MNNModelCatalog.IsPublishedSpeechRepository(model.repositoryId)).All(model => model.name == "old"));
                Assert.AreEqual(49 + 3, repository(first).models.Count);
                yield return Until(() => Volatile.Read(ref calls) == 4);
                UnityEngine.Object.DestroyImmediate(first);
                first = null;
                reopened = ScriptableObject.CreateInstance<MNNModelManagerWindow>();
                Assert.True(repository(reopened).models.Where(model => !MNNDtlnAecModel.IsRepository(model.repositoryId) && !MNNVerifiedTtsModel.IsCosyVoice300MRepository(model.repositoryId) && !MNNModelCatalog.IsPublishedSpeechRepository(model.repositoryId)).All(model => model.name == "old"));
                Assert.AreEqual(49 + 3, repository(reopened).models.Count);
                Assert.AreEqual(4, calls, "Reopening joins the existing shared refresh instead of starting another scan.");
                release.SetResult(true);
                yield return Until(() => !(bool)typeof(MNNModelManagerWindow).GetField("_isLoadingModels", flags).GetValue(reopened));
                Assert.True(repository(reopened).models.Where(model => !MNNDtlnAecModel.IsRepository(model.repositoryId) && !MNNVerifiedTtsModel.IsCosyVoice300MRepository(model.repositoryId) && !MNNModelCatalog.IsPublishedSpeechRepository(model.repositoryId)).All(model => model.name == "new"));
                Assert.AreEqual(49 + 3, repository(reopened).models.Count);
                Assert.AreEqual(4, calls);
            }
            finally
            {
                release.TrySetResult(true);
                if (first != null) UnityEngine.Object.DestroyImmediate(first);
                if (reopened != null) UnityEngine.Object.DestroyImmediate(reopened);
                MNNModelCatalogCache.Shared = previousShared;
            }
        }

        [UnityEngine.TestTools.UnityTest]
        public IEnumerator FreshCacheReadersJoinAnExistingForcedRefreshWithoutStartingAnotherScan()
        {
            int calls = 0;
            var release = new TaskCompletionSource<MNNModelRepository>(TaskCreationOptions.RunContinuationsAsynchronously);
            var cache = new MNNModelCatalogCache(_directory, provider => Interlocked.Increment(ref calls) == 1
                ? Task.FromResult(Repository(provider, "old")) : release.Task, () => _now);
            yield return Wait(cache.LoadAsync(MNNModelProvider.HuggingFace));
            var refresh = cache.LoadAsync(MNNModelProvider.HuggingFace, true);
            yield return Until(() => Volatile.Read(ref calls) == 2);
            var reopened = cache.LoadAsync(MNNModelProvider.HuggingFace);
            Assert.False(reopened.IsCompleted, "Fresh readers must receive the already-requested update.");
            Assert.True(cache.TryRead(MNNModelProvider.HuggingFace, out var visible));
            Assert.True(cache.IsFresh(visible));
            Assert.AreEqual("old", visible.models[0].name, "The old fresh snapshot stays visible during refresh.");
            release.SetResult(Repository(MNNModelProvider.HuggingFace, "new"));
            yield return Wait(Task.WhenAll(refresh, reopened));
            Assert.AreEqual(2, calls);
            Assert.AreEqual("new", reopened.Result.models[0].name);
            Assert.AreNotSame(refresh.Result.models[0], reopened.Result.models[0]);
        }

        [UnityEngine.TestTools.UnityTest, Explicit("Measures memory/disk cache reads of an existing local catalog, without any network or inference.")]
        public IEnumerator MeasureExistingCatalogCacheReadsWithoutNetwork()
        {
            string source = Environment.GetEnvironmentVariable("MNN_TEST_CATALOG_SOURCE") ?? throw new InvalidOperationException("Set MNN_TEST_CATALOG_SOURCE.");
            string output = Environment.GetEnvironmentVariable("MNN_TEST_CATALOG_CACHE_REPORT") ?? throw new InvalidOperationException("Set MNN_TEST_CATALOG_CACHE_REPORT.");
            var catalog = JsonUtility.FromJson<MNNModelRepository>(File.ReadAllText(source));
            int discoveries = 0;
            Func<MNNModelProvider, Task<MNNModelRepository>> discover = provider =>
            {
                Interlocked.Increment(ref discoveries);
                return Task.FromResult(new MNNModelRepository {models = catalog.models.Where(model => model.provider == provider).ToList()});
            };
            var cache = new MNNModelCatalogCache(_directory, discover, () => _now);
            yield return Wait(Task.WhenAll(cache.LoadAsync(MNNModelProvider.ModelScope), cache.LoadAsync(MNNModelProvider.HuggingFace)));
            var memory = new double[21];
            var disk = new double[7];
            var clock = new Stopwatch();
            for (int i = 0; i < memory.Length; ++i)
            {
                clock.Restart();
                var a = cache.LoadAsync(MNNModelProvider.ModelScope);
                var b = cache.LoadAsync(MNNModelProvider.HuggingFace);
                Assert.True(a.IsCompleted && b.IsCompleted);
                Assert.AreEqual(catalog.models.Count, a.Result.models.Length + b.Result.models.Length);
                memory[i] = clock.Elapsed.TotalMilliseconds;
            }
            for (int i = 0; i < disk.Length; ++i)
            {
                var reopened = new MNNModelCatalogCache(_directory, discover, () => _now);
                clock.Restart();
                var a = reopened.LoadAsync(MNNModelProvider.ModelScope);
                var b = reopened.LoadAsync(MNNModelProvider.HuggingFace);
                Assert.True(a.IsCompleted && b.IsCompleted);
                Assert.AreEqual(catalog.models.Count, a.Result.models.Length + b.Result.models.Length);
                disk[i] = clock.Elapsed.TotalMilliseconds;
            }
            Assert.AreEqual(2, discoveries, "Every measured read must avoid discovery entirely.");
            Array.Sort(memory);
            Array.Sort(disk);
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllText(output, JsonUtility.ToJson(new CacheMeasurement
            {
                repositories = catalog.models.Count, memoryMedianMs = memory[10], memoryP95Ms = memory[19],
                diskMedianMs = disk[3], diskMaximumMs = disk[6], networkRequests = 0,
                memorySamples = memory.Length, diskSamples = disk.Length, unity = Application.unityVersion
            }, true));
            TestContext.WriteLine(File.ReadAllText(output));
        }

        [Serializable]
        private sealed class CacheMeasurement
        {
            public int repositories, memorySamples, diskSamples, networkRequests;
            public double memoryMedianMs, memoryP95Ms, diskMedianMs, diskMaximumMs;
            public string unity;
        }

        private static MNNModelRepository Repository(MNNModelProvider provider, string name)
        {
            var result = new MNNModelRepository();
            result.models.Add(new MNNModelInfo
            {
                id = provider + ":author/" + name, repositoryId = "author/" + name,
                name = name, provider = provider, tags = new[] {"mnn"}, isRepository = true,
                tasks = new[] {"text-generation"}, mnnFiles = new[] {"model.mnn"},
                inputSpec = new ModelInputSpec {name = "input", shape = new[] {1, 2}},
                outputSpec = new ModelOutputSpec {name = "output", shape = new[] {1, 2}},
                isInstalled = true, localPath = "must-not-persist"
            });
            return result;
        }

        private static IEnumerator Wait(Task task)
        {
            yield return Until(() => task.IsCompleted);
            Assert.False(task.IsFaulted, task.Exception?.ToString());
            Assert.False(task.IsCanceled);
        }

        private static IEnumerator Until(Func<bool> condition)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(10);
            while (!condition() && DateTime.UtcNow < deadline)
                yield return null;
            Assert.True(condition(), "Catalog cache operation timed out.");
        }
    }
}
