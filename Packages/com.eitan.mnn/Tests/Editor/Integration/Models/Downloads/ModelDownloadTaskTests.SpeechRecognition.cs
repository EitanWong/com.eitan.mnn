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
        [UnityTest, Explicit("Installs existing pinned weights through local HTTP fixtures and runs Studio; no external network.")]
        public IEnumerator WhisperManagerPinsDownloadsPreparesAndRunsRenamedInstallation() => InstallWhisper("developerabu-tiny-en");
        [UnityTest, Explicit("Uses existing pinned weights; no external network.")]
        public IEnumerator WhisperModelScopeManagerPinsAndRunsRenamedInstallation() => InstallWhisper("modelscope-tiny-en");
        [UnityTest, Explicit("Uses existing pinned weights; no external network.")]
        public IEnumerator WhisperTinyManagerPinsAndRunsRenamedInstallation() => InstallWhisper("developerabu-tiny");
        [UnityTest, Explicit("Uses existing pinned weights; no external network.")]
        public IEnumerator WhisperBaseManagerPinsAndRunsRenamedInstallation() => InstallWhisper("developerabu-base");
        [UnityTest, Explicit("Uses existing pinned weights; no external network.")]
        public IEnumerator WhisperSmallManagerPinsAndRunsRenamedInstallation() => InstallWhisper("developerabu-small");
        [UnityTest, Explicit("Installs existing Qwen3-ASR weights through a local HTTP fixture, no external network.")]
        public IEnumerator Qwen3AsrHuggingFaceManagerInstallsAndRunsRenamedProfile() => InstallQwen3Asr(false);
        [UnityTest, Explicit("Installs the ModelScope per-file pinned plan using existing identical weights, no external network.")]
        public IEnumerator Qwen3AsrModelScopeManagerInstallsAndRunsRenamedProfile() => InstallQwen3Asr(true);
        [UnityTest, Explicit("Installs existing Paraformer assets through a local HTTP fixture; no external network.")]
        public IEnumerator ParaformerKindBraveHuggingFaceInstallsAndRuns() => InstallParaformer(false, false);
        [UnityTest, Explicit("Installs identical existing assets through the duplicate pinned HF identity.")]
        public IEnumerator ParaformerGuoeraHuggingFaceInstallsAndRuns() => InstallParaformer(false, true);
        [UnityTest, Explicit("Uses the pinned ModelScope plan even when resuming an outdated manifest.")]
        public IEnumerator ParaformerModelScopeInstallsAndRuns() => InstallParaformer(true, false);
        private IEnumerator InstallParaformer(bool modelScope, bool duplicate)
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_PARAFORMER_ROOT") ?? throw new InvalidOperationException("Set MNN_TEST_PARAFORMER_ROOT.");
            string repository = duplicate ? MNNParaformerModel.DuplicateRepository : MNNParaformerModel.Repository;
            string revision = MNNParaformerModel.ResolveRevision(null, repository, modelScope);
            string source = Path.Combine(root, "Models/KindBrave-paraformer-online");
            var requested = new System.Collections.Concurrent.ConcurrentBag<string>();
            _client = new HttpClient(new Handler((request, token) =>
            {
                string file;
                if (modelScope)
                {
                    Assert.AreEqual("ms.test", request.RequestUri.Host);
                    Assert.AreEqual("/api/v1/models/" + repository + "/repo", request.RequestUri.AbsolutePath);
                    string query = Uri.UnescapeDataString(request.RequestUri.Query);
                    file = query.Substring(query.IndexOf("FilePath=", StringComparison.Ordinal) + 9);
                    Assert.AreEqual(revision, query.Substring(10, query.IndexOf('&') - 10));
                }
                else
                {
                    string prefix = "/" + repository + "/resolve/" + revision + "/";
                    Assert.AreEqual("hf.test", request.RequestUri.Host);
                    Assert.That(request.RequestUri.AbsolutePath, Does.StartWith(prefix), "No mutable metadata request.");
                    file = request.RequestUri.AbsolutePath.Substring(prefix.Length);
                }

                Assert.True(MNNParaformerModel.Paths.Contains(file) || file == "README.md");
                requested.Add(file);
                string path = file == "README.md" ? Path.Combine(root, "Inspection", modelScope ? "ms-kindbrave" : "hf-kindbrave", file) : Path.Combine(source, file);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {Content = new StreamContent(File.OpenRead(path))});
            }, defaultManifest: false));
            var pool = new MNNHuggingFaceMirrorPool(new[]{new MNNHuggingFaceMirror("fixture", "https://hf.test")});
            _service = new MNNModelDownloadService(_client, "https://ms.test", options: new MNNModelDownloadOptions{PreservePartialDownloads = true, ParallelThresholdBytes = long.MaxValue}, mirrorPool: pool);
            ServiceField.SetValue(_manager, _service);
            string staging = Path.Combine(_root, "staging");
            Directory.CreateDirectory(staging);
            File.WriteAllText(Path.Combine(staging, ".mnn-download-repository"), repository);
            File.WriteAllText(Path.Combine(staging, ".mnn-download-manifest.json"), "{\"Files\":[{\"Path\":\"outdated.mnn\",\"Type\":\"blob\"}]}");
            _job = _manager.StartDownload(new MNNModelInfo{provider = modelScope ? MNNModelProvider.ModelScope : MNNModelProvider.HuggingFace, repositoryId = repository, revision = modelScope ? "master" : "main", name = "Chinese recognizer"}, staging, Path.Combine(_root, "renamed-audio"));
            var deadline = DateTime.UtcNow.AddMinutes(3);
            while (_job.IsRunning && DateTime.UtcNow < deadline)
                yield return null;
            Assert.AreEqual(MNNDownloadState.Completed, _job.State, _job.Error);
            Assert.AreEqual(revision, _job.Revision);
            CollectionAssert.AreEquivalent(MNNParaformerModel.Paths.Concat(new[]{"README.md"}), requested);
            Assert.True(MNNParaformerModel.IsComplete(_job.InstalledPath));
            var model = MNNStudioModel.Read(_job.InstalledPath);
            Assert.True(model.HasSpeechRecognitionPipeline);
            Assert.False(MNNStudioTasks.Get((int)MNNStudioTask.Chat).CanRun(model));
            using (var backend = MNNStudioNativeBackend.Load(model, 4, false))
            {
                var result = backend.Run(new MNNStudioRequest{Audio = Path.Combine(root, "Inspection/Reference/example--asr_example.wav")});
                Assert.AreEqual("欢迎大家来体验达摩院推出的语音识别模型", result.Text);
                Assert.That(result.BackendDescription, Does.Contain("Paraformer").And.Contain("CPU"));
            }

            File.Delete(Path.Combine(_job.InstalledPath, "decoder.mnn"));
            Assert.False(MNNStudioModel.Read(_job.InstalledPath).HasSpeechRecognitionPipeline);
        }

        private IEnumerator InstallQwen3Asr(bool modelScope)
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_QWEN3_ASR_ROOT") ?? throw new InvalidOperationException("Set MNN_TEST_QWEN3_ASR_ROOT.");
            string repository = modelScope ? MNNQwen3AsrModel.ModelScopeRepository : MNNQwen3AsrModel.Repository;
            string source = Path.Combine(root, MNNQwen3AsrModel.Repository.Replace("/", "--"));
            var requested = new System.Collections.Concurrent.ConcurrentBag<string>();
            _client = new HttpClient(new Handler((request, token) =>
            {
                string file, revision;
                if (modelScope)
                {
                    Assert.AreEqual("ms.test", request.RequestUri.Host);
                    Assert.AreEqual("/api/v1/models/" + repository + "/repo", request.RequestUri.AbsolutePath);
                    string query = Uri.UnescapeDataString(request.RequestUri.Query);
                    file = query.Substring(query.IndexOf("FilePath=", StringComparison.Ordinal) + 9);
                    revision = query.Substring(10, query.IndexOf('&') - 10);
                }
                else
                {
                    string prefix = "/" + repository + "/resolve/" + MNNQwen3AsrModel.Revision + "/";
                    Assert.AreEqual("hf.test", request.RequestUri.Host);
                    Assert.That(request.RequestUri.AbsolutePath, Does.StartWith(prefix), "No mutable metadata request.");
                    file = request.RequestUri.AbsolutePath.Substring(prefix.Length);
                    revision = MNNQwen3AsrModel.Revision;
                }

                int index = Array.IndexOf(MNNQwen3AsrModel.Paths, file);
                Assert.True(index >= 0 || file == (modelScope ? "README.md" : "LICENSE"));
                Assert.AreEqual(index >= 0 ? MNNQwen3AsrModel.FileRevision(index, modelScope) : modelScope ? "dccf04663faa19b24af3f9d3fd0835d308d51687" : MNNQwen3AsrModel.Revision, revision);
                requested.Add(file);
                string path = file == "README.md" ? Path.Combine(root, "ModelScopeNotices", file) : Path.Combine(source, file);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {Content = new StreamContent(File.OpenRead(path))});
            }, defaultManifest: false));
            var pool = new MNNHuggingFaceMirrorPool(new[]{new MNNHuggingFaceMirror("fixture", "https://hf.test")});
            _service = new MNNModelDownloadService(_client, "https://ms.test", options: new MNNModelDownloadOptions{PreservePartialDownloads = true, ParallelThresholdBytes = long.MaxValue}, mirrorPool: pool);
            ServiceField.SetValue(_manager, _service);
            string staging = Path.Combine(_root, "staging");
            if (modelScope)
            {
                Directory.CreateDirectory(staging);
                File.WriteAllText(Path.Combine(staging, ".mnn-download-repository"), repository);
                File.WriteAllText(Path.Combine(staging, ".mnn-download-manifest.json"), "{\"Files\":[{\"Path\":\"outdated.mnn\",\"Type\":\"blob\"}]}");
            }

            _job = _manager.StartDownload(new MNNModelInfo{provider = modelScope ? MNNModelProvider.ModelScope : MNNModelProvider.HuggingFace, repositoryId = repository, revision = modelScope ? "master" : "main", name = "Speech recognizer"}, staging, Path.Combine(_root, "renamed-audio"));
            var deadline = DateTime.UtcNow.AddMinutes(3);
            while (_job.IsRunning && DateTime.UtcNow < deadline)
                yield return null;
            Assert.AreEqual(MNNDownloadState.Completed, _job.State, _job.Error);
            Assert.AreEqual(MNNQwen3AsrModel.ResolveRevision(null, repository), _job.Revision);
            CollectionAssert.AreEquivalent(MNNQwen3AsrModel.Paths.Concat(new[]{modelScope ? "README.md" : "LICENSE"}), requested);
            Assert.True(MNNQwen3AsrModel.IsComplete(_job.InstalledPath));
            using (var backend = MNNStudioNativeBackend.Load(MNNStudioModel.Read(_job.InstalledPath), 4, false))
            {
                var result = backend.Run(new MNNStudioRequest{Audio = Path.Combine(root, "..", "..", "Zipformer", "Audio", "en-0.wav")});
                string reference = File.ReadAllLines(Path.Combine(root, "..", "..", "Zipformer", "Audio", "en-trans.txt"))[0].Substring(6).Trim();
                Assert.AreEqual(reference, System.Text.RegularExpressions.Regex.Replace(result.Text.ToUpperInvariant(), "[^A-Z]+", " ").Trim());
            }

            File.Delete(Path.Combine(_job.InstalledPath, "audio.mnn.weight"));
            Assert.False(MNNStudioModel.Read(_job.InstalledPath).HasSpeechRecognitionPipeline);
        }

        private IEnumerator InstallWhisper(string id)
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_WHISPER_ROOT") ?? throw new InvalidOperationException("Set MNN_TEST_WHISPER_ROOT.");
            var profile = MNNWhisperModel.Profiles.Single(p => p.Id == id);
            string source = Path.Combine(root, profile.Repository.Replace("/", "--"));
            var requested = new System.Collections.Concurrent.ConcurrentBag<string>();
            _client = new HttpClient(new Handler((request, token) =>
            {
                string file;
                if (profile.ModelScope)
                {
                    Assert.AreEqual("ms.test", request.RequestUri.Host);
                    Assert.AreEqual("/api/v1/models/" + profile.Repository + "/repo", request.RequestUri.AbsolutePath, "Pinned profiles do not request a mutable repository manifest.");
                    string query = Uri.UnescapeDataString(request.RequestUri.Query);
                    Assert.That(query, Does.Contain("Revision=" + profile.Revision + "&"));
                    file = query.Substring(query.IndexOf("FilePath=", StringComparison.Ordinal) + 9);
                }
                else
                {
                    string prefix = "/" + profile.Repository + "/resolve/" + profile.Revision + "/";
                    Assert.AreEqual("hf.test", request.RequestUri.Host);
                    Assert.That(request.RequestUri.AbsolutePath, Does.StartWith(prefix));
                    file = request.RequestUri.AbsolutePath.Substring(prefix.Length);
                }

                Assert.True(profile.Paths.Contains(file));
                requested.Add(file);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {Content = new StreamContent(File.OpenRead(Path.Combine(source, file)))});
            }, defaultManifest: false));
            var pool = new MNNHuggingFaceMirrorPool(new[]{new MNNHuggingFaceMirror("fixture", "https://hf.test")});
            _service = new MNNModelDownloadService(_client, "https://ms.test", options: new MNNModelDownloadOptions{PreservePartialDownloads = true, ParallelThresholdBytes = long.MaxValue}, mirrorPool: pool);
            ServiceField.SetValue(_manager, _service);
            _job = _manager.StartDownload(new MNNModelInfo{provider = profile.ModelScope ? MNNModelProvider.ModelScope : MNNModelProvider.HuggingFace, repositoryId = profile.Repository, revision = profile.ModelScope ? "master" : "main", name = "Speech recognizer"}, Path.Combine(_root, "staging"), Path.Combine(_root, "renamed-whisper"));
            var deadline = DateTime.UtcNow.AddMinutes(2);
            while (_job.IsRunning && DateTime.UtcNow < deadline)
                yield return null;
            Assert.AreEqual(MNNDownloadState.Completed, _job.State, _job.Error);
            Assert.AreEqual(profile.Revision, _job.Revision);
            CollectionAssert.AreEquivalent(profile.Paths, requested);
            Assert.True(MNNWhisperModel.IsComplete(_job.InstalledPath));
            using (var backend = MNNStudioNativeBackend.Load(MNNStudioModel.Read(_job.InstalledPath), 4, false))
            {
                var result = backend.Run(new MNNStudioRequest{Audio = Path.Combine(root, "..", "..", "Zipformer", "Audio", "en-0.wav")});
                string reference = File.ReadAllLines(Path.Combine(root, "..", "..", "Zipformer", "Audio", "en-trans.txt"))[0].Substring(6).Trim();
                Assert.AreEqual(reference, System.Text.RegularExpressions.Regex.Replace(result.Text.ToUpperInvariant(), "[^A-Z ]", ""));
            }
        }
    }
}
