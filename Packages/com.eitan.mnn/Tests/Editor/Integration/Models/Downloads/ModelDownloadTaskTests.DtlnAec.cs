using System;
using System.Collections;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MNN.Unity.Tests
{
    public partial class ModelDownloadTaskTests
    {
        [UnityTest, Explicit("Installs all three existing verified DTLN pairs through local HTTP and verifies the manager's preparation; no external downloads.")]
        public IEnumerator DtlnAecManagerInstallsAllVariantsAndReusesVerifiedFiles()
        {
            foreach (int units in MNNDtlnAecModel.Variants)
            {
                string repository = MNNDtlnAecModel.Repository(units);
                var requested = new ConcurrentBag<string>();
                _client = new HttpClient(new Handler((request, token) =>
                {
                    Assert.AreEqual("modelscope.test", request.RequestUri.Host);
                    Assert.AreEqual("/api/v1/models/" + repository + "/repo", request.RequestUri.AbsolutePath);
                    var query = request.RequestUri.Query;
                    Assert.That(query, Does.Contain("Revision=" + MNNDtlnAecModel.Revision(units)));
                    string path = Uri.UnescapeDataString(query.Substring(query.IndexOf("FilePath=", StringComparison.Ordinal) + 9));
                    Assert.True(MNNModelDownloadService.DtlnAecDownloadPlan(repository).Any(file => file.Path == path));
                    requested.Add(path);
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {Content = new StreamContent(File.OpenRead(Path.Combine(DtlnAecTestData.ModelDirectory(units), path)))});
                }, defaultManifest: false));
                _service = new MNNModelDownloadService(_client, "https://modelscope.test", new MNNModelDownloadOptions {PreservePartialDownloads = true, MaxConnections = 1});
                ServiceField.SetValue(_manager, _service);
                string staging = Path.Combine(_root, "staging-" + units);
                Directory.CreateDirectory(staging);
                File.WriteAllText(Path.Combine(staging, ".mnn-download-manifest.json"), "{\"Files\":[{\"Path\":\"original.tflite\",\"Type\":\"blob\"}]}");
                _job = _manager.StartDownload(new MNNModelInfo {provider = MNNModelProvider.ModelScope, repositoryId = repository, name = "renamed-" + units}, staging, Path.Combine(_root, "renamed-" + units));
                var deadline = DateTime.UtcNow.AddMinutes(2);
                while (_job.IsRunning && DateTime.UtcNow < deadline) yield return null;
                Assert.AreEqual(MNNDownloadState.Completed, _job.State, _job.Error);
                Assert.True(MNNDtlnAecModel.IsComplete(_job.InstalledPath));
                CollectionAssert.AreEquivalent(MNNModelDownloadService.DtlnAecDownloadPlan(repository).Select(file => file.Path), requested);
                Assert.AreEqual(5, Directory.GetFiles(_job.InstalledPath).Length, "Two graphs, license, generated profile and installation marker only.");
                int count = requested.Count;
                _service.DownloadRepositoryAsync(repository, _job.InstalledPath).GetAwaiter().GetResult();
                Assert.AreEqual(count, requested.Count, "Verified files must be reused without HTTP.");
                Assert.True(MNNStudioModel.Read(_job.InstalledPath).HasEchoCancellationPipeline);
                _manager.Dismiss(_job); _job = null; _service.Dispose(); _client.Dispose(); _service = null; _client = null;
            }
        }
    }
}
