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

        [UnityTest]
        public IEnumerator PublishedSpeechRepositoryKeepsCompleteDeploymentFiles()
        {
            var bytes = Encoding.UTF8.GetBytes("speech deployment file");
            var hash = ComputeSha256(bytes);
            string[] paths = {"model.mnn", "config.json", "llm_config.json", "tokenizer.json", "mnn_turnsense.json", "NOTICE.txt", "constants.bin"};
            string listing = "{\"Code\":200,\"Data\":{\"Files\":[" + string.Join(",", paths.Select(path =>
                "{\"Path\":\"" + path + "\",\"Type\":\"blob\",\"Size\":" + bytes.Length + ",\"Sha256\":\"" + hash + "\"}")) + "]}}";
            var handler = new FakeHandler(request => request.RequestUri.AbsolutePath.EndsWith("/repo/files")
                ? Json(listing) : new HttpResponseMessage(HttpStatusCode.OK) {Content = new ByteArrayContent(bytes)});
            string staging = Path.Combine(_root, "speech");
            using (var service = new MNNModelDownloadService(new HttpClient(handler), "https://unit.test"))
            {
                var task = service.DownloadRepositoryAsync("EitanWang/TurnSense-SmolLM2-135M-MNN", staging);
                yield return WaitForTask(task);
                Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
            }
            foreach (var path in paths)
                CollectionAssert.AreEqual(bytes, File.ReadAllBytes(Path.Combine(staging, path)), path);
        }
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
        public IEnumerator DownloadRepositoryAsync_GenericMnnModelSkipsConversionInputsAndDatasets()
        {
            var bytes = Encoding.UTF8.GetBytes("required inference asset");
            var hash = ComputeSha256(bytes);
            var requested = new ConcurrentBag<string>();
            Func<string, bool, string> file = (path, required) => required
                ? "{\"Path\":\"" + path + "\",\"Type\":\"blob\",\"Size\":" + bytes.Length + ",\"Sha256\":\"" + hash + "\"}"
                : "{\"Path\":\"" + path + "\",\"Type\":\"blob\",\"Size\":999999}";
            string listing = "{\"Code\":200,\"Data\":{\"Files\":[" + string.Join(",", new[]{
                file("llm.mnn", true), file("llm.mnn.weight", true), file("llm_config.json", true),
                file("tokenizer.txt", true), file("config.json", true), file("pytorch_model.safetensors", false),
                file("training-data.jsonl", false)}) + "]}}";
            var handler = new FakeHandler(request =>
            {
                if (request.RequestUri.AbsolutePath.EndsWith("/repo/files"))
                    return Json(listing);
                string path = Uri.UnescapeDataString(request.RequestUri.Query).Split(new[]{"FilePath="}, StringSplitOptions.None).Last();
                requested.Add(path);
                Assert.That(path, Does.Not.Contain("safetensors").And.Not.Contain("training-data"));
                return new HttpResponseMessage(HttpStatusCode.OK){Content = new ByteArrayContent(bytes)};
            });
            string staging = Path.Combine(_root, "generic");
            using (var service = new MNNModelDownloadService(new HttpClient(handler), "https://unit.test"))
            {
                var task = service.DownloadRepositoryAsync("MNN/generic", staging);
                yield return WaitForTask(task);
                Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
                Assert.AreEqual(bytes.Length * 5, task.Result);
            }
            CollectionAssert.AreEquivalent(new[]{"llm.mnn", "llm.mnn.weight", "llm_config.json", "tokenizer.txt", "config.json"}, requested.ToArray());
            Assert.IsFalse(File.Exists(Path.Combine(staging, "pytorch_model.safetensors")));
            Assert.IsFalse(File.Exists(Path.Combine(staging, "training-data.jsonl")));
        }

        [UnityTest, Explicit("Downloads the small official MobileLLM MNN profile from ModelScope; prepared weights are required by the Studio smoke test.")]
        public IEnumerator ModelScopeMobileLlmDownloadsOnlyItsRuntimeProfile()
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_MOBILE_LLM_ROOT");
            if (string.IsNullOrWhiteSpace(root)) Assert.Ignore("Set MNN_TEST_MOBILE_LLM_ROOT to a directory under TestArtifacts~.");
            root = Path.GetFullPath(root);
            string artifacts = Path.GetFullPath("TestArtifacts~") + Path.DirectorySeparatorChar;
            Assert.That(root, Does.StartWith(artifacts), "Model downloads must stay under TestArtifacts~.");
            using (var service = new MNNModelDownloadService())
            {
                var task = service.DownloadRepositoryAsync("MNN/MobileLLM-125M-MNN", root);
                yield return WaitForTask(task);
                Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
                TestContext.WriteLine("Downloaded verified runtime bytes: " + task.Result);
            }
            foreach (string file in new[]{"config.json", "configuration.json", "embeddings_bf16.bin", "llm.mnn", "llm.mnn.json", "llm.mnn.weight", "llm_config.json", "tokenizer.txt", "README.md"})
                Assert.True(File.Exists(Path.Combine(root, file)), "Required runtime file: " + file);
            Assert.IsFalse(File.Exists(Path.Combine(root, ".gitattributes")), "Repository bookkeeping is excluded by the generic runtime filter.");
        }

        [UnityTest, Explicit("Downloads the small official SmolLM2 MNN profile from ModelScope; prepared weights are required by the Studio smoke test.")]
        public IEnumerator ModelScopeSmolLlmDownloadsOnlyItsRuntimeProfile()
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_SMOL_LLM_ROOT");
            if (string.IsNullOrWhiteSpace(root)) Assert.Ignore("Set MNN_TEST_SMOL_LLM_ROOT to a directory under TestArtifacts~/.");
            root = Path.GetFullPath(root);
            string artifacts = Path.GetFullPath("TestArtifacts~") + Path.DirectorySeparatorChar;
            Assert.That(root, Does.StartWith(artifacts), "Model downloads must stay under TestArtifacts~.");
            using (var service = new MNNModelDownloadService())
            {
                var task = service.DownloadRepositoryAsync("MNN/SmolLM2-135M-Instruct-MNN", root);
                yield return WaitForTask(task);
                Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
                TestContext.WriteLine("Downloaded verified runtime bytes: " + task.Result);
            }
            foreach (string file in new[]{"config.json", "configuration.json", "embeddings_bf16.bin", "llm.mnn", "llm.mnn.json", "llm.mnn.weight", "llm_config.json", "tokenizer.txt", "README.md"})
                Assert.True(File.Exists(Path.Combine(root, file)), "Required runtime file: " + file);
            Assert.IsFalse(File.Exists(Path.Combine(root, ".gitattributes")), "Repository bookkeeping is excluded by the generic runtime filter.");
        }

        [UnityTest, Explicit("Downloads the official Qwen2.5-0.5B MNN profile from ModelScope; prepared weights are required by the Studio smoke test.")]
        public IEnumerator ModelScopeQwen25SmallDownloadsOnlyItsRuntimeProfile()
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_QWEN25_LLM_ROOT");
            if (string.IsNullOrWhiteSpace(root)) Assert.Ignore("Set MNN_TEST_QWEN25_LLM_ROOT to a directory under TestArtifacts~/.");
            root = Path.GetFullPath(root);
            string artifacts = Path.GetFullPath("TestArtifacts~") + Path.DirectorySeparatorChar;
            Assert.That(root, Does.StartWith(artifacts), "Model downloads must stay under TestArtifacts~.");
            using (var service = new MNNModelDownloadService())
            {
                var task = service.DownloadRepositoryAsync("MNN/Qwen2.5-0.5B-Instruct-MNN", root);
                yield return WaitForTask(task);
                Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
                TestContext.WriteLine("Downloaded verified runtime bytes: " + task.Result);
            }
            foreach (string file in new[]{"config.json", "configuration.json", "embeddings_bf16.bin", "llm.mnn", "llm.mnn.json", "llm.mnn.weight", "llm_config.json", "tokenizer.txt", "README.md"})
                Assert.True(File.Exists(Path.Combine(root, file)), "Required runtime file: " + file);
            Assert.IsFalse(File.Exists(Path.Combine(root, ".gitattributes")), "Repository bookkeeping is excluded by the generic runtime filter.");
        }

        [UnityTest, Explicit("Downloads the official Qwen3-0.6B MNN profile from ModelScope; prepared weights are required by the Studio smoke test.")]
        public IEnumerator ModelScopeQwen3SmallDownloadsOnlyItsRuntimeProfile()
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_QWEN3_LLM_ROOT");
            if (string.IsNullOrWhiteSpace(root)) Assert.Ignore("Set MNN_TEST_QWEN3_LLM_ROOT to a directory under TestArtifacts~/.");
            root = Path.GetFullPath(root);
            string artifacts = Path.GetFullPath("TestArtifacts~") + Path.DirectorySeparatorChar;
            Assert.That(root, Does.StartWith(artifacts), "Model downloads must stay under TestArtifacts~/.");
            using (var service = new MNNModelDownloadService())
            {
                var task = service.DownloadRepositoryAsync("MNN/Qwen3-0.6B-MNN", root);
                yield return WaitForTask(task);
                Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
                TestContext.WriteLine("Downloaded verified runtime bytes: " + task.Result);
            }
            foreach (string file in new[]{"config.json", "configuration.json", "llm.mnn", "llm.mnn.weight", "llm_config.json", "tokenizer.txt", "README.md"})
                Assert.True(File.Exists(Path.Combine(root, file)), "Required runtime file: " + file);
            Assert.IsFalse(File.Exists(Path.Combine(root, ".gitattributes")), "Repository bookkeeping is excluded by the generic runtime filter.");
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
        public IEnumerator HuggingFaceDownload_FallsBackAcrossMetadataAndFileMirrors()
        {
            var bytes = Encoding.UTF8.GetBytes("hf model bytes");
            var hash = ComputeSha256(bytes);
            var fileHosts = new ConcurrentBag<string>();
            var handler = new FakeHandler(request =>
            {
                if (request.RequestUri.Host == "api-down.test")
                    return new HttpResponseMessage(HttpStatusCode.Forbidden);
                if (request.RequestUri.Host == "api-up.test")
                    return Json("{\"sha\":\"commit-1\",\"siblings\":[{\"rfilename\":\"nested/model.mnn\",\"size\":" + bytes.Length + ",\"lfs\":{\"sha256\":\"" + hash + "\"}}]}");
                fileHosts.Add(request.RequestUri.Host);
                if (request.RequestUri.Host == "file-up.test" || request.RequestUri.Host == "file-down.test")
                    return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
                return new HttpResponseMessage(HttpStatusCode.OK){Content = new ByteArrayContent(bytes)};
            });
            var pool = new MNNHuggingFaceMirrorPool(new[]
            {
                new MNNHuggingFaceMirror("API first", "https://api-down.test", "https://file-down.test"),
                new MNNHuggingFaceMirror("API second", "https://api-up.test", "https://file-up.test"),
                new MNNHuggingFaceMirror("CDN only", "https://cdn.test", "https://cdn.test", false)
            });
            using (var client = new HttpClient(handler))
            using (var service = new MNNModelDownloadService(client, options: new MNNModelDownloadOptions{RetryCount = 1}))
            {
                var staging = Path.Combine(_root, "hf-staging");
                var task = service.DownloadHuggingFaceRepositoryAsync("org/model", staging, "commit-1", mirrorPool: pool);
                yield return WaitForTask(task);
                Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
                Assert.AreEqual(bytes.Length, task.Result);
                CollectionAssert.AreEqual(bytes, File.ReadAllBytes(Path.Combine(staging, "nested", "model.mnn")));
                CollectionAssert.AreEquivalent(new[]{"file-up.test", "cdn.test"}, fileHosts.ToArray());
                Assert.IsFalse(pool.GetApiCandidates().Any(mirror => mirror.Name == "CDN only"));
            }
        }

        [UnityTest]
        public IEnumerator ReferenceSpeechDownloadUsesPinnedCrossRepositorySourcesAndRejectsFailedTransfer()
        {
            var requests = new ConcurrentBag<string>();
            var handler = new FakeHandler(request =>
            {
                requests.Add(request.RequestUri.AbsolutePath);
                return new HttpResponseMessage(HttpStatusCode.Forbidden);
            });
            var pool = new MNNHuggingFaceMirrorPool(new[]{new MNNHuggingFaceMirror("fixture", "https://hf.test", "https://hf.test")});
            string staging = Path.Combine(_root, "reference-profile");
            using (var client = new HttpClient(handler))
            using (var service = new MNNModelDownloadService(client, options: new MNNModelDownloadOptions{RetryCount = 0}))
            {
                var task = service.DownloadHuggingFaceRepositoryAsync(MNNReferenceSpeechModel.FireRedRepository, staging, mirrorPool: pool);
                yield return WaitForTask(task);
                AssertTaskFailedWith<HttpRequestException>(task);
            }
            Assert.False(Directory.Exists(staging));
            Assert.False(requests.Any(path => path.StartsWith("/api/")), "Fixed profiles must not depend on mutable listings.");
            Assert.True(requests.Any(path => path.Contains("/yunfengwang/fireredtts3-mnn/resolve/" + MNNReferenceSpeechModel.FireRedRevision + "/redae_encoder.mnn")));
            Assert.True(requests.Any(path => path.Contains("/FireRedTeam/FireRedTTS3/resolve/dcf1bdcd1b8b25b382fa84c3e34eb82e3054a610/text_tokenizer/tokenizer.json")));
        }

        [UnityTest]
        public IEnumerator CosyVoiceDownloadPinsRevisionAndSkipsEnrollment()
        {
            var bytes = Encoding.UTF8.GetBytes("archive fixture");
            var paths = new ConcurrentBag<string>();
            var handler = new FakeHandler(request =>
            {
                paths.Add(request.RequestUri.AbsolutePath);
                if (request.RequestUri.AbsolutePath.StartsWith("/api/", StringComparison.Ordinal))
                {
                    Assert.That(request.RequestUri.AbsolutePath, Does.EndWith("/revision/" + MNNCosyVoiceModel.Revision));
                    return Json("{\"siblings\":[{\"rfilename\":\"" + MNNCosyVoiceModel.Archive + "\",\"size\":1,\"lfs\":{\"size\":" + bytes.Length + ",\"sha256\":\"" + ComputeSha256(bytes) + "\"}},{\"rfilename\":\"enrollment.zip\",\"size\":1000},{\"rfilename\":\"demo.apk\",\"size\":1000}]}");
                }
                Assert.That(request.RequestUri.AbsolutePath, Does.Contain("/resolve/" + MNNCosyVoiceModel.Revision + "/"));
                return new HttpResponseMessage(HttpStatusCode.OK){Content = new ByteArrayContent(bytes)};
            });
            var pool = new MNNHuggingFaceMirrorPool(new[]{new MNNHuggingFaceMirror("fixture", "https://hf.test", "https://hf.test")});
            using (var client = new HttpClient(handler))
            using (var service = new MNNModelDownloadService(client))
            {
                var task = service.DownloadHuggingFaceRepositoryAsync(MNNCosyVoiceModel.Repository, _root, mirrorPool: pool);
                yield return WaitForTask(task);
                Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
                Assert.AreEqual(bytes.Length, task.Result);
                Assert.AreEqual(2, paths.Count);
                CollectionAssert.AreEqual(bytes, File.ReadAllBytes(Path.Combine(_root, MNNCosyVoiceModel.Archive)));
            }
        }

        [UnityTest]
        public IEnumerator CosyVoiceMissingCompleteArchiveFailsBeforeAnyFileDownload()
        {
            int calls = 0;
            var handler = new FakeHandler(request =>
            {
                Interlocked.Increment(ref calls);
                return Json("{\"siblings\":[{\"rfilename\":\"enrollment.zip\",\"size\":1000}]}");
            });
            var pool = new MNNHuggingFaceMirrorPool(new[]{new MNNHuggingFaceMirror("fixture", "https://hf.test", "https://hf.test")});
            using (var client = new HttpClient(handler))
            using (var service = new MNNModelDownloadService(client))
            {
                var task = service.DownloadHuggingFaceRepositoryAsync(MNNCosyVoiceModel.Repository, Path.Combine(_root, "staging"), mirrorPool: pool);
                yield return WaitForTask(task);
                AssertTaskFailedWith<InvalidDataException>(task);
                Assert.AreEqual(1, calls);
            }
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
