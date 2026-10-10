using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MNN.Unity.Tests
{
    public class ModelDownloadServiceTests
    {
        private string _root;
        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetFullPath("TestArtifacts~/MNNValidation"), "MNNDownloadTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, true);
        }

        [UnityTest]
        public IEnumerator DownloadRepositoryAsync_WritesAllFilesAndVerifiesHashes()
        {
            var bytes = Encoding.UTF8.GetBytes("valid model bytes");
            var hash = ComputeSha256(bytes);
            var handler = new FakeHandler(request =>
            {
                if (request.RequestUri.AbsolutePath.EndsWith("/repo/files"))
                    return Json("{\"Code\":200,\"Data\":{\"Files\":[{" + "\"Path\":\"nested/model.mnn\",\"Type\":\"blob\",\"Size\":" + bytes.Length + ",\"Revision\":\"commit-123\",\"Sha256\":\"" + hash + "\"}]}}");
                Assert.That(request.RequestUri.Query, Does.Contain("Revision=commit-123"));
                return new HttpResponseMessage(HttpStatusCode.OK)
                {Content = new ByteArrayContent(bytes)};
            });
            using (var service = new MNNModelDownloadService(new HttpClient(handler), "https://unit.test"))
            {
                var task = service.DownloadRepositoryAsync("MNN/test", Path.Combine(_root, "staging"));
                yield return WaitForTask(task);
                Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
                Assert.AreEqual(bytes.Length, task.Result);
                CollectionAssert.AreEqual(bytes, File.ReadAllBytes(Path.Combine(_root, "staging", "nested", "model.mnn")));
            }
        }

        [UnityTest]
        public IEnumerator DownloadRepositoryAsync_HttpError_CleansStagingDirectory()
        {
            var handler = new FakeHandler(request => request.RequestUri.AbsolutePath.EndsWith("/repo/files") ? Json("{\"Code\":200,\"Data\":{\"Files\":[{\"Path\":\"bad.mnn\",\"Type\":\"blob\",\"Size\":3}]}}") : new HttpResponseMessage(HttpStatusCode.Forbidden)
            {Content = new StringContent("denied")});
            var staging = Path.Combine(_root, "staging");
            using (var service = new MNNModelDownloadService(new HttpClient(handler), "https://unit.test"))
            {
                var task = service.DownloadRepositoryAsync("MNN/test", staging);
                yield return WaitForTask(task);
                AssertTaskFailedWith<HttpRequestException>(task);
            }

            Assert.IsFalse(Directory.Exists(staging));
        }

        [UnityTest]
        public IEnumerator DownloadRepositoryAsync_SizeMismatch_CleansStagingDirectory()
        {
            var handler = new FakeHandler(request => request.RequestUri.AbsolutePath.EndsWith("/repo/files") ? Json("{\"Code\":200,\"Data\":{\"Files\":[{\"Path\":\"bad.mnn\",\"Type\":\"blob\",\"Size\":8}]}}") : new HttpResponseMessage(HttpStatusCode.OK)
            {Content = new ByteArrayContent(new byte[]{1, 2, 3})});
            var staging = Path.Combine(_root, "staging");
            using (var service = new MNNModelDownloadService(new HttpClient(handler), "https://unit.test"))
            {
                var task = service.DownloadRepositoryAsync("MNN/test", staging);
                yield return WaitForTask(task);
                AssertTaskFailedWith<IOException>(task);
            }

            Assert.IsFalse(Directory.Exists(staging));
        }

        [UnityTest]
        public IEnumerator DownloadRepositoryAsync_HashMismatch_CleansStagingDirectory()
        {
            var handler = new FakeHandler(request => request.RequestUri.AbsolutePath.EndsWith("/repo/files") ? Json("{\"Code\":200,\"Data\":{\"Files\":[{\"Path\":\"bad.mnn\",\"Type\":\"blob\",\"Size\":3,\"Sha256\":\"0000000000000000000000000000000000000000000000000000000000000000\"}]}}") : new HttpResponseMessage(HttpStatusCode.OK)
            {Content = new ByteArrayContent(new byte[]{1, 2, 3})});
            var staging = Path.Combine(_root, "staging");
            using (var service = new MNNModelDownloadService(new HttpClient(handler), "https://unit.test"))
            {
                var task = service.DownloadRepositoryAsync("MNN/test", staging);
                yield return WaitForTask(task);
                AssertTaskFailedWith<InvalidDataException>(task);
            }

            Assert.IsFalse(Directory.Exists(staging));
        }

        [UnityTest]
        public IEnumerator DownloadRepositoryAsync_PathTraversal_IsRejectedAndCleansStagingDirectory()
        {
            var handler = new FakeHandler(request => Json("{\"Code\":200,\"Data\":{\"Files\":[{\"Path\":\"../outside.mnn\",\"Type\":\"blob\",\"Size\":1}]}}"));
            var staging = Path.Combine(_root, "staging");
            using (var service = new MNNModelDownloadService(new HttpClient(handler), "https://unit.test"))
            {
                var task = service.DownloadRepositoryAsync("MNN/test", staging);
                yield return WaitForTask(task);
                AssertTaskFailedWith<InvalidDataException>(task);
            }

            Assert.IsFalse(Directory.Exists(staging));
        }

        [UnityTest]
        public IEnumerator DownloadRepositoryFileAsync_ParallelRanges_AreConcurrentAndMergeCorrectly()
        {
            var bytes = MakeModelBytes();
            var ranges = new ConcurrentBag<string>();
            var allStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var active = 0;
            var handler = new AsyncHandler(async (request, token) =>
            {
                var range = request.Headers.Range.Ranges.Single();
                ranges.Add(range.From + "-" + range.To);
                if (Interlocked.Increment(ref active) == 4)
                    allStarted.TrySetResult(true);
                await allStarted.Task.ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                return Partial(bytes, range.From.Value, range.To.Value);
            });
            using (var client = new HttpClient(handler))
            using (var service = new MNNModelDownloadService(client, "https://unit.test", new MNNModelDownloadOptions{MaxConnections = 4, ParallelThresholdBytes = 1}))
            {
                var destination = Path.Combine(_root, "parallel.mnn");
                var task = service.DownloadRepositoryFileAsync("MNN/test", ModelFile(bytes), destination);
                yield return WaitForTask(task);
                Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
                Assert.AreEqual(4, ranges.Count);
                CollectionAssert.AreEquivalent(new[]{"0-131071", "131072-262143", "262144-393215", "393216-524287"}, ranges);
                CollectionAssert.AreEqual(bytes, File.ReadAllBytes(destination));
            }
        }

        [UnityTest]
        public IEnumerator DownloadRepositoryFileAsync_CancelThenResume_RequestsOnlyRemainingBytes()
        {
            var bytes = MakeModelBytes();
            var requests = new ConcurrentBag<long>();
            var handler = new FakeHandler(request =>
            {
                var range = request.Headers.Range?.Ranges.Single();
                requests.Add(range?.From ?? 0);
                return range == null ? new HttpResponseMessage(HttpStatusCode.OK)
                {Content = new ByteArrayContent(bytes)} : Partial(bytes, range.From.Value, range.To.Value);
            });
            using (var client = new HttpClient(handler))
            using (var service = new MNNModelDownloadService(client, "https://unit.test", new MNNModelDownloadOptions{PreservePartialDownloads = true, MaxConnections = 1}))
            using (var cancellation = new CancellationTokenSource())
            {
                var destination = Path.Combine(_root, "resume.mnn");
                var file = ModelFile(bytes);
                var first = service.DownloadRepositoryFileAsync("MNN/test", file, destination, (received, total, name) =>
                {
                    if (received > 0)
                        cancellation.Cancel();
                }, cancellation.Token);
                yield return WaitForTask(first);
                Assert.IsTrue(first.IsCanceled);
                Assert.IsFalse(File.Exists(destination));
                var saved = new FileInfo(Path.Combine(destination + ".mnn-parts", "0.part")).Length;
                Assert.That(saved, Is.GreaterThan(0).And.LessThan(bytes.Length));
                var resumed = service.DownloadRepositoryFileAsync("MNN/test", file, destination);
                yield return WaitForTask(resumed);
                Assert.IsFalse(resumed.IsFaulted, resumed.Exception?.ToString());
                CollectionAssert.AreEquivalent(new[]{0L, saved}, requests);
                CollectionAssert.AreEqual(bytes, File.ReadAllBytes(destination));
                Assert.IsFalse(Directory.Exists(destination + ".mnn-parts"));
            }
        }

        [UnityTest]
        public IEnumerator DownloadRepositoryFileAsync_ParallelRangeIgnored_FallsBackToSingleRequest()
        {
            var bytes = MakeModelBytes();
            var fullRequests = 0;
            var handler = new FakeHandler(request =>
            {
                if (request.Headers.Range == null)
                    Interlocked.Increment(ref fullRequests);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {Content = new ByteArrayContent(bytes)};
            });
            using (var client = new HttpClient(handler))
            using (var service = new MNNModelDownloadService(client, "https://unit.test", new MNNModelDownloadOptions{MaxConnections = 4, ParallelThresholdBytes = 1}))
            {
                var destination = Path.Combine(_root, "fallback.mnn");
                var task = service.DownloadRepositoryFileAsync("MNN/test", ModelFile(bytes), destination);
                yield return WaitForTask(task);
                Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
                Assert.AreEqual(1, fullRequests);
                CollectionAssert.AreEqual(bytes, File.ReadAllBytes(destination));
            }
        }

        [UnityTest]
        public IEnumerator DownloadRepositoryFileAsync_ResumeRangeIgnored_OverwritesPartialFile()
        {
            var bytes = MakeModelBytes();
            var handler = new FakeHandler(request => new HttpResponseMessage(HttpStatusCode.OK)
            {Content = new ByteArrayContent(bytes)});
            using (var client = new HttpClient(handler))
            using (var service = new MNNModelDownloadService(client, "https://unit.test", new MNNModelDownloadOptions{PreservePartialDownloads = true, MaxConnections = 1}))
            using (var cancellation = new CancellationTokenSource())
            {
                var destination = Path.Combine(_root, "overwrite.mnn");
                var file = ModelFile(bytes);
                var first = service.DownloadRepositoryFileAsync("MNN/test", file, destination, (received, total, name) =>
                {
                    if (received > 0)
                        cancellation.Cancel();
                }, cancellation.Token);
                yield return WaitForTask(first);
                Assert.IsTrue(first.IsCanceled);
                var resumed = service.DownloadRepositoryFileAsync("MNN/test", file, destination);
                yield return WaitForTask(resumed);
                Assert.IsFalse(resumed.IsFaulted, resumed.Exception?.ToString());
                CollectionAssert.AreEqual(bytes, File.ReadAllBytes(destination));
            }
        }

        [UnityTest]
        public IEnumerator DownloadRepositoryFileAsync_InvalidContentRange_IsRejected()
        {
            var bytes = MakeModelBytes();
            var handler = new FakeHandler(request => Partial(bytes, 1, bytes.Length - 1));
            using (var client = new HttpClient(handler))
            using (var service = new MNNModelDownloadService(client, "https://unit.test", new MNNModelDownloadOptions{MaxConnections = 4, ParallelThresholdBytes = 1}))
            {
                var destination = Path.Combine(_root, "invalid.mnn");
                var task = service.DownloadRepositoryFileAsync("MNN/test", ModelFile(bytes), destination);
                yield return WaitForTask(task);
                AssertTaskFailedWith<InvalidDataException>(task);
                Assert.IsFalse(File.Exists(destination));
                Assert.IsFalse(Directory.Exists(destination + ".mnn-parts"));
            }
        }

        private static byte[] MakeModelBytes() => Enumerable.Range(0, 512 * 1024).Select(i => (byte)(i % 251)).ToArray();
        private static MNNModelRepository.ModelScopeFile ModelFile(byte[] bytes) => new MNNModelRepository.ModelScopeFile{Path = "model.mnn", Type = "blob", Size = bytes.Length, Revision = "test-commit", Sha256 = ComputeSha256(bytes)};
        private static HttpResponseMessage Partial(byte[] bytes, long start, long end)
        {
            var content = new ByteArrayContent(bytes.Skip((int)start).Take((int)(end - start + 1)).ToArray());
            content.Headers.ContentRange = new ContentRangeHeaderValue(start, end, bytes.Length);
            return new HttpResponseMessage(HttpStatusCode.PartialContent)
            {Content = content};
        }

        private sealed class AsyncHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _factory;
            public AsyncHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> factory)
            {
                _factory = factory;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => _factory(request, token);
        }

        [UnityTest, Explicit("Downloads a 566 KB LFS file from ModelScope CDN")]
        public IEnumerator DownloadRepositoryFileAsync_ModelScopeLfsRedirect_DownloadsCompleteFile()
        {
            var destination = Path.Combine(_root, "llm.mnn");
            const string modelScopeId = "MNN/Qwen2.5-0.5B-Instruct-MNN";
            using (var service = new MNNModelDownloadService())
            {
                var listingTask = service.GetRepositoryFilesAsync(modelScopeId);
                yield return WaitForTask(listingTask);
                Assert.IsFalse(listingTask.IsFaulted, listingTask.Exception?.ToString());
                var file = listingTask.Result.FirstOrDefault(item => item.Path == "llm.mnn");
                Assert.IsNotNull(file, "ModelScope file listing did not include llm.mnn.");
                var downloadTask = service.DownloadRepositoryFileAsync(modelScopeId, file, destination);
                yield return WaitForTask(downloadTask);
                Assert.IsFalse(downloadTask.IsFaulted, downloadTask.Exception?.ToString());
                Assert.AreEqual(file.Size, downloadTask.Result);
                Assert.AreEqual(file.Size, new FileInfo(destination).Length);
            }
        }

        private static HttpResponseMessage Json(string json)
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {Content = new StringContent(json, Encoding.UTF8, "application/json")};
        }

        private static string ComputeSha256(byte[] bytes)
        {
            using (var sha256 = SHA256.Create())
                return BitConverter.ToString(sha256.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        private static IEnumerator WaitForTask(Task task)
        {
            var timeout = DateTime.UtcNow.AddSeconds(120);
            while (!task.IsCompleted && DateTime.UtcNow < timeout)
                yield return null;
            Assert.IsTrue(task.IsCompleted, "Download did not finish within 120 seconds.");
        }

        private static void AssertTaskFailedWith<TException>(Task task)
            where TException : Exception
        {
            Assert.IsTrue(task.IsFaulted, "Expected " + typeof(TException).Name + " to be thrown.");
            Assert.IsInstanceOf<TException>(task.Exception.InnerException);
        }

        private sealed class FakeHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;
            public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
            {
                _responseFactory = responseFactory;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(_responseFactory(request));
            }
        }
    }
}
