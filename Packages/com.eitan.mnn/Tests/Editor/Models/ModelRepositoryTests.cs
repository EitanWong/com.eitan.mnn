using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MNN.Unity.Tests
{
    public class ModelRepositoryTests
    {
        [UnityTest]
        public IEnumerator Catalog_IncludesUntaggedResourcesAndDeduplicatesPages()
        {
            var page = 0;
            using (var client = Client(request =>
            {
                StringAssert.Contains("owner=MNN", request.RequestUri.Query);
                StringAssert.DoesNotContain("filter.library", request.RequestUri.Query);
                StringAssert.Contains("page_number=" + (++page), request.RequestUri.Query);
                return page == 1 ? Page(3, "{\"id\":\"MNN/bert-vits2-MNN\",\"tasks\":[\"text-to-speech\"],\"tags\":[\"library:other\"]}", "{\"id\":\"MNN/stable-diffusion-v1-5-mnn\",\"tasks\":[\"text-generation\"]}") : Page(3, "{\"id\":\"MNN/stable-diffusion-v1-5-mnn\"}", "{\"id\":\"MNN/Qwen3.5-2B-Dflash\",\"tasks\":[\"text-generation\"],\"tags\":[\"library:safetensors\"]}");
            }))
            {
                var repository = new MNNModelRepository();
                var task = repository.LoadModelScopeModelsAsync(client, "https://unit.test");
                yield return Wait(task);
                Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
                Assert.AreEqual(2, page, "A short page must not stop a catalog with more results.");
                Assert.AreEqual(3, repository.models.Count);
                Assert.AreEqual("SpeechSynthesis", repository.models[0].category);
                Assert.AreEqual("ImageGenerationModel", repository.models[1].category);
                Assert.AreEqual("LargeLanguageModel", repository.models[2].category);
                Assert.IsTrue(repository.models.All(model => model.isRepository));
                Assert.AreEqual(1, repository.SearchModels("DFLASH").Count);
                StringAssert.Contains("Recursive=true", repository.models[0].downloadUrl);
            }
        }

        [UnityTest]
        public IEnumerator Catalog_FetchesBeyondTwentyPages()
        {
            var page = 0;
            using (var client = Client(request =>
            {
                var offset = page++ * 50;
                return Page(1051, Enumerable.Range(offset, Math.Min(50, 1051 - offset)).Select(index => "{\"id\":\"MNN/model-" + index + "\"}").ToArray());
            }))
            {
                var repository = new MNNModelRepository();
                var task = repository.LoadModelScopeModelsAsync(client);
                yield return Wait(task);
                Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
                Assert.AreEqual(22, page);
                Assert.AreEqual(1051, repository.models.Count);
            }
        }

        [UnityTest]
        public IEnumerator Catalog_WithoutTotal_ContinuesUntilEmptyPage()
        {
            var page = 0;
            using (var client = Client(request => ++page == 1 ? Page(0, "{\"id\":\"MNN/test\"}") : Page(0)))
            {
                var repository = new MNNModelRepository();
                var task = repository.LoadModelScopeModelsAsync(client);
                yield return Wait(task);
                Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
                Assert.AreEqual(2, page);
                Assert.AreEqual(1, repository.models.Count);
            }
        }

        [UnityTest]
        public IEnumerator Catalog_RepeatedOrTruncatedPages_KeepPreviousCatalog()
        {
            foreach (var repeat in new[]{false, true})
            {
                var page = 0;
                using (var client = Client(request => ++page == 1 || repeat ? Page(2, "{\"id\":\"MNN/test\"}") : Page(2)))
                {
                    var repository = new MNNModelRepository();
                    var original = repository.models;
                    original.Add(new MNNModelInfo{id = "MNN/previous"});
                    var task = repository.LoadModelScopeModelsAsync(client);
                    yield return Wait(task);
                    Assert.IsTrue(task.IsFaulted);
                    Assert.IsInstanceOf<InvalidOperationException>(task.Exception.InnerException);
                    Assert.AreSame(original, repository.models);
                    Assert.AreEqual(2, page);
                }
            }
        }

        [UnityTest]
        public IEnumerator Catalog_ApiFailureOrCancellation_KeepPreviousCatalog()
        {
            using (var client = Client(request => "{\"success\":false,\"data\":{\"models\":[],\"total_count\":0}}"))
            {
                var repository = new MNNModelRepository();
                var original = repository.models;
                var task = repository.LoadModelScopeModelsAsync(client);
                yield return Wait(task);
                Assert.IsTrue(task.IsFaulted);
                Assert.AreSame(original, repository.models);
                using (var cancellation = new CancellationTokenSource())
                {
                    cancellation.Cancel();
                    task = repository.LoadModelScopeModelsAsync(client, cancellationToken: cancellation.Token);
                    yield return Wait(task);
                    Assert.IsTrue(task.IsCanceled);
                    Assert.AreSame(original, repository.models);
                }
            }
        }

        [UnityTest]
        public IEnumerator Catalog_ClassifiesResourceTasksAndKeepsOtherModelsSearchable()
        {
            using (var client = Client(request => Page(4, "{\"id\":\"MNN/bge-reranker-MNN\"}", "{\"id\":\"MNN/gte_sentence-embedding_multilingual-base-MNN\",\"tasks\":[\"sentence-embedding\"]}", "{\"id\":\"MNN/piper-voices-MNN\",\"tasks\":[\"text-to-speech\"]}", "{\"id\":\"MNN/TaoAvatar-NNR-MNN\",\"tasks\":[\"face-reconstruction\"]}")))
            {
                var repository = new MNNModelRepository();
                var task = repository.LoadModelScopeModelsAsync(client);
                yield return Wait(task);
                Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
                CollectionAssert.AreEqual(new[]{"Reranker", "EmbeddingModel", "SpeechSynthesis", "Other"}, repository.models.Select(model => model.category).ToArray());
                Assert.AreEqual(1, repository.GetModelsByCategory(MNNModelCategory.Other).Count);
                Assert.AreEqual(1, repository.SearchModels("TaoAvatar").Count);
            }
        }

        [UnityTest, Explicit("Reads the live organization catalog and every repository manifest; downloads two small metadata files only.")]
        public IEnumerator LiveCatalog_AllRepositoriesHaveDownloadableFiles()
        {
            var task = AuditLiveCatalogAsync();
            yield return Wait(task, 600);
            Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
        }

        private static async Task AuditLiveCatalogAsync()
        {
            var output = Path.GetFullPath("TestArtifacts~/ModelCatalog/LiveAudit");
            Directory.CreateDirectory(output);
            var repository = new MNNModelRepository();
            using (var client = new HttpClient{Timeout = TimeSpan.FromSeconds(45)})
            using (var service = new MNNModelDownloadService(client))
            using (var connections = new SemaphoreSlim(4))
            {
                await repository.LoadModelScopeModelsAsync(client);
                File.WriteAllText(Path.Combine(output, "catalog.json"), JsonUtility.ToJson(repository, true));
                // Representative omissions from the linked official collections.
                foreach (var id in new[]{"MNN/bert-vits2-MNN", "MNN/piper-voices-MNN", "MNN/TaoAvatar-NNR-MNN", "MNN/stable-diffusion-v1-5-mnn", "MNN/gte_sentence-embedding_multilingual-base-MNN", "MNN/Qwen3.5-2B-Dflash"})
                    Assert.IsTrue(repository.models.Any(model => model.modelScopeId == id), "Missing " + id);
                var results = await Task.WhenAll(repository.models.Select(async model =>
                {
                    await connections.WaitAsync();
                    try
                    {
                        var files = await service.GetRepositoryFilesAsync(model.modelScopeId);
                        Assert.IsTrue(files.Any(file => file.Type == "blob"), "Empty repository: " + model.modelScopeId);
                        // Check the transfer route for newly included repositories without fetching weights.
                        if (model.name == "gte_sentence-embedding_multilingual-base-MNN" || model.name == "stable-diffusion-v1-5-mnn")
                        {
                            var file = files.First(item => item.Type == "blob" && (item.Path.EndsWith(".json") || item.Path.EndsWith(".md") || item.Path.EndsWith(".txt")) && item.Size > 0 && item.Size < 16384);
                            var size = await service.DownloadRepositoryFileAsync(model.modelScopeId, file, Path.Combine(output, model.name, file.Path));
                            Assert.AreEqual(file.Size, size);
                        }

                        return model.modelScopeId + "\t" + files.Count(file => file.Type == "blob") + "\t" + files.Sum(file => Math.Max(0, file.Size));
                    }
                    finally
                    {
                        connections.Release();
                    }
                }));
                File.WriteAllLines(Path.Combine(output, "manifests.tsv"), new[]{"repository\tfiles\tbytes"}.Concat(results));
            }
        }

        private static string Page(int total, params string[] models) => "{\"success\":true,\"data\":{\"total_count\":" + total + ",\"models\":[" + string.Join(",", models) + "]}}";
        private static HttpClient Client(Func<HttpRequestMessage, string> response) => new HttpClient(new Handler(response));
        private sealed class Handler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, string> _response;
            public Handler(Func<HttpRequestMessage, string> response)
            {
                _response = response;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {Content = new StringContent(_response(request), Encoding.UTF8, "application/json")});
        }

        private static IEnumerator Wait(Task task, int seconds = 30)
        {
            var deadline = DateTime.UtcNow.AddSeconds(seconds);
            while (!task.IsCompleted && DateTime.UtcNow < deadline)
                yield return null;
            Assert.IsTrue(task.IsCompleted, "Catalog request timed out.");
        }
    }
}
