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
        [UnityTest, Explicit("Installs existing pinned Silero bytes through a local HTTP fixture and runs Studio; no external network.")]
        public IEnumerator SileroVadManagerPinsInstallsAndRunsRenamedProfile()
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_SILERO_ROOT") ?? throw new InvalidOperationException("Set MNN_TEST_SILERO_ROOT.");
            var requested = new System.Collections.Concurrent.ConcurrentBag<string>();
            _client = new HttpClient(new Handler((request, token) =>
            {
                string prefix = "/" + MNNSileroModel.Repository + "/resolve/" + MNNSileroModel.Revision + "/";
                Assert.AreEqual("hf.test", request.RequestUri.Host);
                Assert.That(request.RequestUri.AbsolutePath, Does.StartWith(prefix), "Never use mutable metadata for the verified plan.");
                string file = request.RequestUri.AbsolutePath.Substring(prefix.Length);
                Assert.True(file == MNNSileroModel.Graph || file == "README.md");
                requested.Add(file);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {Content = new StreamContent(File.OpenRead(Path.Combine(root, file == "README.md" ? "Inspection" : "Models", file)))});
            }, defaultManifest: false));
            var pool = new MNNHuggingFaceMirrorPool(new[]{new MNNHuggingFaceMirror("fixture", "https://hf.test")});
            _service = new MNNModelDownloadService(_client, options: new MNNModelDownloadOptions{PreservePartialDownloads = true}, mirrorPool: pool);
            ServiceField.SetValue(_manager, _service);
            string staging = Path.Combine(_root, "staging");
            Directory.CreateDirectory(staging);
            File.WriteAllText(Path.Combine(staging, ".mnn-download-manifest.json"), "{\"Files\":[{\"Path\":\"outdated.mnn\",\"Type\":\"blob\"}]}");
            _job = _manager.StartDownload(new MNNModelInfo{provider = MNNModelProvider.HuggingFace, repositoryId = MNNSileroModel.Repository, revision = "main", name = "Voice activity"}, staging, Path.Combine(_root, "renamed-vad"));
            var deadline = DateTime.UtcNow.AddMinutes(2);
            while (_job.IsRunning && DateTime.UtcNow < deadline)
                yield return null;
            Assert.AreEqual(MNNDownloadState.Completed, _job.State, _job.Error);
            Assert.AreEqual(MNNSileroModel.Revision, _job.Revision);
            CollectionAssert.AreEquivalent(new[]{MNNSileroModel.Graph, "README.md"}, requested);
            var model = MNNStudioModel.Read(_job.InstalledPath);
            Assert.True(model.HasAudioProcessingPipeline);
            using (var backend = MNNStudioNativeBackend.Load(model, 4, false))
            {
                var result = backend.Run(new MNNStudioRequest{Audio = Path.Combine(root, "../../ASR/Zipformer/Audio/en-0.wav")});
                Assert.AreEqual(1, result.VoiceActivity.Segments.Count);
                Assert.That(result.Text, Does.Contain("0.386–6.238 s"));
            }

            File.Delete(Path.Combine(_job.InstalledPath, MNNSileroModel.Graph));
            Assert.False(MNNStudioModel.Read(_job.InstalledPath).HasAudioProcessingPipeline);
        }

        [UnityTest, Explicit("Installs the existing KWS model through a local HTTP fixture and runs Studio; no external network.")]
        public IEnumerator KeywordSpotterManagerPinsInstallsNestedProfileAndDetectsDemo()
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_KWS_MODELS") ?? throw new InvalidOperationException("Set MNN_TEST_KWS_MODELS.");
            string assets = MNNKwsModel.AssetDirectory(root);
            var requested = new System.Collections.Concurrent.ConcurrentBag<string>();
            _client = new HttpClient(new Handler((request, token) =>
            {
                string prefix = "/" + MNNKwsModel.Repository + "/resolve/" + MNNKwsModel.Revision + "/" + MNNKwsModel.Prefix;
                Assert.AreEqual("hf.test", request.RequestUri.Host);
                Assert.That(request.RequestUri.AbsolutePath, Does.StartWith(prefix));
                string file = request.RequestUri.AbsolutePath.Substring(prefix.Length);
                CollectionAssert.Contains(MNNKwsModel.Paths, file);
                requested.Add(file);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {Content = new StreamContent(File.OpenRead(Path.Combine(assets, file)))});
            }, defaultManifest: false));
            var pool = new MNNHuggingFaceMirrorPool(new[]{new MNNHuggingFaceMirror("fixture", "https://hf.test")});
            _service = new MNNModelDownloadService(_client, options: new MNNModelDownloadOptions{PreservePartialDownloads = true}, mirrorPool: pool);
            ServiceField.SetValue(_manager, _service);
            _job = _manager.StartDownload(new MNNModelInfo{provider = MNNModelProvider.HuggingFace, repositoryId = MNNKwsModel.Repository, revision = "main", name = "Wake words"}, Path.Combine(_root, "staging"), Path.Combine(_root, "renamed-keywords"));
            var deadline = DateTime.UtcNow.AddMinutes(2);
            while (_job.IsRunning && DateTime.UtcNow < deadline)
                yield return null;
            Assert.AreEqual(MNNDownloadState.Completed, _job.State, _job.Error);
            Assert.AreEqual(MNNKwsModel.Revision, _job.Revision);
            CollectionAssert.AreEquivalent(MNNKwsModel.Paths, requested);
            var model = MNNStudioModel.Read(_job.InstalledPath);
            Assert.True(model.IsKeywordSpotter);
            Assert.True(model.HasKeywordSpottingPipeline);
            using (var backend = MNNStudioNativeBackend.Load(model, 2, false))
            {
                var result = backend.Run(new MNNStudioRequest{Audio = Path.Combine(assets, "test_wavs/3.wav"), Keywords = File.ReadAllText(Path.Combine(assets, "test_wavs/test_keywords.txt"))});
                CollectionAssert.AreEqual(new[]{"文森特卡索", "法国"}, result.KeywordDetections.Select(match => match.Keyword));
            }

            File.WriteAllText(Path.Combine(MNNKwsModel.AssetDirectory(_job.InstalledPath), MNNKwsModel.Joiner), new string ('x', (int)MNNKwsModel.Sizes[2]));
            Assert.Throws<InvalidDataException>(() => MNNKeywordSpotter.Load(_job.InstalledPath));
        }

        [UnityTest, Explicit("Installs existing pinned SmartTurn bytes through a local HTTP fixture and runs Studio; no external network.")]
        public IEnumerator SmartTurnManagerPinsInstallsAndRunsRenamedProfile()
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_SMART_TURN_ROOT") ?? throw new InvalidOperationException("Set MNN_TEST_SMART_TURN_ROOT.");
            var requested = new System.Collections.Concurrent.ConcurrentBag<string>();
            _client = new HttpClient(new Handler((request, token) =>
            {
                string prefix = "/" + MNNSmartTurnModel.Repository + "/resolve/" + MNNSmartTurnModel.Revision + "/";
                Assert.AreEqual("hf.test", request.RequestUri.Host);
                Assert.That(request.RequestUri.AbsolutePath, Does.StartWith(prefix), "Never use mutable metadata for the verified plan.");
                string file = request.RequestUri.AbsolutePath.Substring(prefix.Length);
                Assert.True(file == MNNSmartTurnModel.Graph || file == "README.md");
                requested.Add(file);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {Content = new StreamContent(File.OpenRead(Path.Combine(root, "Models", file)))});
            }, defaultManifest: false));
            var pool = new MNNHuggingFaceMirrorPool(new[]{new MNNHuggingFaceMirror("fixture", "https://hf.test")});
            _service = new MNNModelDownloadService(_client, options: new MNNModelDownloadOptions{PreservePartialDownloads = true}, mirrorPool: pool);
            ServiceField.SetValue(_manager, _service);
            string staging = Path.Combine(_root, "staging");
            Directory.CreateDirectory(staging);
            File.WriteAllText(Path.Combine(staging, ".mnn-download-manifest.json"), "{\"Files\":[{\"Path\":\"outdated.mnn\",\"Type\":\"blob\"}]}");
            _job = _manager.StartDownload(new MNNModelInfo{provider = MNNModelProvider.HuggingFace, repositoryId = MNNSmartTurnModel.Repository, revision = "main", name = "Turn detection"}, staging, Path.Combine(_root, "renamed-vad"));
            var deadline = DateTime.UtcNow.AddMinutes(2);
            while (_job.IsRunning && DateTime.UtcNow < deadline)
                yield return null;
            Assert.AreEqual(MNNDownloadState.Completed, _job.State, _job.Error);
            Assert.AreEqual(MNNSmartTurnModel.Revision, _job.Revision);
            CollectionAssert.AreEquivalent(new[]{MNNSmartTurnModel.Graph, "README.md"}, requested);
            var model = MNNStudioModel.Read(_job.InstalledPath);
            Assert.True(model.HasAudioProcessingPipeline);
            using (var backend = MNNStudioNativeBackend.Load(model, 4, false))
            {
                var result = backend.Run(new MNNStudioRequest{Audio = Path.Combine(root, "../../ASR/Zipformer/Audio/en-0.wav")});
                Assert.True(result.TurnDetection.IsComplete);
                Assert.That(result.Text, Does.Contain("Turn complete."));
            }

            File.Delete(Path.Combine(_job.InstalledPath, MNNSmartTurnModel.Graph));
            Assert.False(MNNStudioModel.Read(_job.InstalledPath).HasAudioProcessingPipeline);
        }
    }
}
