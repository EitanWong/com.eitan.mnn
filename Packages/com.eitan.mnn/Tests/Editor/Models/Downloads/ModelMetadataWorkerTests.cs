using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class ModelMetadataWorkerTests
    {
        private sealed class MetadataHandler : HttpMessageHandler
        {
            internal string Body;
            internal HttpStatusCode Status = HttpStatusCode.OK;
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
                => Task.FromResult(new HttpResponseMessage(Status) { Content = new StringContent(Body) });
        }

        [Test]
        public void OnlineCatalogPersistsAndReopensOnWorkerWithoutUnityRuntime()
        {
            string folder = Path.GetFullPath("TestArtifacts~/ModelCatalog/WorkerTests/" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "online.json");
            try
            {
                var cache = new MNNOnlineModelCache(path, ct => Task.FromResult(new MNNOnlineModelCache.Discovery
                {
                    Succeeded = true,
                    Models = new[] { new MNNRemoteModel { id = "MNN/test", modelScopeId = "MNN/test", isRepository = true, name = "语音", fileSize = 4294967300L,
                        inputSpec = new MNNModelInputSpec { shape = new[] { 1, 80 } } } },
                    ModelScopeNextPage = 2, Warnings = new[] { "fixture" }
                }));
                Task.Run(() => cache.LoadAsync(true)).GetAwaiter().GetResult();
                var reopened = Task.Run(() => new MNNOnlineModelCache(path).Read()).GetAwaiter().GetResult();
                Assert.AreEqual("语音", reopened.models[0].name);
                Assert.AreEqual(4294967300L, reopened.models[0].fileSize);
                CollectionAssert.AreEqual(new[] { 1, 80 }, reopened.models[0].inputSpec.shape);
                Assert.AreEqual(2, reopened.modelScopeNextPage);
                File.WriteAllText(path, "{\"schemaVersion\":3,\"updatedUtcTicks\":" + DateTime.UtcNow.Ticks + ",\"models\":[{\"id\":\"MNN/test\",\"modelScopeId\":\"MNN/test\",\"isRepository\":true,\"name\":\"legacy\"}],\"warnings\":[]}");
                var legacy = Task.Run(() => new MNNOnlineModelCache(path).Read()).GetAwaiter().GetResult();
                Assert.AreEqual("legacy", legacy.models[0].name, "Existing snapshots may omit optional fields.");
            }
            finally { Directory.Delete(folder, true); }
        }

        [Test]
        public void CatalogProvidersDecodeOnWorkerWithoutUnityRuntime()
        {
            var handler = new MetadataHandler();
            using (var client = new HttpClient(handler))
            {
                var catalog = new MNNModelCatalog();
                var pool = new MNNHuggingFaceSourcePool(new[] { new MNNHuggingFaceSource("fixture", "https://catalog.test") });
                handler.Body = "{\"id\":\"test/voice\",\"sha\":\"pinned\",\"siblings\":[{\"rfilename\":\"model.mnn\",\"size\":42}]}";
                var model = Task.Run(() => catalog.LoadHuggingFaceModelAsync("test/voice", client, pool)).GetAwaiter().GetResult();
                Assert.AreEqual("test/voice", model.repositoryId);
                Assert.AreEqual("pinned", model.revision);
                handler.Body = "[" + handler.Body + "]";
                var page = Task.Run(() => catalog.FetchHuggingFacePageAsync(null, client, pool)).GetAwaiter().GetResult();
                Assert.AreEqual("test/voice", page.Models[0].repositoryId);
                handler.Body = "{\"success\":true,\"data\":{\"models\":[],\"total_count\":0}}";
                var empty = Task.Run(() => catalog.FetchModelScopePageAsync(1, client, "https://scope.test")).GetAwaiter().GetResult();
                Assert.AreEqual(0, empty.Models.Count);
            }
        }

        [Test]
        public void RepositoryMetadataDecodesOnWorkerWithoutUnityRuntime()
        {
            var handler = new MetadataHandler { Body = "{\"Code\":200,\"Data\":{\"Files\":[{\"Path\":\"语音/model.mnn\",\"Type\":\"blob\",\"Size\":4294967300,\"Revision\":\"pinned\",\"Sha256\":\"abc\"}]}}" };
            using (var client = new HttpClient(handler))
            using (var downloader = new MNNModelDownloader(client, "https://metadata.test"))
            {
                var files = Task.Run(() => downloader.GetRepositoryFilesAsync("MNN/test")).GetAwaiter().GetResult();
                Assert.AreEqual(1, files.Length);
                Assert.AreEqual("语音/model.mnn", files[0].Path);
                Assert.AreEqual(4294967300L, files[0].Size);
                Assert.AreEqual("pinned", files[0].Revision);
                Assert.AreEqual("abc", files[0].Sha256);
                Assert.IsNull(files[0].SourceRepository, "Optional metadata fields may be absent.");
            }
        }

        [Test]
        public void RepositoryMetadataRejectsInvalidEnvelopesAndUnavailableRepositories()
        {
            var handler = new MetadataHandler();
            using (var client = new HttpClient(handler))
            using (var downloader = new MNNModelDownloader(client, "https://metadata.test"))
            {
                foreach (string body in new[] { "{", "{\"Code\":500}", "{\"Code\":200,\"Data\":{}}" })
                {
                    handler.Body = body;
                    Assert.Throws<InvalidDataException>(() => Task.Run(() => downloader.GetRepositoryFilesAsync("MNN/test")).GetAwaiter().GetResult());
                }
                handler.Body = "unavailable"; handler.Status = HttpStatusCode.NotFound;
                Assert.Throws<MNNModelDownloader.RepositoryUnavailableException>(() => Task.Run(() => downloader.GetRepositoryFilesAsync("MNN/test")).GetAwaiter().GetResult());
            }
        }
    }
}
