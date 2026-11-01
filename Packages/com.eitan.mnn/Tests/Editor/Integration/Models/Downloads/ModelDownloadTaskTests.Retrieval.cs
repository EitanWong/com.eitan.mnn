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
        [UnityTest, Explicit("Streams the existing pinned MiniLM files through Model Manager; no external network.")]
        public IEnumerator BertEmbeddingManagerPinsDownloadsPreparesAndPublishesStudioModel()
        {
            string source = Path.Combine(Environment.GetEnvironmentVariable("MNN_TEST_BERT_ROOT") ?? throw new InvalidOperationException("Set MNN_TEST_BERT_ROOT."), MNNBertEmbeddingModel.MiniLmRepository);
            var files = MNNBertEmbeddingModel.Files(MNNBertEmbeddingModel.MiniLmRepository);
            var requested = new System.Collections.Concurrent.ConcurrentBag<string>();
            _client = new HttpClient(new Handler((request, token) =>
            {
                string prefix = "/" + MNNBertEmbeddingModel.MiniLmRepository + "/resolve/" + MNNBertEmbeddingModel.Revision(MNNBertEmbeddingModel.MiniLmRepository) + "/";
                string path = request.RequestUri.AbsolutePath;
                if (request.RequestUri.Host == "hf.test" && path.StartsWith(prefix, StringComparison.Ordinal))
                {
                    string file = path.Substring(prefix.Length);
                    Assert.IsTrue(files.Any(item => item.Path == file));
                    requested.Add(file);
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {Content = new StreamContent(File.OpenRead(Path.Combine(source, file)))});
                }

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }));
            var pool = new MNNHuggingFaceMirrorPool(new[]{new MNNHuggingFaceMirror("fixture", "https://hf.test")});
            _service = new MNNModelDownloadService(_client, options: new MNNModelDownloadOptions{PreservePartialDownloads = true, ParallelThresholdBytes = long.MaxValue}, mirrorPool: pool);
            ServiceField.SetValue(_manager, _service);
            _job = _manager.StartDownload(new MNNModelInfo{provider = MNNModelProvider.HuggingFace, repositoryId = MNNBertEmbeddingModel.MiniLmRepository, revision = "main", name = "renamed-encoder"}, Path.Combine(_root, "staging"), Path.Combine(_root, "installed-encoder"));
            yield return WaitUntil(() => !_job.IsRunning);
            Assert.AreEqual(MNNDownloadState.Completed, _job.State, _job.Error);
            Assert.AreEqual(MNNBertEmbeddingModel.Revision(MNNBertEmbeddingModel.MiniLmRepository), _job.Revision);
            CollectionAssert.AreEquivalent(files.Select(file => file.Path), requested);
            Assert.IsTrue(File.Exists(Path.Combine(_job.InstalledPath, MNNBertEmbeddingConfig.FileName)));
            var model = MNNStudioModel.Read(_job.InstalledPath);
            Assert.AreEqual(MNNStudioTask.Embedding, model.Task);
            using (var embedding = MNNEmbedding.Load(_job.InstalledPath, backendType: MNNBackendType.CPU))
                Assert.AreEqual(384, embedding.Encode("Installed through Model Manager.").Length);
        }

        [UnityTest, Explicit("Streams the existing verified ModelScope Chinese BGE files through the real installer; no external network.")]
        public IEnumerator ModelScopeMtokEmbeddingInstallsAndRunsFromRenamedFolder()
        {
            const string repository = "TorryJi/bge-small-zh-v1.5-MNN";
            string source = Environment.GetEnvironmentVariable("MNN_TEST_NATIVE_BERT_ROOT") ?? throw new InvalidOperationException("Set MNN_TEST_NATIVE_BERT_ROOT.");
            var paths = new[]{"README.md", "configuration.json", "config.json", "llm_config.json", "embeddings_int8.bin", "llm.mnn", "llm.mnn.weight", "tokenizer.mtok"};
            string manifest = "{\"Code\":200,\"Data\":{\"Files\":[" + string.Join(",", paths.Select(path =>
            {
                string local = Path.Combine(source, path);
                return "{\"Path\":\"" + path + "\",\"Type\":\"blob\",\"Size\":" + new FileInfo(local).Length + ",\"Sha256\":\"" + ArchiveHash(local) + "\"}";
            })) + "]}}";
            InstallService((request, token) =>
            {
                if (request.RequestUri.AbsolutePath.EndsWith("/repo/files", StringComparison.Ordinal))
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {Content = new StringContent(manifest)});
                string query = Uri.UnescapeDataString(request.RequestUri.Query);
                string file = paths.SingleOrDefault(path => query.EndsWith("FilePath=" + path, StringComparison.Ordinal)) ?? throw new InvalidOperationException("Unexpected local fixture request: " + request.RequestUri.AbsoluteUri);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {Content = new StreamContent(File.OpenRead(Path.Combine(source, file)))});
            }, defaultManifest: false);
            _job = _manager.StartDownload(new MNNModelInfo{provider = MNNModelProvider.ModelScope, repositoryId = repository, modelScopeId = repository, name = "Chinese BGE", isRepository = true}, Path.Combine(_root, "staging"), Path.Combine(_root, "renamed-chinese-encoder"));
            yield return WaitUntil(() => !_job.IsRunning);
            Assert.AreEqual(MNNDownloadState.Completed, _job.State, _job.Error);
            Assert.IsTrue(File.Exists(Path.Combine(_job.InstalledPath, "tokenizer.mtok")));
            Assert.AreEqual(MNNStudioTask.Embedding, MNNStudioModel.Read(_job.InstalledPath).Task);
            using (var embedding = MNNEmbedding.Load(_job.InstalledPath, backendType: MNNBackendType.CPU))
                Assert.AreEqual(512, embedding.Encode("北京是中国的首都。").Length);
        }
    }
}
