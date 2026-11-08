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
