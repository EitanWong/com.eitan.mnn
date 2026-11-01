using System;
using System.Collections;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MNN.Unity.Tests
{
    public class ModelCatalogConcurrencyTests
    {
        [UnityTest]
        public IEnumerator ModelScopeOverlapsSearchesAndBoundsDeduplicatedManifestRequests()
        {
            var searches = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var manifests = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            int searchCount = 0, manifestCount = 0, active = 0, peak = 0;
            using (var client = new HttpClient(new Handler(async (request, token) =>
            {
                if (!request.RequestUri.AbsolutePath.Contains("repo/files"))
                {
                    Interlocked.Increment(ref searchCount);
                    await Released(searches.Task, token);
                    return Json("{\"success\":true,\"data\":{\"total_count\":20,\"models\":[" +
                        string.Join(",", Enumerable.Range(0, 20).Select(i => "{\"id\":\"community/model-mnn-" + i + "\"}")) + "]}}");
                }
                Interlocked.Increment(ref manifestCount);
                int current = Interlocked.Increment(ref active);
                lock (manifests) peak = Math.Max(peak, current);
                try
                {
                    await Released(manifests.Task, token);
                    return Json("{\"Code\":200,\"Data\":{\"Files\":[{\"Type\":\"blob\",\"Path\":\"model.mnn\",\"Size\":16}]}}");
                }
                finally { Interlocked.Decrement(ref active); }
            })))
            {
                var repository = new MNNModelRepository();
                var load = repository.DiscoverModelScopeModelsAsync(client, "https://catalog.unit.test");
                try
                {
                    yield return Until(() => Volatile.Read(ref searchCount) == 3);
                    Assert.False(load.IsCompleted, "All independent searches must start before any finishes.");
                    searches.SetResult(true);
                    yield return Until(() => Volatile.Read(ref active) == 8);
                    Assert.AreEqual(8, manifestCount, "The remaining manifests must wait for the concurrency budget.");
                    manifests.SetResult(true);
                    yield return Until(() => load.IsCompleted);
                    Assert.False(load.IsFaulted, load.Exception?.ToString());
                    Assert.AreEqual(20, manifestCount, "Overlapping search results must be unioned before reading manifests.");
                    Assert.AreEqual(20, repository.models.Count);
                    Assert.AreEqual(8, peak);
                }
                finally
                {
                    searches.TrySetResult(true);
                    manifests.TrySetResult(true);
                }
            }
        }

        [UnityTest]
        public IEnumerator ModelScopeFetchesRemainingSearchPagesConcurrently()
        {
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            int pending = 0;
            using (var client = new HttpClient(new Handler(async (request, token) =>
            {
                if (request.RequestUri.AbsolutePath.Contains("repo/files"))
                    return Json("{\"Code\":200,\"Data\":{\"Files\":[{\"Type\":\"blob\",\"Path\":\"model.mnn\"}]}}");
                string query = request.RequestUri.Query;
                if (!query.Contains("search=mnn"))
                    return SearchPage(0, 0, 0);
                int page = int.Parse(query.Split(new[] {"page_number="}, StringSplitOptions.None)[1]);
                if (page > 1)
                {
                    Interlocked.Increment(ref pending);
                    await Released(release.Task, token);
                }
                return SearchPage(page, 150, 50);
            })))
            {
                var repository = new MNNModelRepository();
                var load = repository.DiscoverModelScopeModelsAsync(client, "https://catalog.unit.test");
                try
                {
                    yield return Until(() => Volatile.Read(ref pending) == 2);
                    Assert.False(load.IsCompleted);
                    release.SetResult(true);
                    yield return Until(() => load.IsCompleted);
                    Assert.False(load.IsFaulted, load.Exception?.ToString());
                    Assert.AreEqual(150, repository.models.Count);
                }
                finally { release.TrySetResult(true); }
            }
        }

        [UnityTest]
        public IEnumerator ModelScopeDeduplicatesOverlappingSortedPages()
        {
            using (var client = new HttpClient(new Handler((request, token) =>
            {
                if (request.RequestUri.AbsolutePath.Contains("repo/files"))
                    return Task.FromResult(Json("{\"Code\":200,\"Data\":{\"Files\":[{\"Type\":\"blob\",\"Path\":\"model.mnn\"}]}}"));
                string query = request.RequestUri.Query;
                if (!query.Contains("search=mnn"))
                    return Task.FromResult(SearchPage(0, 0, 0));
                bool second = query.Contains("page_number=2");
                int start = second ? 49 : 0;
                return Task.FromResult(Json("{\"success\":true,\"data\":{\"total_count\":100,\"models\":[" +
                    string.Join(",", Enumerable.Range(start, 50).Select(i => "{\"id\":\"community/model-mnn-" + i + "\"}")) + "]}}"));
            })))
            {
                var repository = new MNNModelRepository();
                var load = repository.DiscoverModelScopeModelsAsync(client, "https://catalog.unit.test");
                yield return Until(() => load.IsCompleted);
                Assert.False(load.IsFaulted, load.Exception?.ToString());
                Assert.AreEqual(99, repository.models.Count);
            }
        }

        [UnityTest]
        public IEnumerator ModelScopeOfficialCatalogFetchesRemainingPagesConcurrently()
        {
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            int pending = 0;
            using (var client = new HttpClient(new Handler(async (request, token) =>
            {
                int page = int.Parse(request.RequestUri.Query.Split(new[] {"page_number="}, StringSplitOptions.None)[1]);
                if (page > 1)
                {
                    Interlocked.Increment(ref pending);
                    await Released(release.Task, token);
                }
                return SearchPage(page, 150, 50);
            })))
            {
                var catalog = new MNNModelCatalog();
                var load = catalog.LoadModelScopeModelsAsync(client, "https://catalog.unit.test");
                try
                {
                    yield return Until(() => Volatile.Read(ref pending) == 2);
                    release.SetResult(true);
                    yield return Until(() => load.IsCompleted);
                    Assert.False(load.IsFaulted, load.Exception?.ToString());
                    Assert.AreEqual(150, catalog.Models.Count);
                }
                finally { release.TrySetResult(true); }
            }
        }

        [UnityTest]
        public IEnumerator ModelScopeShortFirstPageDoesNotScheduleUnboundedRequests()
        {
            int requests = 0;
            using (var client = new HttpClient(new Handler((request, token) =>
            {
                int page = Interlocked.Increment(ref requests);
                return Task.FromResult(page == 1 ? SearchPage(1, 1000, 1) : SearchPage(0, 1000, 0));
            })))
            {
                var catalog = new MNNModelCatalog();
                var load = catalog.LoadModelScopeModelsAsync(client, "https://catalog.unit.test");
                yield return Until(() => load.IsCompleted);
                Assert.True(load.IsFaulted);
                Assert.AreEqual(2, requests, "A short first page must not fan out based on its one-item length.");
                Assert.IsEmpty(catalog.Models);
            }
        }

        [UnityTest]
        public IEnumerator HuggingFaceOverlapsQueriesAndCuratedMetadataWhileFollowingEachCursor()
        {
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            int initial = 0, pages = 0;
            using (var client = new HttpClient(new Handler(async (request, token) =>
            {
                bool next = request.RequestUri.Query.Contains("cursor=second");
                if (!next)
                {
                    Interlocked.Increment(ref initial);
                    await Released(release.Task, token);
                }
                if (request.RequestUri.AbsolutePath != "/api/models")
                {
                    string id = Uri.UnescapeDataString(request.RequestUri.AbsolutePath.Substring("/api/models/".Length))
                        .Split(new[] {"/revision/"}, StringSplitOptions.None)[0];
                    return Json(Model(id));
                }
                Interlocked.Increment(ref pages);
                var response = Json("[" + Model(next ? "community/second" : "community/shared") + "]");
                if (!next && request.RequestUri.Query.Contains("author=taobao-mnn"))
                    response.Headers.TryAddWithoutValidation("Link", "<https://catalog.unit.test/api/models?cursor=second>; rel=\"next\"");
                return response;
            })))
            {
                var repository = new MNNModelRepository();
                var pool = new MNNHuggingFaceMirrorPool(new[] {new MNNHuggingFaceMirror("Fixture", "https://catalog.unit.test")});
                var load = repository.DiscoverHuggingFaceModelsAsync(client, pool);
                try
                {
                    yield return Until(() => Volatile.Read(ref initial) == 3 + MNNModelRepository.FeaturedHuggingFaceRepositories.Length);
                    Assert.AreEqual(0, pages);
                    release.SetResult(true);
                    yield return Until(() => load.IsCompleted);
                    Assert.False(load.IsFaulted, load.Exception?.ToString());
                    Assert.AreEqual(4, pages);
                    Assert.AreEqual(2 + MNNModelRepository.FeaturedHuggingFaceRepositories.Length, repository.models.Count);
                    Assert.AreEqual(1, repository.models.Count(model => model.repositoryId == "community/shared"));
                    Assert.True(repository.models.Any(model => model.repositoryId == "community/second"));
                }
                finally { release.TrySetResult(true); }
            }
        }

        [UnityTest]
        public IEnumerator HuggingFaceLargeSearchPagesReturnMoreModelsPerRoundTrip()
        {
            int searches = 0;
            using (var client = new HttpClient(new Handler((request, token) =>
            {
                if (request.RequestUri.AbsolutePath != "/api/models")
                {
                    string id = Uri.UnescapeDataString(request.RequestUri.AbsolutePath.Substring("/api/models/".Length))
                        .Split(new[] {"/revision/"}, StringSplitOptions.None)[0];
                    return Task.FromResult(Json(Model(id)));
                }
                Interlocked.Increment(ref searches);
                int count = request.RequestUri.Query.Contains("limit=500") ? 250 : 100;
                return Task.FromResult(Json("[" + string.Join(",", Enumerable.Range(0, count)
                    .Select(i => Model("community/model-mnn-" + i))) + "]"));
            })))
            {
                var repository = new MNNModelRepository();
                var pool = new MNNHuggingFaceMirrorPool(new[] {new MNNHuggingFaceMirror("Fixture", "https://catalog.unit.test")});
                var load = repository.DiscoverHuggingFaceModelsAsync(client, pool);
                yield return Until(() => load.IsCompleted);
                Assert.False(load.IsFaulted, load.Exception?.ToString());
                Assert.AreEqual(3, searches);
                Assert.AreEqual(250 + MNNModelRepository.FeaturedHuggingFaceRepositories.Length, repository.models.Count);
            }
        }

        private static async Task Released(Task release, CancellationToken token)
        {
            var canceled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (token.Register(() => canceled.TrySetCanceled()))
            {
                await Task.WhenAny(release, canceled.Task);
                token.ThrowIfCancellationRequested();
                await release;
            }
        }

        private static IEnumerator Until(Func<bool> condition)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(10);
            while (!condition() && DateTime.UtcNow < deadline)
                yield return null;
            Assert.True(condition(), "Catalog concurrency fixture timed out.");
        }

        private static string Model(string id) => "{\"id\":\"" + id + "\",\"sha\":\"pinned\",\"siblings\":[{\"rfilename\":\"model.mnn\"}]}";
        private static HttpResponseMessage SearchPage(int page, int total, int count) => Json(
            "{\"success\":true,\"data\":{\"total_count\":" + total + ",\"models\":[" +
            string.Join(",", Enumerable.Range(0, count).Select(i => "{\"id\":\"community/model-mnn-" + ((page - 1) * 50 + i) + "\"}")) + "]}}");
        private static HttpResponseMessage Json(string body) => new HttpResponseMessage(HttpStatusCode.OK)
        {Content = new StringContent(body, Encoding.UTF8, "application/json")};

        private sealed class Handler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _send;
            internal Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) => _send = send;
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => _send(request, token);
        }
    }
}
