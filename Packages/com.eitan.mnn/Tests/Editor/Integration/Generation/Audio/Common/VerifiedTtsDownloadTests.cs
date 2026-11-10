using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MNN.Unity.Tests
{
    public class VerifiedTtsDownloadTests
    {
        [UnityTest]
        public IEnumerator KokoroRequestsPinnedGraphAndSupplementRepositories()
        {
            const string repository = "wangjazz/kokoro-82m-mnn";
            string source = Environment.GetEnvironmentVariable("MNN_TEST_KOKORO_CONSTANT_ROOT");
            Assert.That(source, Is.Not.Null.And.Not.Empty);
            string destination = Path.Combine(AdditionalTtsValidation.Artifacts, "download-routing", Guid.NewGuid().ToString("N"));
            var files = MNNVerifiedTtsModel.HuggingFaceFiles(repository);
            var calls = new ConcurrentBag<string>();
            using (var client = new HttpClient(new LocalFiles(source, repository, files, calls)))
            using (var service = new MNNModelDownloadService(client, options: new MNNModelDownloadOptions{MaxConnections = 1, RetryCount = 0},
                mirrorPool: new MNNHuggingFaceMirrorPool(new[]{new MNNHuggingFaceMirror("fixture", "https://fixture.test", "https://fixture.test")})))
            {
                try
                {
                    Task<long> task = service.DownloadHuggingFaceRepositoryAsync(repository, destination);
                    while (!task.IsCompleted)
                        yield return null;
                    Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
                    MNNVerifiedTtsModel.Validate(destination, repository);
                    CollectionAssert.AreEquivalent(files.Select(file => "/" + (file.SourceRepository ?? repository) + "/resolve/" + file.Revision + "/" + file.Path), calls);
                    Task<long> invalid = service.DownloadHuggingFaceRepositoryAsync(repository, destination, "unverified-revision");
                    while (!invalid.IsCompleted)
                        yield return null;
                    Assert.IsTrue(invalid.IsFaulted);
                    Assert.IsInstanceOf<ArgumentException>(invalid.Exception.GetBaseException());
                    Assert.AreEqual(files.Length, calls.Count);
                }
                finally
                {
                    if (Directory.Exists(destination))
                        Directory.Delete(destination, true);
                }
            }
        }

        [UnityTest]
        public IEnumerator Kokoro11PinsExternalWeightsAndBothHubRevisions()
        {
            const string repository = MNNVerifiedTtsModel.Kokoro11Repository;
            string source = Environment.GetEnvironmentVariable("MNN_TEST_KOKORO11_ROOT");
            string destination = Path.Combine(AdditionalTtsValidation.Artifacts, "download-routing", Guid.NewGuid().ToString("N"));
            var files = MNNVerifiedTtsModel.HuggingFaceFiles(repository);
            var calls = new ConcurrentBag<string>();
            using (var client = new HttpClient(new LocalFiles(source, repository, files, calls)))
            using (var service = new MNNModelDownloadService(client, options: new MNNModelDownloadOptions{MaxConnections = 2, RetryCount = 0},
                mirrorPool: new MNNHuggingFaceMirrorPool(new[]{new MNNHuggingFaceMirror("fixture", "https://fixture.test", "https://fixture.test")})))
            {
                try
                {
                    Task<long> task = service.DownloadHuggingFaceRepositoryAsync(repository, destination);
                    while (!task.IsCompleted) yield return null;
                    Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
                    MNNVerifiedTtsModel.Validate(destination, repository);
                    CollectionAssert.AreEquivalent(files.Select(file => "/" + repository + "/resolve/" + MNNVerifiedTtsModel.Kokoro11HuggingFaceRevision + "/" + file.Path), calls);
                    CollectionAssert.AreEquivalent(files.Select(file => file.Hash), MNNVerifiedTtsModel.Files(repository).Select(file => file.Hash));
                    Assert.IsTrue(MNNVerifiedTtsModel.Files(repository).All(file => file.Revision == MNNVerifiedTtsModel.Kokoro11ModelScopeRevision));
                    var studio = MNNStudioModel.Read(destination);
                    Assert.AreEqual(MNNSpeechModelKind.Kokoro, studio.TtsProfile);
                    Assert.AreEqual(103, studio.TtsVoices.Length);
                    Assert.IsTrue(studio.HasGenerationPipeline);
                }
                finally
                {
                    if (Directory.Exists(destination)) Directory.Delete(destination, true);
                }
            }
        }

        [UnityTest]
        public IEnumerator MmsTamilInstallsPinnedGraphTokensAndPreparedStudioProfile()
        {
            const string repository = MNNVerifiedTtsModel.MmsTamilRepository;
            string source = Environment.GetEnvironmentVariable("MNN_TEST_MMS_TAMIL_ROOT");
            string destination = Path.Combine(AdditionalTtsValidation.Artifacts, "download-routing", Guid.NewGuid().ToString("N"));
            var files = MNNVerifiedTtsModel.HuggingFaceFiles(repository);
            var calls = new ConcurrentBag<string>();
            using (var client = new HttpClient(new LocalFiles(source, repository, files, calls)))
            using (var service = new MNNModelDownloadService(client, options: new MNNModelDownloadOptions{MaxConnections = 2, RetryCount = 0},
                mirrorPool: new MNNHuggingFaceMirrorPool(new[]{new MNNHuggingFaceMirror("fixture", "https://fixture.test", "https://fixture.test")})))
            {
                try
                {
                    Task<long> task = service.DownloadHuggingFaceRepositoryAsync(repository, destination);
                    while (!task.IsCompleted) yield return null;
                    Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
                    MNNVerifiedTtsModel.Prepare(destination, repository);
                    StringAssert.Contains("CC-BY-NC-4.0", File.ReadAllText(Path.Combine(destination, "MMS-SOURCE-NOTICE.txt")));
                    CollectionAssert.AreEquivalent(files.Select(file => "/" + repository + "/resolve/" + MNNVerifiedTtsModel.MmsTamilRevision + "/" + file.Path), calls);
                    var studio = MNNStudioModel.Read(destination);
                    CollectionAssert.AreEqual(new[]{"int8", "fp16_int8", "fp32"}, MNNMmsTamil.InstalledPrecisions(destination));
                    Assert.AreEqual(MNNSpeechModelKind.MmsTamil, studio.TtsProfile);
                    Assert.AreEqual(MNNStudioTask.SpeechSynthesis, studio.Task);
                    CollectionAssert.AreEqual(new[]{"Tamil"}, studio.TtsVoices);
                    Assert.IsTrue(studio.HasGenerationPipeline);
                }
                finally
                {
                    if (Directory.Exists(destination)) Directory.Delete(destination, true);
                }
            }
        }

        [UnityTest]
        public IEnumerator SherpaVitsChineseDownloadsFixedFilesFromEitherHub()
        {
            const string repository = MNNVerifiedTtsModel.SherpaVitsRepository;
            string source = Environment.GetEnvironmentVariable("MNN_TEST_SHERPA_VITS_ROOT");
            foreach (bool modelScope in new[]{false, true})
            {
                string destination = Path.Combine(AdditionalTtsValidation.Artifacts, "download-routing", Guid.NewGuid().ToString("N"));
                var files = modelScope ? MNNVerifiedTtsModel.Files(repository) : MNNVerifiedTtsModel.HuggingFaceFiles(repository);
                var calls = new ConcurrentBag<string>();
                using (var client = new HttpClient(modelScope ? (HttpMessageHandler)new LocalModelScopeFiles(source, repository, files, calls) : new LocalFiles(source, repository, files, calls)))
                using (var service = new MNNModelDownloadService(client, "https://fixture.test", new MNNModelDownloadOptions{MaxConnections = 1, RetryCount = 0},
                    new MNNHuggingFaceMirrorPool(new[]{new MNNHuggingFaceMirror("fixture", "https://fixture.test", "https://fixture.test")})))
                {
                    try
                    {
                        Task<long> task = modelScope ? service.DownloadRepositoryAsync(repository, destination) : service.DownloadHuggingFaceRepositoryAsync(repository, destination);
                        while (!task.IsCompleted) yield return null;
                        Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
                        MNNVerifiedTtsModel.Prepare(destination, repository);
                        Assert.AreEqual(files.Length, calls.Count);
                        Assert.AreEqual(files.Sum(file => file.Bytes), task.Result);
                        var studio = MNNStudioModel.Read(destination);
                        Assert.AreEqual(MNNSpeechModelKind.SherpaVitsChinese, studio.TtsProfile);
                        Assert.AreEqual(MNNStudioTask.SpeechSynthesis, studio.Task);
                        Assert.AreEqual(5, studio.TtsVoices.Length);
                        Assert.IsTrue(studio.HasGenerationPipeline);
                        Assert.IsTrue(File.Exists(Path.Combine(destination, "VITS-SOURCE-NOTICE.txt")));
                    }
                    finally
                    {
                        if (Directory.Exists(destination)) Directory.Delete(destination, true);
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator MossNanoDownloadsPinnedStagesAndResumesWithoutRequests()
            => DownloadMoss(MNNVerifiedTtsModel.MossRepository, "MossNano", 10);

        [UnityTest]
        public IEnumerator MossNanoCachedDownloadsPinnedStagesAndResumesWithoutRequests()
            => DownloadMoss(MNNVerifiedTtsModel.MossCachedRepository, "MossNanoCached", 9);

        private static IEnumerator DownloadMoss(string repository, string folder, int count)
        {
            string source = Path.Combine(Environment.GetEnvironmentVariable("MNN_TEST_QWEN3_TTS_ROOT"), folder);
            string destination = Path.Combine(AdditionalTtsValidation.Artifacts, "download-routing", Guid.NewGuid().ToString("N"));
            var files = MNNVerifiedTtsModel.Files(repository);
            Assert.AreEqual(count, files.Length);
            var calls = new ConcurrentBag<string>();
            using (var client = new HttpClient(new LocalModelScopeFiles(source, repository, files, calls)))
            using (var service = new MNNModelDownloadService(client, "https://fixture.test", new MNNModelDownloadOptions{MaxConnections = 1, RetryCount = 0, PreservePartialDownloads = true}))
            {
                try
                {
                    Task<long> task = service.DownloadRepositoryAsync(repository, destination);
                    while (!task.IsCompleted) yield return null;
                    Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
                    MNNVerifiedTtsModel.Prepare(destination, repository);
                    Assert.AreEqual(files.Length, calls.Count);
                    Assert.AreEqual(files.Sum(file => file.Bytes), task.Result);
                    var studio = MNNStudioModel.Read(destination);
                    Assert.AreEqual(MNNSpeechModelKind.MossNano, studio.TtsProfile);
                    Assert.AreEqual(MNNStudioTask.SpeechSynthesis, studio.Task);
                    Assert.AreEqual(18, studio.TtsVoices.Length);
                    Assert.IsTrue(studio.HasGenerationPipeline);
                    task = service.DownloadRepositoryAsync(repository, destination);
                    while (!task.IsCompleted) yield return null;
                    Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
                    Assert.AreEqual(files.Length, calls.Count, "Verified complete models must resume without HTTP.");
                }
                finally { if (Directory.Exists(destination)) Directory.Delete(destination, true); }
            }
        }

        [UnityTest]
        public IEnumerator DhVaaniPinsCompleteAssetsAndResumesWithoutRequests() => PinnedReferenceTts(0);

        [UnityTest]
        public IEnumerator Qwen3ExplicitPinsCompleteAssetsAndResumesWithoutRequests() => PinnedReferenceTts(1);

        [UnityTest]
        public IEnumerator PiperLessacPinsVoiceAndEnglishDataAndResumesWithoutRequests() => PinnedReferenceTts(2);

        [UnityTest]
        public IEnumerator KokoroSinglePinsOriginalEmptyStylesAndMatchingLexicons() => PinnedReferenceTts(3);

        [UnityTest]
        public IEnumerator PocketPinsMnnOnlyAssetsAndResumesWithoutRequests() => PinnedReferenceTts(4);

        [UnityTest]
        public IEnumerator IndicF5PinsMatchedVocabularyAndResumesWithoutRequests() => PinnedReferenceTts(5);

        private static IEnumerator PinnedReferenceTts(int profile)
        {
            bool qwen = profile == 1, piper = profile == 2, kokoro = profile == 3, pocket = profile == 4, indic = profile == 5;
            string repository = indic ? MNNVerifiedTtsModel.IndicF5Repository : pocket ? MNNVerifiedTtsModel.PocketRepository : kokoro ? MNNVerifiedTtsModel.KokoroSingleRepository : piper ? MNNVerifiedTtsModel.PiperLessacRepository : qwen ? MNNVerifiedTtsModel.Qwen3ExplicitRepository : MNNVerifiedTtsModel.DhVaaniRepository;
            string source = Path.Combine(Environment.GetEnvironmentVariable("MNN_TEST_QWEN3_TTS_ROOT"), indic ? "IndicF5-MNN" : pocket ? "Pocket-TTS-MNN" : kokoro ? "Jeryuk--kokoro-v1_0-MNN" : piper ? "PiperLessac" : qwen ? "Qwen3-TTS-Explicit" : "DhVaani");
            string destination = Path.Combine(AdditionalTtsValidation.Artifacts, "download-routing", Guid.NewGuid().ToString("N"));
            var files = MNNVerifiedTtsModel.HuggingFaceFiles(repository);
            Assert.AreEqual(indic ? 7 : pocket ? 11 : kokoro ? 8 : piper ? 11 : qwen ? 47 : 8, files.Length);
            var calls = new ConcurrentBag<string>();
            using (var client = new HttpClient(new LocalFiles(source, repository, files, calls)))
            using (var service = new MNNModelDownloadService(client, options: new MNNModelDownloadOptions{MaxConnections = 1, RetryCount = 0, PreservePartialDownloads = true},
                mirrorPool: new MNNHuggingFaceMirrorPool(new[]{new MNNHuggingFaceMirror("fixture", "https://fixture.test", "https://fixture.test")})))
            {
                try
                {
                    Task<long> task = service.DownloadHuggingFaceRepositoryAsync(repository, destination);
                    while (!task.IsCompleted) yield return null;
                    Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
                    MNNVerifiedTtsModel.Prepare(destination, repository);
                    Assert.AreEqual(files.Sum(file => file.Bytes), task.Result);
                    CollectionAssert.AreEquivalent(files.Select(file => "/" + (file.SourceRepository ?? repository) + "/resolve/" + file.Revision + "/" + file.Path), calls);
                    var studio = MNNStudioModel.Read(destination);
                    Assert.AreEqual(indic ? MNNSpeechModelKind.IndicF5 : pocket ? MNNSpeechModelKind.Pocket : kokoro ? MNNSpeechModelKind.Kokoro : piper ? MNNSpeechModelKind.Piper : qwen ? MNNSpeechModelKind.Qwen3Explicit : MNNSpeechModelKind.DhVaani, studio.TtsProfile);
                    Assert.AreEqual(MNNStudioTask.SpeechSynthesis, studio.Task);
                    Assert.IsTrue(studio.HasGenerationPipeline);
                    Assert.AreEqual(!qwen && !piper && !kokoro && !pocket, studio.RequiresReferenceTranscript);
                    Assert.AreEqual(!qwen && !piper && !kokoro, studio.RequiresReferenceAudio);
                    task = service.DownloadHuggingFaceRepositoryAsync(repository, destination);
                    while (!task.IsCompleted) yield return null;
                    Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
                    Assert.AreEqual(files.Length, calls.Count);
                    task = service.DownloadHuggingFaceRepositoryAsync(repository, destination, "old-silent-export");
                    while (!task.IsCompleted) yield return null;
                    Assert.IsTrue(task.IsFaulted);
                    Assert.AreEqual(files.Length, calls.Count);
                }
                finally { if (Directory.Exists(destination)) Directory.Delete(destination, true); }
            }
        }

        [UnityTest]
        public IEnumerator BertVits2MirrorsPinTheSameCompleteInferenceAssetsOnBothHubs() => PinnedMirrors(true);

        [UnityTest]
        public IEnumerator ExpandedKokoroPinsBothHubsAndReusesItsCompleteNamedPrefix() => PinnedMirrors(false);

        private static IEnumerator PinnedMirrors(bool bert)
        {
            string source = bert ? GenerationTestData.Model("bert-vits2-MNN") : Path.Combine(Environment.GetEnvironmentVariable("MNN_TEST_LOCAL_MODEL_ROOT"), "starsharp06sharp--kokoro-v1_1");
            string[] repositories = bert ? new[]{MNNVerifiedTtsModel.BertVits2CustomRepository, MNNVerifiedTtsModel.BertVits2CustomRepository, MNNVerifiedTtsModel.BertVits2MirrorRepository}
                : new[]{MNNVerifiedTtsModel.KokoroExpandedRepository, MNNVerifiedTtsModel.KokoroExpandedRepository};
            for (int i = 0; i < repositories.Length; ++i)
            {
                bool hf = i > 0;
                string repository = repositories[i];
                var files = hf ? MNNVerifiedTtsModel.HuggingFaceFiles(repository) : MNNVerifiedTtsModel.Files(repository);
                Assert.AreEqual(bert ? 23 : 8, files.Length);
                var calls = new ConcurrentBag<string>();
                string destination = Path.Combine(AdditionalTtsValidation.Artifacts, "download-routing", Guid.NewGuid().ToString("N"));
                using (var client = new HttpClient(hf ? (HttpMessageHandler)new LocalFiles(source, repository, files, calls) : new LocalModelScopeFiles(source, repository, files, calls)))
                using (var service = new MNNModelDownloadService(client, options: new MNNModelDownloadOptions{PreservePartialDownloads = true, RetryCount = 0, MaxConnections = 1},
                    mirrorPool: new MNNHuggingFaceMirrorPool(new[]{new MNNHuggingFaceMirror("fixture", "https://fixture.test", "https://fixture.test")})))
                {
                    try
                    {
                        Task<long> task = hf ? service.DownloadHuggingFaceRepositoryAsync(repository, destination) : service.DownloadRepositoryAsync(repository, destination);
                        while (!task.IsCompleted) yield return null;
                        Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
                        Assert.AreEqual(files.Sum(file => file.Bytes), task.Result);
                        Assert.AreEqual(files.Length, calls.Count);
                        var model = MNNStudioModel.Read(destination);
                        Assert.AreEqual(bert ? MNNSpeechModelKind.BertVits2 : MNNSpeechModelKind.Kokoro, model.TtsProfile);
                        if (!bert) Assert.AreEqual(103, model.TtsVoices.Length);
                        Assert.IsTrue(model.HasGenerationPipeline);
                        task = hf ? service.DownloadHuggingFaceRepositoryAsync(repository, destination) : service.DownloadRepositoryAsync(repository, destination);
                        while (!task.IsCompleted) yield return null;
                        Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
                        Assert.AreEqual(files.Length, calls.Count);
                    }
                    finally { if (Directory.Exists(destination)) Directory.Delete(destination, true); }
                }
            }
        }

        private sealed class LocalModelScopeFiles : HttpMessageHandler
        {
            private readonly string _source, _repository;
            private readonly MNNVerifiedTtsModel.FileEntry[] _files;
            private readonly ConcurrentBag<string> _calls;
            internal LocalModelScopeFiles(string source, string repository, MNNVerifiedTtsModel.FileEntry[] files, ConcurrentBag<string> calls)
            { _source = source; _repository = repository; _files = files; _calls = calls; }
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Assert.AreEqual("/api/v1/models/" + _repository + "/repo", request.RequestUri.AbsolutePath);
                string query = request.RequestUri.Query;
                var file = _files.Single(entry => query == "?Revision=" + entry.Revision + "&FilePath=" + Uri.EscapeDataString(entry.Path));
                _calls.Add(file.Path);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {Content = new StreamContent(File.OpenRead(Path.Combine(_source, file.Path)))});
            }
        }

        private sealed class LocalFiles : HttpMessageHandler
        {
            private readonly string _source, _repository;
            private readonly MNNVerifiedTtsModel.FileEntry[] _files;
            private readonly ConcurrentBag<string> _calls;
            internal LocalFiles(string source, string repository, MNNVerifiedTtsModel.FileEntry[] files, ConcurrentBag<string> calls)
            { _source = source; _repository = repository; _files = files; _calls = calls; }
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                string path = Uri.UnescapeDataString(request.RequestUri.AbsolutePath);
                var file = _files.Single(entry => path == "/" + (entry.SourceRepository ?? _repository) + "/resolve/" + entry.Revision + "/" + entry.Path);
                _calls.Add(path);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {Content = new StreamContent(File.OpenRead(Path.Combine(_source, file.Path)))});
            }
        }

        [UnityTest]
        public IEnumerator CompleteVerifiedModelsResumeWithoutNetworkRequests()
        {
            string[] repositories = {"dr3334/Kokoro-82M-v1.0-MNN", "dr3334/Supertonic-3-INT8-MNN", "wangjazz/kokoro-82m-mnn", "developerabu/vits-tts-mnn", MNNVerifiedTtsModel.Kokoro11Repository, MNNVerifiedTtsModel.MmsTamilRepository, MNNVerifiedTtsModel.SherpaVitsRepository};
            string[] variables = {"MNN_TEST_KOKORO_ROOT", "MNN_TEST_SUPERTONIC3_ROOT", "MNN_TEST_KOKORO_CONSTANT_ROOT", "MNN_TEST_INDIC_VITS_ROOT", "MNN_TEST_KOKORO11_ROOT", "MNN_TEST_MMS_TAMIL_ROOT", "MNN_TEST_SHERPA_VITS_ROOT"};
            var handler = new RejectNetwork();
            using (var client = new HttpClient(handler))
            using (var service = new MNNModelDownloadService(client, options: new MNNModelDownloadOptions{PreservePartialDownloads = true},
                mirrorPool: new MNNHuggingFaceMirrorPool(new[]{new MNNHuggingFaceMirror("fixture", "https://fixture.test", "https://fixture.test")})))
                for (int i = 0; i < repositories.Length; ++i)
                {
                    string directory = Environment.GetEnvironmentVariable(variables[i]);
                    Assert.That(directory, Is.Not.Null.And.Not.Empty);
                    var files = i >= 2 ? MNNVerifiedTtsModel.HuggingFaceFiles(repositories[i]) : MNNVerifiedTtsModel.Files(repositories[i]);
                    MNNVerifiedTtsModel.Validate(directory, repositories[i]);
                    Task<long> task = i >= 2 ? service.DownloadHuggingFaceRepositoryAsync(repositories[i], directory) : service.DownloadRepositoryAsync(repositories[i], directory);
                    while (!task.IsCompleted)
                        yield return null;
                    Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
                    Assert.AreEqual(files.Sum(file => file.Bytes), task.Result);
                }
            Assert.AreEqual(0, handler.Calls, "All resources must be verified and reused without metadata or file HTTP calls.");
        }

        [UnityTest]
        public IEnumerator MigratedVerifiedTtsModelsResumeFromStreamingAssetsWithoutRequests()
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_LOCAL_MODEL_ROOT");
            Assert.That(root, Is.Not.Null.And.Not.Empty);
            string[] repositories = {"dr3334/Kokoro-82M-v1.0-MNN", "dr3334/Supertonic-3-INT8-MNN", MNNVerifiedTtsModel.MossRepository, MNNVerifiedTtsModel.MossCachedRepository,
                "wangjazz/kokoro-82m-mnn", MNNVerifiedTtsModel.IndicVitsRepository, MNNVerifiedTtsModel.Kokoro11Repository, MNNVerifiedTtsModel.MmsTamilRepository,
                MNNVerifiedTtsModel.SherpaVitsRepository, MNNVerifiedTtsModel.DhVaaniRepository, MNNVerifiedTtsModel.Qwen3ExplicitRepository,
                MNNVerifiedTtsModel.PiperLessacRepository, MNNVerifiedTtsModel.KokoroSingleRepository, MNNVerifiedTtsModel.KokoroExpandedRepository, MNNVerifiedTtsModel.PocketRepository, MNNVerifiedTtsModel.IndicF5Repository};
            string[] folders = {"Kokoro-82M-v1.0-MNN", "Supertonic-3-INT8-MNN", "MossNano", "MossNanoCached", "kokoro-82m-mnn", "vits-tts-mnn", "kokoro-v1_1",
                "mms-tts-tam-mnn", "sherpa-vits-zh-ll", "DhVaani", "Qwen3-TTS-Explicit", "PiperLessac", "Jeryuk--kokoro-v1_0-MNN", "starsharp06sharp--kokoro-v1_1", "Pocket-TTS-MNN", "IndicF5-MNN"};
            var identities = new System.Collections.Generic.List<string>();
            var handler = new RejectNetwork();
            using (var client = new HttpClient(handler))
            using (var service = new MNNModelDownloadService(client, options: new MNNModelDownloadOptions{PreservePartialDownloads = true},
                mirrorPool: new MNNHuggingFaceMirrorPool(new[]{new MNNHuggingFaceMirror("fixture", "https://fixture.test", "https://fixture.test")})))
                for (int i = 0; i < repositories.Length; ++i)
                {
                    bool hf = i >= 4;
                    string directory = Path.Combine(root, folders[i]), repository = repositories[i];
                    var files = hf ? MNNVerifiedTtsModel.HuggingFaceFiles(repository) : MNNVerifiedTtsModel.Files(repository);
                    Task<long> task = hf ? service.DownloadHuggingFaceRepositoryAsync(repository, directory) : service.DownloadRepositoryAsync(repository, directory);
                    while (!task.IsCompleted) yield return null;
                    Assert.IsFalse(task.IsFaulted, repository + ": " + task.Exception);
                    Assert.AreEqual(files.Sum(file => file.Bytes), task.Result, repository);
                    Assert.IsTrue(MNNStudioModel.Read(directory).HasGenerationPipeline, repository);
                    identities.Add(directory + "\t" + (hf ? "hf:" + repository + "@" + MNNVerifiedTtsModel.ResolveHuggingFaceRevision(repository, null) : repository));
                }
            Assert.AreEqual(0, handler.Calls);
            File.WriteAllLines(Path.Combine(AdditionalTtsValidation.Artifacts, "migrated-tts-installations.tsv"), identities);
        }

        private sealed class RejectNetwork : HttpMessageHandler
        {
            internal int Calls;
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref Calls);
                throw new InvalidOperationException("Unexpected network request: " + request.RequestUri);
            }
        }
    }
}
