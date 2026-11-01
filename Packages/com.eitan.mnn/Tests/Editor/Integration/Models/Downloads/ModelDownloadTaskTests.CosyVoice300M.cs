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
        [UnityTest, Explicit("Streams the existing final CosyVoice-300M release through the real manager; no external model downloads.")]
        public IEnumerator CosyVoice300MManagerInstallsPinnedReleaseAndStudioDiscovers()
        {
            string source = Environment.GetEnvironmentVariable("MNN_TEST_COSYVOICE300M_ROOT");
            Assert.IsTrue(MNNCosyVoice300MModel.IsComplete(source), "Set MNN_TEST_COSYVOICE300M_ROOT to the final release.");
            var plan = MNNVerifiedTtsModel.CosyVoice300MFiles();
            var requested = new ConcurrentBag<string>();
            _client = new HttpClient(new Handler((request, token) =>
            {
                Assert.AreEqual("modelscope.test", request.RequestUri.Host);
                Assert.AreEqual("/api/v1/models/" + MNNVerifiedTtsModel.CosyVoice300MRepository + "/repo", request.RequestUri.AbsolutePath);
                string query = request.RequestUri.Query;
                Assert.That(query, Does.Contain("Revision=" + MNNVerifiedTtsModel.CosyVoice300MRevision));
                string path = Uri.UnescapeDataString(query.Substring(query.IndexOf("FilePath=", StringComparison.Ordinal) + 9));
                Assert.IsTrue(plan.Any(file => file.Path == path), "Only the verified release plan may be requested.");
                requested.Add(path);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {Content = new StreamContent(File.OpenRead(Path.Combine(source, path)))});
            }, defaultManifest: false));
            _service = new MNNModelDownloadService(_client, "https://modelscope.test",
                new MNNModelDownloadOptions {PreservePartialDownloads = true, MaxConnections = 2, ParallelThresholdBytes = long.MaxValue, RetryCount = 0});
            ServiceField.SetValue(_manager, _service);
            string staging = Path.Combine(_root, "staging");
            Directory.CreateDirectory(staging);
            File.WriteAllText(Path.Combine(staging, ".mnn-download-repository"), MNNVerifiedTtsModel.CosyVoice300MRepository + "@" + MNNVerifiedTtsModel.CosyVoice300MRevision);
            File.WriteAllText(Path.Combine(staging, ".mnn-download-manifest.json"), "{\"Files\":[{\"Path\":\"experimental-int8/rejected.mnn\",\"Type\":\"blob\"}]}");
            var model = MNNModelRepository.GetDefaultRepository().SearchModels("CosyVoice-300M-MNN").Single();
            _job = _manager.StartDownload(model, staging, Path.Combine(_root, "renamed-final-voice"));
            var deadline = DateTime.UtcNow.AddMinutes(3);
            while (_job.IsRunning && DateTime.UtcNow < deadline) yield return null;
            Assert.AreEqual(MNNDownloadState.Completed, _job.State, _job.Error);
            Assert.AreEqual(MNNVerifiedTtsModel.CosyVoice300MRevision, _job.Revision);
            Assert.AreEqual(plan.Sum(file => file.Bytes), _job.ReceivedBytes);
            CollectionAssert.AreEquivalent(plan.Select(file => file.Path), requested);
            Assert.IsFalse(Directory.Exists(staging));
            Assert.IsTrue(MNNCosyVoice300MModel.IsComplete(_job.InstalledPath));
            Assert.AreEqual(plan.Length + 1, Directory.GetFiles(_job.InstalledPath, "*", SearchOption.AllDirectories).Length);
            Assert.IsTrue(MNNModelRepository.MatchesInstallMarker(model, File.ReadAllText(Path.Combine(_job.InstalledPath, MNNModelDownloadTasks.InstallMarker))));
            var studio = MNNStudioModel.Read(_job.InstalledPath);
            Assert.IsTrue(studio.IsCosyVoice300M);
            Assert.AreEqual(MNNStudioTask.SpeechSynthesis, studio.Task);
            Assert.IsTrue(MNNStudioTasks.Get((int)studio.Task).CanRun(studio));
            CollectionAssert.AreEqual(new[] {"Default"}, studio.TtsVoices);
            int count = requested.Count;
            var reuse = _service.DownloadRepositoryAsync(model.repositoryId, _job.InstalledPath);
            while (!reuse.IsCompleted) yield return null;
            Assert.IsFalse(reuse.IsFaulted, reuse.Exception?.ToString());
            Assert.AreEqual(count, requested.Count, "All verified files must be reused without HTTP.");
            File.Delete(Path.Combine(_job.InstalledPath, "fp16/llm_decode_compact.mnn.weight"));
            Assert.IsFalse(MNNStudioModel.Read(_job.InstalledPath).HasGenerationPipeline);
        }
    }
}
