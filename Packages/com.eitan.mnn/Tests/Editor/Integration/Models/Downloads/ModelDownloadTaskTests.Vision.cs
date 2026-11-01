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
        [UnityTest, Explicit("Streams the verified local YOLO graph through ModelScope installation and runs the installed Studio profile; no external network.")]
        public IEnumerator YoloManagerVerifiesGraphPreparesProfileAndRunsFromRenamedFolder()
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_VISION_ROOT") ?? throw new InvalidOperationException("Set MNN_TEST_VISION_ROOT.");
            string source = Path.Combine(root, "modelscope", MNNYoloV8Model.Repository, "yolov8n.mnn");
            string manifest = "{\"Code\":200,\"Data\":{\"Files\":[{\"Path\":\"yolov8n.mnn\",\"Type\":\"blob\",\"Size\":" + MNNYoloV8Model.GraphBytes + ",\"Revision\":\"fixture-revision\",\"Sha256\":\"" + MNNYoloV8Model.GraphSha256 + "\"}]}}";
            InstallService((request, token) =>
            {
                if (request.RequestUri.AbsolutePath.EndsWith("/repo/files", StringComparison.Ordinal))
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {Content = new StringContent(manifest)});
                StringAssert.Contains("Revision=fixture-revision", request.RequestUri.Query);
                StringAssert.Contains("FilePath=yolov8n.mnn", request.RequestUri.Query);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {Content = new StreamContent(File.OpenRead(source))});
            }, defaultManifest: false);
            _job = _manager.StartDownload(new MNNModelInfo{provider = MNNModelProvider.ModelScope, repositoryId = MNNYoloV8Model.Repository, modelScopeId = MNNYoloV8Model.Repository, name = "Detector", isRepository = true}, Path.Combine(_root, "staging"), Path.Combine(_root, "renamed-detector"));
            yield return WaitUntil(() => !_job.IsRunning);
            Assert.AreEqual(MNNDownloadState.Completed, _job.State, _job.Error);
            var model = MNNStudioModel.Read(_job.InstalledPath);
            Assert.AreEqual(MNNStudioTask.ObjectDetection, model.Task);
            Assert.True(MNNStudioTasks.Get((int)model.Task).CanRun(model));
            using (var detector = MNNYoloV8.Load(_job.InstalledPath, backendType: MNNBackendType.CPU))
                Assert.True(detector.Detect(MNNStudioImage.ReadRgb(Path.Combine(root, "bus.jpg"))).Any(box => box.Label == "bus"));
        }

        [UnityTest, Explicit("Installs the pinned multilingual OCR graphs/dictionaries from local HTTP fixtures and runs the published Studio model; no external network.")]
        public IEnumerator PaddleTextManagerInstallsMatchedLanguageAssetsAndRunsStudio()
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_VISION_ROOT") ?? throw new InvalidOperationException("Set MNN_TEST_VISION_ROOT.");
            string source = Path.Combine(root, "huggingface", MNNPaddleTextModel.Repository);
            var requested = new System.Collections.Concurrent.ConcurrentBag<string>();
            _client = new HttpClient(new Handler((request, token) =>
            {
                string prefix = "/" + MNNPaddleTextModel.Repository + "/resolve/" + MNNPaddleTextModel.Revision + "/";
                if (request.RequestUri.Host != "hf.test" || !request.RequestUri.AbsolutePath.StartsWith(prefix, StringComparison.Ordinal))
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
                string file = request.RequestUri.AbsolutePath.Substring(prefix.Length);
                Assert.True(MNNPaddleTextModel.Files().Any(item => item.Path == file));
                requested.Add(file);
                var entry = MNNPaddleTextModel.Files().Single(item => item.Path == file);
                string local = File.Exists(Path.Combine(source, file)) ? file : entry.InstalledPath;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {Content = new StreamContent(File.OpenRead(Path.Combine(source, local)))});
            }, defaultManifest: false));
            var pool = new MNNHuggingFaceMirrorPool(new[]{new MNNHuggingFaceMirror("fixture", "https://hf.test")});
            _service = new MNNModelDownloadService(_client, options: new MNNModelDownloadOptions{PreservePartialDownloads = true, ParallelThresholdBytes = long.MaxValue}, mirrorPool: pool);
            ServiceField.SetValue(_manager, _service);
            _job = _manager.StartDownload(new MNNModelInfo{provider = MNNModelProvider.HuggingFace, repositoryId = MNNPaddleTextModel.Repository, revision = "main", name = "English text recognizer"}, Path.Combine(_root, "staging"), Path.Combine(_root, "renamed-text-reader"));
            yield return WaitUntil(() => !_job.IsRunning);
            Assert.AreEqual(MNNDownloadState.Completed, _job.State, _job.Error);
            Assert.AreEqual(MNNPaddleTextModel.Revision, _job.Revision);
            CollectionAssert.AreEquivalent(MNNPaddleTextModel.Files().Select(file => file.Path), requested);
            var variants = MNNStudioModel.Discover(new[]{_job.InstalledPath});
            Assert.AreEqual(1 + MNNPaddleTextModel.Profiles.Length, variants.Count);
            Assert.True(variants.All(item => item.Task == MNNStudioTask.OCR && item.HasVisionPipeline));
            var model = MNNStudioModel.Read(_job.InstalledPath);
            Assert.AreEqual(MNNStudioTask.OCR, model.Task);
            using (var backend = MNNStudioNativeBackend.Load(model, 2, false))
                Assert.AreEqual("Hello MNN 2026", backend.Run(new MNNStudioRequest{OcrCroppedLine = true, Prompt = "Read this line.", VisionImage = MNNStudioImage.ReadRgb(Path.Combine(root, "OCR", "hello.png"))}).Text);
        }
    }
}
