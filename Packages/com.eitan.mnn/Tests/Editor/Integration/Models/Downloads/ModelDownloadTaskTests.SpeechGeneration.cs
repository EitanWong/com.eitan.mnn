using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace MNN.Unity.Tests
{
    public partial class ModelDownloadTaskTests
    {
        [UnityTest, Explicit("Requires the complete local CosyVoice archive; uses local HTTP fixtures only.")]
        public IEnumerator CosyVoiceManagerDownloadsExtractsPublishesAndStudioDiscovers()
        {
            string archive = Path.Combine(Environment.GetEnvironmentVariable("MNN_TEST_COSYVOICE_ROOT") ?? "", MNNCosyVoiceModel.Archive);
            Assert.IsTrue(File.Exists(archive));
            _client = new HttpClient(new Handler((request, token) =>
            {
                if (request.RequestUri.Host != "hf.test")
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
                if (request.RequestUri.AbsolutePath.StartsWith("/api/", StringComparison.Ordinal))
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {Content = new StringContent("{\"siblings\":[{\"rfilename\":\"" + MNNCosyVoiceModel.Archive + "\",\"size\":" + MNNCosyVoiceModel.ArchiveBytes + ",\"lfs\":{\"sha256\":\"" + MNNCosyVoiceModel.ArchiveSha256 + "\"}}]}")});
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {Content = new StreamContent(File.OpenRead(archive))});
            }));
            var pool = new MNNHuggingFaceMirrorPool(new[]{new MNNHuggingFaceMirror("fixture", "https://hf.test", "https://hf.test")});
            _service = new MNNModelDownloadService(_client, options: new MNNModelDownloadOptions{PreservePartialDownloads = true, MaxConnections = 1}, mirrorPool: pool);
            ServiceField.SetValue(_manager, _service);
            _job = _manager.StartDownload(new MNNModelInfo{provider = MNNModelProvider.HuggingFace, repositoryId = MNNCosyVoiceModel.Repository, revision = "main", name = "CosyVoice"}, Path.Combine(_root, "staging"), Path.Combine(_root, "renamed-voice"));
            var timeout = DateTime.UtcNow.AddMinutes(3);
            while (_job.IsRunning && DateTime.UtcNow < timeout)
                yield return null;
            Assert.AreEqual(MNNDownloadState.Completed, _job.State, _job.Error);
            Assert.AreEqual(MNNCosyVoiceModel.Revision, _job.Revision);
            Assert.IsTrue(MNNCosyVoiceModel.IsComplete(_job.InstalledPath));
            Assert.IsFalse(Directory.Exists(_job.StagingPath));
            Assert.IsFalse(File.Exists(Path.Combine(_job.InstalledPath, MNNCosyVoiceModel.Archive)));
            StringAssert.Contains(MNNCosyVoiceModel.Revision, File.ReadAllText(Path.Combine(_job.InstalledPath, MNNModelDownloadTasks.InstallMarker)));
            var model = MNNStudioModel.Read(_job.InstalledPath);
            Assert.IsTrue(model.IsCosyVoice);
            Assert.AreEqual(MNNStudioTask.SpeechSynthesis, model.Task);
            Assert.IsTrue(MNNStudioTasks.Get((int)model.Task).CanRun(model));
            CollectionAssert.AreEqual(new[]{"Default"}, model.TtsVoices);
        }

        [UnityTest, Explicit("Streams the existing local IndexTTS profile through the real download/install service.")]
        public IEnumerator IndexTtsManagerDownloadsVerifiesPublishesAndStudioDiscovers() => ReferenceSpeechInstall(true);
        [UnityTest, Explicit("Streams the existing local FireRed profile including external tokenizer dependencies.")]
        public IEnumerator FireRedManagerDownloadsVerifiesPublishesAndStudioDiscovers() => ReferenceSpeechInstall(false);
        private IEnumerator ReferenceSpeechInstall(bool index)
        {
            string repo = index ? MNNReferenceSpeechModel.IndexRepository : MNNReferenceSpeechModel.FireRedRepository;
            string source = Path.Combine(Environment.GetEnvironmentVariable("MNN_TEST_REFERENCE_SPEECH_ROOT"), index ? "IndexTTS-2.5-mnn" : "fireredtts3-mnn");
            var files = MNNModelDownloadService.ReferenceSpeechDownloadPlan(repo, "main");
            var requests = new System.Collections.Concurrent.ConcurrentBag<string>();
            _client = new HttpClient(new Handler((request, token) =>
            {
                string path = Uri.UnescapeDataString(request.RequestUri.AbsolutePath);
                requests.Add(path);
                foreach (var file in files)
                    if (path == "/" + file.SourceRepository + "/resolve/" + file.Revision + "/" + file.Path)
                        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                        {Content = new StreamContent(File.OpenRead(Path.Combine(source, file.Path)))});
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }));
            var pool = new MNNHuggingFaceMirrorPool(new[]{new MNNHuggingFaceMirror("fixture", "https://hf.test", "https://hf.test")});
            _service = new MNNModelDownloadService(_client, options: new MNNModelDownloadOptions{PreservePartialDownloads = true, MaxConnections = 2, ParallelThresholdBytes = long.MaxValue, RetryCount = 0}, mirrorPool: pool);
            ServiceField.SetValue(_manager, _service);
            _job = _manager.StartDownload(new MNNModelInfo{provider = MNNModelProvider.HuggingFace, repositoryId = repo, revision = "main", name = "Reference speech"}, Path.Combine(_root, "staging"), Path.Combine(_root, "renamed-reference"));
            var timeout = DateTime.UtcNow.AddMinutes(3);
            while (_job.IsRunning && DateTime.UtcNow < timeout)
                yield return null;
            Assert.AreEqual(MNNDownloadState.Completed, _job.State, _job.Error);
            Assert.AreEqual(MNNReferenceSpeechModel.Revision(repo), _job.Revision);
            Assert.True(MNNReferenceSpeechModel.IsComplete(_job.InstalledPath, index));
            Assert.False(Directory.Exists(_job.StagingPath));
            StringAssert.Contains(_job.Revision, File.ReadAllText(Path.Combine(_job.InstalledPath, MNNModelDownloadTasks.InstallMarker)));
            var model = MNNStudioModel.Read(_job.InstalledPath);
            Assert.True(model.RequiresReferenceAudio);
            Assert.AreEqual(index, model.IsIndexTts);
            Assert.True(MNNStudioTasks.Get((int)model.Task).CanRun(model));
            foreach (var file in files)
                CollectionAssert.Contains(requests, "/" + file.SourceRepository + "/resolve/" + file.Revision + "/" + file.Path);
            File.Delete(Path.Combine(_job.InstalledPath, index ? "gpt_prefill_fp16.mnn.weight" : "text_tokenizer/tokenizer.json"));
            Assert.False(MNNStudioModel.Read(_job.InstalledPath).HasGenerationPipeline);
        }
    }
}
