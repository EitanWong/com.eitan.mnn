using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MNN.Unity.Tests
{
    public class ModelAcquisitionTests
    {
        private string _root;
        private static readonly byte[] ModelBytes = Encoding.UTF8.GetBytes("runtime model fixture");

        [UnityTest]
        public IEnumerator UnifiedCatalogTraversesAllProviderStreamsAndQueriesOnlyOnce()
        {
            var pool = new MNNHuggingFaceSourcePool(new[] { new MNNHuggingFaceSource("Fixture", "https://unified-hf.test") });
            int scopeRequests = 0, faceRequests = 0;
            using (var client = Client(request =>
            {
                if (request.RequestUri.AbsolutePath == "/openapi/v1/models")
                {
                    ++scopeRequests;
                    return Json("{\"success\":true,\"data\":{\"total_count\":0,\"models\":[]}}");
                }
                ++faceRequests;
                return Json("[]");
            }))
            {
                foreach (MNNModelSource source in Enum.GetValues(typeof(MNNModelSource)))
                {
                    int stream = 0, page = 1, fetched = 0;
                    string cursor = null;
                    bool more;
                    do
                    {
                        var task = new MNNModelCatalog().FetchCatalogPageAsync(source, stream, page, cursor,
                            httpClient: client, modelScopeApiRoot: "https://unified-ms.test", mirrorPool: pool);
                        yield return Until(task);
                        AssertSucceeded(task);
                        stream = task.Result.NextStream; page = task.Result.NextPage; cursor = task.Result.NextCursor;
                        more = task.Result.HasMore;
                        Assert.LessOrEqual(++fetched, 3, "Empty streams must advance and eventually exhaust.");
                    } while (more);
                    Assert.AreEqual(3, fetched, "Union official, tag and name search streams for both consumers.");
                    var search = new MNNModelCatalog().FetchCatalogPageAsync(source, query: "turn detection",
                        httpClient: client, modelScopeApiRoot: "https://unified-ms.test", mirrorPool: pool);
                    yield return Until(search);
                    AssertSucceeded(search);
                    Assert.IsFalse(search.Result.HasMore, "A user query must not repeat through the three default streams.");
                }
                Assert.AreEqual(4, scopeRequests);
                Assert.AreEqual(4, faceRequests);
            }
        }

        [UnityTest, Explicit("Downloads the pinned Silero graph for a real transfer measurement.")]
        public IEnumerator LivePinnedModelTransferMeasuresDownload()
        {
            string destination = Environment.GetEnvironmentVariable("MNN_TEST_LIVE_TRANSFER_DESTINATION");
            if (string.IsNullOrWhiteSpace(destination)) Assert.Ignore("Set MNN_TEST_LIVE_TRANSFER_DESTINATION to a model asset path.");
            Assert.False(File.Exists(destination), "Use a fresh destination to measure a real transfer.");
            var watch = System.Diagnostics.Stopwatch.StartNew();
            long progress = 0;
            using (var client = new HttpClient { Timeout = TimeSpan.FromMinutes(2) })
            using (var transfer = new MNNModelFileTransfer(client))
            {
                var file = new MNNModelTransferFile { Path = "silero_vad.mnn", Revision = "e2502b184542017860ab1ca83c18f8742b221ecd",
                    Size = 1486656, Sha256 = "20008d132c61be560704c6d81459b688ffce7244b46a0afdaa0247e2d09d2727" };
                var task = transfer.DownloadAsync("Jeryuk/silero_vad-MNN", file, destination, value => progress = value,
                    CancellationToken.None, () => "https://huggingface.co/Jeryuk/silero_vad-MNN/resolve/e2502b184542017860ab1ca83c18f8742b221ecd/silero_vad.mnn");
                yield return Until(task);
                AssertSucceeded(task);
                Assert.AreEqual(file.Size, new FileInfo(destination).Length);
                Assert.AreEqual(file.Size, progress);
                TestContext.WriteLine("LIVE_TRANSFER platform=" + Application.platform + " bytes=" + file.Size +
                    " elapsed_ms=" + watch.Elapsed.TotalMilliseconds.ToString("F1"));
            }
        }

        [UnityTest, Explicit("Queries both public catalogs for a real first-page measurement.")]
        public IEnumerator LiveCatalogFirstPagesMeasureDiscovery()
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var scope = new MNNModelCatalog().FetchModelScopeSearchPageAsync(0, 1, null);
            yield return Until(scope);
            AssertSucceeded(scope);
            double scopeMs = watch.Elapsed.TotalMilliseconds;
            watch.Restart();
            var face = new MNNModelCatalog().FetchHuggingFaceSearchPageAsync(0, null, null);
            yield return Until(face);
            AssertSucceeded(face);
            TestContext.WriteLine("LIVE_CATALOG platform=" + Application.platform + " modelscope_models=" + scope.Result.Models.Count +
                " modelscope_ms=" + scopeMs.ToString("F1") + " hf_models=" + face.Result.Models.Count +
                " hf_ms=" + watch.Elapsed.TotalMilliseconds.ToString("F1"));
        }

        [UnityTest]
        public IEnumerator ModelScopePageLoadsOnlyRequestedPageAndPreservesContinuation()
        {
            var method = typeof(MNNModelCatalog).GetMethod("FetchModelScopePageAsync");
            Assert.NotNull(method, "Runtime catalog must expose on-demand pages.");
            int searches = 0, manifests = 0;
            using (var client = Client(request =>
            {
                if (request.RequestUri.AbsolutePath == "/openapi/v1/models")
                {
                    ++searches;
                    StringAssert.Contains("page_number=1", request.RequestUri.Query);
                    return Json("{\"success\":true,\"data\":{\"total_count\":100,\"models\":[{\"id\":\"MNN/fixture\",\"tasks\":[\"text-generation\"]}]}}");
                }
                ++manifests;
                return ModelScopeFiles("model.mnn", Hash(ModelBytes));
            }))
            {
                var task = (Task)method.Invoke(new MNNModelCatalog(), new object[] { 1, client, "https://runtime-ms.test", CancellationToken.None });
                yield return Until(task);
                AssertSucceeded(task);
                Assert.AreEqual(1, searches);
                Assert.AreEqual(1, manifests);
                var page = task.GetType().GetProperty("Result").GetValue(task);
                Assert.AreEqual(2, page.GetType().GetProperty("NextPage").GetValue(page));
            }
        }

        [UnityTest]
        public IEnumerator HuggingFacePageFollowsCursorOnlyWhenRequested()
        {
            var method = typeof(MNNModelCatalog).GetMethod("FetchHuggingFacePageAsync");
            Assert.NotNull(method, "Runtime catalog must expose cursor-based pages.");
            int requests = 0;
            var pool = new MNNHuggingFaceSourcePool(new[] {new MNNHuggingFaceSource("Fixture", "https://runtime-hf.test")});
            using (var client = Client(request =>
            {
                ++requests;
                var response = Json("[{\"id\":\"publisher/fixture-mnn\",\"sha\":\"commit\",\"siblings\":[{\"rfilename\":\"model.mnn\"}]}]");
                response.Headers.TryAddWithoutValidation("Link", "<https://runtime-hf.test/api/models?cursor=next>; rel=\"next\"");
                return response;
            }))
            {
                var task = (Task)method.Invoke(new MNNModelCatalog(), new object[] { null, client, pool, CancellationToken.None });
                yield return Until(task);
                AssertSucceeded(task);
                Assert.AreEqual(1, requests);
                var page = task.GetType().GetProperty("Result").GetValue(task);
                StringAssert.Contains("cursor=next", (string)page.GetType().GetProperty("NextCursor").GetValue(page));
            }
        }

        [UnityTest]
        public IEnumerator ModelScopePagedDiscoveryVisitsEachSearchStream()
        {
            var method = typeof(MNNModelCatalog).GetMethod("FetchModelScopeSearchPageAsync");
            Assert.NotNull(method, "Each search stream must be independently pageable.");
            var queries = new System.Collections.Generic.List<string>();
            using (var client = Client(request =>
            {
                queries.Add(request.RequestUri.Query);
                return Json("{\"success\":true,\"data\":{\"total_count\":0,\"models\":[]}}");
            }))
            {
                for (int stream = 0; stream < 3; ++stream)
                {
                    var task = (Task)method.Invoke(new MNNModelCatalog(), new object[] { stream, 1, null, client, "https://runtime-ms.test", CancellationToken.None });
                    yield return Until(task);
                    AssertSucceeded(task);
                }
            }
            StringAssert.Contains("owner=MNN", queries[0]);
            StringAssert.Contains("filter.library=mnn", queries[1]);
            StringAssert.Contains("search=mnn", queries[2]);
        }

        [UnityTest]
        public IEnumerator RemoteSearchEscapesUserQueryAtTheProviderBoundary()
        {
            var method = typeof(MNNModelCatalog).GetMethod("FetchModelScopeSearchPageAsync");
            Assert.NotNull(method);
            string query = null;
            using (var client = Client(request =>
            {
                query = request.RequestUri.Query;
                return Json("{\"success\":true,\"data\":{\"total_count\":0,\"models\":[]}}");
            }))
            {
                var task = (Task)method.Invoke(new MNNModelCatalog(), new object[] { 0, 1, "name&owner=other", client, "https://runtime-ms.test", CancellationToken.None });
                yield return Until(task);
                AssertSucceeded(task);
            }
            StringAssert.Contains("name%26owner%3Dother", query);
        }

        [UnityTest]
        public IEnumerator HuggingFacePagedDiscoveryVisitsEachSearchStream()
        {
            var queries = new System.Collections.Generic.List<string>();
            var pool = new MNNHuggingFaceSourcePool(new[] {new MNNHuggingFaceSource("Fixture", "https://runtime-hf.test")});
            using (var client = Client(request => { queries.Add(request.RequestUri.Query); return Json("[]"); }))
            {
                for (int stream = 0; stream < 3; ++stream)
                {
                    var task = new MNNModelCatalog().FetchHuggingFaceSearchPageAsync(stream, null, null, client, pool);
                    yield return Until(task);
                    AssertSucceeded(task);
                }
            }
            StringAssert.Contains("author=taobao-mnn", queries[0]);
            StringAssert.Contains("filter=mnn", queries[1]);
            StringAssert.Contains("search=mnn", queries[2]);
        }

        [UnityTest]
        public IEnumerator HuggingFaceRemoteSearchUsesEscapedQuery()
        {
            string query = null;
            var pool = new MNNHuggingFaceSourcePool(new[] {new MNNHuggingFaceSource("Fixture", "https://runtime-hf.test")});
            using (var client = Client(request => { query = request.RequestUri.Query; return Json("[]"); }))
            {
                var task = new MNNModelCatalog().FetchHuggingFaceSearchPageAsync(0, null, "name&author=other", client, pool);
                yield return Until(task);
                AssertSucceeded(task);
            }
            StringAssert.Contains("name%26author%3Dother", query);
        }

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Application.isEditor ? Path.GetFullPath("TestArtifacts~/ModelHubApi/Tests")
                : Application.persistentDataPath, "ModelAcquisition", Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, true);
        }

        [TestCase(1)]
        [TestCase(4)]
        [TestCase(16)]
        public void DefaultDownloaderCanBeCreatedOnPlayer(int maxConnections)
        {
            // Injected HTTP handlers bypass the platform handler that failed on Android.
            using (var downloader = new MNNModelDownloader(options: new MNNModelTransferOptions
            {
                MaxConnections = maxConnections
            }))
            {
                Assert.NotNull(downloader);
            }
        }

        [UnityTest]
        public IEnumerator DefaultCatalogClientSupportsCancellationOnPlayer()
        {
            using (var cancel = new CancellationTokenSource())
            {
                cancel.Cancel();
                // ModelScope discovery creates the real platform client before checking
                // cancellation. No HTTP request or model download is needed.
                var task = new MNNModelCatalog().DiscoverModelScopeModelsAsync(cancellationToken: cancel.Token);
                yield return Until(task);
                Assert.True(task.IsCanceled, task.Exception?.ToString());
            }
        }

        [UnityTest]
        public IEnumerator ModelScopeCatalogEntryDownloadsWithProgressAndRequestedRevision()
        {
            int transfers = 0;
            long received = 0, total = 0;
            string current = null;
            using (var client = Client(request =>
            {
                if (request.RequestUri.AbsolutePath == "/openapi/v1/models")
                    return Json("{\"success\":true,\"data\":{\"total_count\":1,\"models\":[{\"id\":\"MNN/fixture\",\"tasks\":[\"text-generation\"]}]}}");
                StringAssert.Contains("Revision=release-2", request.RequestUri.Query);
                if (request.RequestUri.AbsolutePath.EndsWith("/repo/files"))
                    return ModelScopeFiles("nested/model.mnn", Hash(ModelBytes));
                ++transfers;
                return Bytes();
            }))
            using (var downloader = new MNNModelDownloader(client, "https://runtime-ms.test"))
            {
                var catalog = new MNNModelCatalog();
                var load = catalog.LoadModelScopeModelsAsync(client, "https://runtime-ms.test");
                yield return Until(load);
                AssertSucceeded(load);
                Assert.AreEqual(1, catalog.Models.Count);
                var model = catalog.SearchModels("FIXTURE").Single();
                Assert.AreEqual(MNNModelSource.ModelScope, model.provider);
                model.revision = "release-2";
                var download = downloader.DownloadAsync(model, _root, (count, size, file) =>
                {
                    received = count; total = size; current = file;
                });
                yield return Until(download);
                AssertSucceeded(download);
                Assert.AreEqual(Path.GetFullPath(_root), download.Result);
                Assert.AreEqual(1, transfers);
                Assert.AreEqual(ModelBytes.Length, received);
                Assert.AreEqual(ModelBytes.Length, total);
                Assert.AreEqual("nested/model.mnn", current);
                CollectionAssert.AreEqual(ModelBytes, File.ReadAllBytes(Path.Combine(_root, "nested/model.mnn")));
                Assert.False(File.Exists(Path.Combine(_root, ".mnn-download-manifest.json")));
                // A completed, hash-verified file can be reused without transferring weights again.
                var reuse = downloader.DownloadAsync(model, _root);
                yield return Until(reuse);
                AssertSucceeded(reuse);
                Assert.AreEqual(1, transfers);
            }
        }

        [UnityTest]
        public IEnumerator HuggingFaceCatalogEntryUsesItsCommitAndCallerSuppliedSourcePool()
        {
            int transfers = 0;
            var pool = new MNNHuggingFaceSourcePool(new[] {new MNNHuggingFaceSource("Fixture", "https://runtime-hf.test")});
            using (var client = Client(request =>
            {
                Assert.AreEqual("runtime-hf.test", request.RequestUri.Host);
                if (request.RequestUri.AbsolutePath == "/api/models/publisher/fixture-mnn")
                    return Json("{\"id\":\"publisher/fixture-mnn\",\"sha\":\"commit-42\",\"tags\":[\"mnn\"]}");
                if (request.RequestUri.AbsolutePath.Contains("/revision/"))
                {
                    StringAssert.EndsWith("/revision/commit-42", request.RequestUri.AbsolutePath);
                    return Json("{\"siblings\":[{\"rfilename\":\"graph.mnn\",\"lfs\":{\"size\":" + ModelBytes.Length + ",\"sha256\":\"" + Hash(ModelBytes) + "\"}}]}");
                }
                StringAssert.EndsWith("/resolve/commit-42/graph.mnn", request.RequestUri.AbsolutePath);
                ++transfers;
                return Bytes();
            }))
            using (var downloader = new MNNModelDownloader(client, mirrorPool: pool))
            {
                var catalog = new MNNModelCatalog();
                var load = catalog.LoadHuggingFaceModelsAsync(new[] {"publisher/fixture-mnn"}, client, pool);
                yield return Until(load);
                AssertSucceeded(load);
                var model = catalog.Models.Single();
                Assert.AreEqual(MNNModelSource.HuggingFace, model.provider);
                Assert.AreEqual("commit-42", model.revision);
                var download = downloader.DownloadAsync(model, _root);
                yield return Until(download);
                AssertSucceeded(download);
                Assert.AreEqual(1, transfers);
                CollectionAssert.AreEqual(ModelBytes, File.ReadAllBytes(Path.Combine(_root, "graph.mnn")));
            }
        }

        [UnityTest]
        public IEnumerator SameSizeFileWithoutHashIsTransferredAgainForANewRevision()
        {
            bool newer = false;
            int transfers = 0;
            byte[] changed = ModelBytes.Select(value => (byte)(value ^ 1)).ToArray();
            using (var client = Client(request =>
            {
                StringAssert.Contains(newer ? "Revision=commit-2" : "Revision=commit-1", request.RequestUri.Query);
                if (request.RequestUri.AbsolutePath.EndsWith("/repo/files"))
                    return ModelScopeFiles("model.mnn", null);
                ++transfers;
                return new HttpResponseMessage(HttpStatusCode.OK) {Content = new ByteArrayContent(newer ? changed : ModelBytes)};
            }))
            using (var downloader = new MNNModelDownloader(client, "https://runtime-revisions.test"))
            {
                var first = downloader.DownloadAsync(MNNModelSource.ModelScope, "MNN/fixture", _root, "commit-1");
                yield return Until(first);
                AssertSucceeded(first);
                newer = true;
                var second = downloader.DownloadAsync(MNNModelSource.ModelScope, "MNN/fixture", _root, "commit-2");
                yield return Until(second);
                AssertSucceeded(second);
                Assert.AreEqual(2, transfers);
                CollectionAssert.AreEqual(changed, File.ReadAllBytes(Path.Combine(_root, "model.mnn")));
            }
        }

        [UnityTest]
        public IEnumerator CancellationBeforeDownloadDoesNotSendRequestsOrCreateFiles()
        {
            using (var cancel = new CancellationTokenSource())
            using (var client = Client(_ => { Assert.Fail("Cancelled acquisition must not send HTTP requests."); return Bytes(); }))
            using (var downloader = new MNNModelDownloader(client))
            {
                cancel.Cancel();
                var task = downloader.DownloadAsync(MNNModelSource.ModelScope, "MNN/fixture", _root, cancellationToken: cancel.Token);
                yield return Until(task);
                Assert.True(task.IsCanceled);
                Assert.False(Directory.Exists(_root));
            }
        }

        [UnityTest]
        public IEnumerator InsufficientDiskSpaceStopsBeforeTransferringModelBytes()
        {
            Directory.CreateDirectory(_root);
            string manifest = Path.Combine(_root, ".mnn-download-manifest.json");
            File.WriteAllText(manifest, "original metadata");
            int transfers = 0;
            using (var client = Client(request =>
            {
                if (request.RequestUri.AbsolutePath.EndsWith("/repo/files"))
                    return Json("{\"Code\":200,\"Data\":{\"Files\":[{\"Path\":\"model.mnn\",\"Type\":\"blob\",\"Size\":2305843009213693951}]}}");
                ++transfers;
                throw new InvalidOperationException("Model data must not be requested when it cannot fit.");
            }))
            using (var downloader = new MNNModelDownloader(client, "https://runtime-space.test"))
            {
                var task = downloader.DownloadRepositoryAsync("MNN/fixture", _root);
                yield return Until(task);
                Assert.True(task.IsFaulted);
                Assert.That(task.Exception.GetBaseException(), Is.InstanceOf<IOException>());
                StringAssert.Contains("Insufficient disk space", task.Exception.GetBaseException().Message);
                Assert.AreEqual(0, transfers);
                Assert.AreEqual("original metadata", File.ReadAllText(manifest));
            }
        }

        [UnityTest]
        public IEnumerator TruncatedResumeManifestIsRefetchedWithoutLosingCompletedFiles()
        {
            Directory.CreateDirectory(_root);
            File.WriteAllText(Path.Combine(_root, ".mnn-download-manifest.json"), "{");
            File.WriteAllText(Path.Combine(_root, ".mnn-download-repository"), "MNN/fixture");
            File.WriteAllBytes(Path.Combine(_root, "model.mnn"), ModelBytes);
            int listings = 0, transfers = 0;
            using (var client = Client(request =>
            {
                if (request.RequestUri.AbsolutePath.EndsWith("/repo/files"))
                {
                    ++listings;
                    return ModelScopeFiles("model.mnn", Hash(ModelBytes));
                }
                ++transfers;
                return Bytes();
            }))
            using (var downloader = new MNNModelDownloader(client, "https://runtime-manifest.test"))
            {
                var task = downloader.DownloadRepositoryAsync("MNN/fixture", _root);
                yield return Until(task);
                AssertSucceeded(task);
                Assert.AreEqual(1, listings);
                Assert.AreEqual(0, transfers);
                CollectionAssert.AreEqual(ModelBytes, File.ReadAllBytes(Path.Combine(_root, "model.mnn")));
            }
        }

        [UnityTest]
        public IEnumerator HashMismatchNeverPublishesAnInvalidModelFile()
        {
            using (var client = Client(request => request.RequestUri.AbsolutePath.EndsWith("/repo/files")
                ? ModelScopeFiles("model.mnn", new string('0', 64)) : Bytes()))
            using (var downloader = new MNNModelDownloader(client, "https://runtime-hash.test"))
            {
                var task = downloader.DownloadAsync(MNNModelSource.ModelScope, "MNN/fixture", _root);
                yield return Until(task);
                Assert.True(task.IsFaulted);
                Assert.That(task.Exception.GetBaseException(), Is.TypeOf<InvalidDataException>());
                Assert.False(File.Exists(Path.Combine(_root, "model.mnn")));
            }
        }

        [UnityTest]
        public IEnumerator UnsafeRepositoryPathsAreRejectedBeforeAnyModelIsWritten()
        {
            using (var client = Client(_ => ModelScopeFiles("../outside.mnn", Hash(ModelBytes))))
            using (var downloader = new MNNModelDownloader(client, "https://runtime-path.test"))
            {
                var task = downloader.DownloadAsync(MNNModelSource.ModelScope, "MNN/fixture", _root);
                yield return Until(task);
                Assert.True(task.IsFaulted);
                Assert.That(task.Exception.GetBaseException(), Is.TypeOf<InvalidDataException>());
                Assert.False(Directory.Exists(_root));
            }
        }

        [UnityTest]
        public IEnumerator InvalidSourceAndRepositoryAreRejectedBeforeNetworkOrFilesystemAccess()
        {
            using (var client = Client(_ => { Assert.Fail("Invalid acquisition must not send HTTP requests."); return Bytes(); }))
            using (var downloader = new MNNModelDownloader(client))
            {
                foreach (var task in new[]
                {
                    downloader.DownloadAsync((MNNModelSource)100, "MNN/fixture", _root),
                    downloader.DownloadAsync(MNNModelSource.ModelScope, "../fixture", _root),
                    downloader.DownloadAsync(MNNModelSource.HuggingFace, "publisher/name?token=value", _root)
                })
                {
                    yield return Until(task);
                    Assert.True(task.IsFaulted);
                    Assert.That(task.Exception.GetBaseException(), Is.InstanceOf<ArgumentException>());
                }
                Assert.False(Directory.Exists(_root));
            }
        }

        [UnityTest]
        public IEnumerator DisposingDownloaderLeavesAnInjectedHttpClientUsable()
        {
            using (var client = Client(_ => Bytes()))
            {
                new MNNModelDownloader(client).Dispose();
                var task = client.GetAsync("https://runtime-ownership.test/");
                yield return Until(task);
                AssertSucceeded(task);
                task.Result.Dispose();
            }
        }

        private static HttpClient Client(Func<HttpRequestMessage, HttpResponseMessage> response) => new HttpClient(new Handler(response));
        private static HttpResponseMessage Json(string body) => new HttpResponseMessage(HttpStatusCode.OK) {Content = new StringContent(body)};
        private static HttpResponseMessage Bytes() => new HttpResponseMessage(HttpStatusCode.OK) {Content = new ByteArrayContent(ModelBytes)};
        private static HttpResponseMessage ModelScopeFiles(string path, string hash)
            => Json("{\"Code\":200,\"Data\":{\"Files\":[{\"Path\":\"" + path + "\",\"Type\":\"blob\",\"Size\":" + ModelBytes.Length + ",\"Sha256\":\"" + hash + "\"}]}}");

        private static string Hash(byte[] bytes)
        {
            using (var algorithm = SHA256.Create())
                return BitConverter.ToString(algorithm.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        private static IEnumerator Until(Task task)
        {
            float deadline = Time.realtimeSinceStartup + 20;
            while (!task.IsCompleted && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.True(task.IsCompleted, "Timed out waiting for a deterministic HTTP fixture.");
        }

        private static void AssertSucceeded(Task task) => Assert.False(task.IsFaulted || task.IsCanceled, task.Exception?.ToString());

        private sealed class Handler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _response;
            internal Handler(Func<HttpRequestMessage, HttpResponseMessage> response) { _response = response; }
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                return Task.FromResult(_response(request));
            }
        }
    }
}
