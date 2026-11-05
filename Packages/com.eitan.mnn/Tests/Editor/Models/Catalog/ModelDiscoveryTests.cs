using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MNN.Unity.Tests
{
    // Offline HTTP fixtures test coverage and transactions, not model inference.
    public class ModelDiscoveryTests
    {
        [UnityTest]
        public IEnumerator TtsLlmFilesRequireAWorkingTaskProfileWhileChatKeepsItsGenericPipeline()
        {
            using (var client = Client(request => request.RequestUri.AbsolutePath == "/api/models"
                ? Json("[" + Hf("community/Qwen3-TTS-mnn", "talker.mnn", "llm_config.json", "tokenizer.txt") + "," +
                    Hf("community/Chat-mnn", "llm.mnn", "llm_config.json", "tokenizer.txt") + "," +
                    Hf(MNNVerifiedTtsModel.DhVaaniRepository, "fm_decoder_int8.mnn") + "," + Hf(MNNVerifiedTtsModel.Qwen3ExplicitRepository, "talker_step.mnn") + "," + Hf(MNNVerifiedTtsModel.PiperLessacRepository, "en_US-lessac-high.mnn") + "," + Hf(MNNVerifiedTtsModel.KokoroSingleRepository, "tts.mnn") + "," + Hf(MNNVerifiedTtsModel.PocketRepository, "mnn/flow_lm_main_w8.mnn") + "," + Hf(MNNVerifiedTtsModel.IndicF5Repository, "indicf5_dit_int8wq.mnn") + "]") : Featured(request)))
            {
                var catalog = new MNNModelRepository();
                var task = catalog.DiscoverHuggingFaceModelsAsync(client, Pool());
                yield return Wait(task);
                Assert.False(task.IsFaulted, task.Exception?.ToString());
                var indic = catalog.models.Single(model => model.repositoryId == MNNVerifiedTtsModel.IndicF5Repository);
                Assert.AreEqual(MNNModelCategory.SpeechSynthesis.ToString(), indic.category);
                Assert.True(indic.runtimeAvailable);
                Assert.AreEqual(MNNVerifiedTtsModel.IndicF5Revision, indic.revision);
                Assert.AreEqual(MNNVerifiedTtsModel.IndicF5Files().Sum(file => file.Bytes), indic.fileSize);
                var pocket = catalog.models.Single(model => model.repositoryId == MNNVerifiedTtsModel.PocketRepository);
                Assert.AreEqual(MNNModelCategory.SpeechSynthesis.ToString(), pocket.category);
                Assert.True(pocket.runtimeAvailable);
                Assert.AreEqual(MNNVerifiedTtsModel.PocketRevision, pocket.revision);
                Assert.AreEqual(MNNVerifiedTtsModel.PocketFiles().Sum(file => file.Bytes), pocket.fileSize);
                StringAssert.Contains("without ONNX Runtime", pocket.supportSummary);
                var single = catalog.models.Single(model => model.repositoryId == MNNVerifiedTtsModel.KokoroSingleRepository);
                Assert.AreEqual(MNNModelCategory.SpeechSynthesis.ToString(), single.category);
                Assert.True(single.runtimeAvailable);
                Assert.AreEqual(MNNVerifiedTtsModel.KokoroSingleRevision, single.revision);
                Assert.AreEqual(MNNVerifiedTtsModel.KokoroSingleFiles().Sum(file => file.Bytes), single.fileSize);
                var piper = catalog.models.Single(model => model.repositoryId == MNNVerifiedTtsModel.PiperLessacRepository);
                Assert.AreEqual(MNNModelCategory.SpeechSynthesis.ToString(), piper.category);
                Assert.True(piper.runtimeAvailable);
                Assert.AreEqual(MNNVerifiedTtsModel.PiperLessacRevision, piper.revision);
                Assert.AreEqual(MNNVerifiedTtsModel.PiperLessacFiles().Sum(file => file.Bytes), piper.fileSize);
                var qwen = catalog.models.Single(model => model.repositoryId == MNNVerifiedTtsModel.Qwen3ExplicitRepository);
                Assert.AreEqual(MNNModelCategory.SpeechSynthesis.ToString(), qwen.category);
                Assert.True(qwen.runtimeAvailable);
                Assert.AreEqual(MNNVerifiedTtsModel.Qwen3ExplicitRevision, qwen.revision);
                Assert.AreEqual(MNNVerifiedTtsModel.Qwen3ExplicitFiles().Sum(file => file.Bytes), qwen.fileSize);
                var unverified = catalog.models.Single(model => model.repositoryId == "community/Qwen3-TTS-mnn");
                Assert.AreEqual(MNNModelCategory.SpeechSynthesis.ToString(), unverified.category);
                Assert.False(unverified.runtimeAvailable, "An LLM-shaped TTS export must not imply a working speech pipeline.");
                Assert.True(catalog.models.Single(model => model.repositoryId == "community/Chat-mnn").runtimeAvailable);
                var verified = catalog.models.Single(model => model.repositoryId == MNNVerifiedTtsModel.DhVaaniRepository);
                Assert.AreEqual(MNNModelCategory.SpeechSynthesis.ToString(), verified.category);
                Assert.AreEqual(MNNVerifiedTtsModel.DhVaaniRevision, verified.revision);
                Assert.AreEqual(MNNVerifiedTtsModel.HuggingFaceFiles(verified.repositoryId).Sum(file => file.Bytes), verified.fileSize);
                Assert.True(verified.runtimeAvailable);
            }
        }

        [UnityTest]
        public IEnumerator MultilingualOcrCatalogPinsTheInstallPlanAndKeepsOtherExportsUnvalidated()
        {
            using (var client = Client(request => request.RequestUri.AbsolutePath == "/api/models"
                ? Json("[" + Hf(MNNPaddleTextModel.Repository, "en_PP-OCRv5_mobile_rec_infer.mnn", "PP-OCRv5_mobile_rec_fp16.mnn") + "," + Hf("other/ppocr-mnn", "rec.mnn") + "]")
                : Featured(request)))
            {
                var repository = new MNNModelRepository();
                var task = repository.DiscoverHuggingFaceModelsAsync(client, Pool());
                yield return Wait(task);
                Assert.False(task.IsFaulted, task.Exception?.ToString());
                var model = repository.models.Single(item => item.repositoryId == MNNPaddleTextModel.Repository);
                Assert.AreEqual(MNNPaddleTextModel.Revision, model.revision);
                Assert.AreEqual(MNNPaddleTextModel.Files().Sum(file => file.Bytes), model.fileSize);
                Assert.AreEqual("OCRModel", model.category);
                Assert.True(model.runtimeAvailable);
                StringAssert.Contains("twelve language/script profiles", model.supportSummary);
                StringAssert.Contains("Chinese v4 and fp16", model.supportSummary);
                StringAssert.Contains("page OCR", model.supportSummary);
                StringAssert.Contains("Orientation classification is not included", model.supportSummary);
                Assert.False(repository.models.Single(item => item.repositoryId == "other/ppocr-mnn").runtimeAvailable);
            }
        }

        [UnityTest]
        public IEnumerator DetectionProfileDoesNotEnableUnverifiedYoloRepositories()
        {
            using (var client = Client(request => request.RequestUri.AbsolutePath.Contains("/repo/files")
                ? Json("{\"Code\":200,\"Data\":{\"Files\":[{\"Path\":\"yolov8n.mnn\",\"Type\":\"blob\",\"Size\":6342828}]}}")
                : MsPage(MNNYoloV8Model.Repository, "other/unverified-yolo-mnn")))
            {
                var catalog = new MNNModelRepository();
                var task = catalog.DiscoverModelScopeModelsAsync(client, "https://api.unit.test");
                yield return Wait(task);
                Assert.False(task.IsFaulted, task.Exception?.ToString());
                var verified = catalog.models.Single(model => model.repositoryId == MNNYoloV8Model.Repository);
                Assert.AreEqual(MNNModelCategory.ObjectDetection.ToString(), verified.category);
                Assert.True(verified.runtimeAvailable);
                Assert.False(catalog.models.Single(model => model.repositoryId == "other/unverified-yolo-mnn").runtimeAvailable);
            }
        }

        [UnityTest]
        public IEnumerator ModelScope_SearchesCommunityAndTagsAndVerifiesArtifacts()
        {
            using (var client = Client(request =>
            {
                string path = request.RequestUri.AbsolutePath;
                if (path.Contains("/repo/files"))
                    return Json("{\"Code\":200,\"Data\":{\"Files\":[{\"Path\":\"" + (path.Contains("unrelated") || path.Contains("auxiliary") ? "model.safetensors" : "model.mnn") + "\",\"Type\":\"blob\",\"Size\":12}]}}");
                string query = request.RequestUri.Query;
                if (query.Contains("owner=MNN"))
                    return MsPage("MNN/auxiliary");
                if (query.Contains("filter.library=mnn"))
                    return MsPage("community/unnamed-format", "MNN/auxiliary");
                StringAssert.Contains("search=mnn", query);
                return MsPage("other/custom-mnn", "other/unrelated");
            }))
            {
                var repository = new MNNModelRepository();
                repository.models.Add(new MNNModelInfo {id = "hf:existing/model", provider = MNNModelProvider.HuggingFace});
                var task = repository.DiscoverModelScopeModelsAsync(client, "https://api.unit.test");
                yield return Wait(task);
                Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
                CollectionAssert.AreEquivalent(new[] {"hf:existing/model", "MNN/auxiliary", "community/unnamed-format", "other/custom-mnn"}, repository.models.Select(model => model.id));
                Assert.IsEmpty(repository.models.Single(model => model.id == "MNN/auxiliary").mnnFiles);
                Assert.IsFalse(repository.models.Single(model => model.id == "other/custom-mnn").runtimeAvailable, "A raw graph has no verified task pipeline.");
            }
        }

        [UnityTest]
        public IEnumerator ModelScope_SearchAndManifestRetryRateLimitsBeforePublishingTheCompleteCatalog()
        {
            int search = 0, manifest = 0;
            using (var client = Client(request =>
            {
                bool listing = request.RequestUri.AbsolutePath.Contains("repo/files");
                int count = listing ? ++manifest : ++search;
                if (count == 1)
                {
                    var response = new HttpResponseMessage((HttpStatusCode)429);
                    response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.Zero);
                    return response;
                }
                return listing ? Json("{\"Code\":200,\"Data\":{\"Files\":[{\"Path\":\"model.mnn\",\"Type\":\"blob\"}]}}") : MsPage("community/limited-mnn");
            }))
            {
                var repository = new MNNModelRepository();
                var task = repository.DiscoverModelScopeModelsAsync(client, "https://limited-discovery.test");
                yield return Wait(task);
                Assert.False(task.IsFaulted, task.Exception?.ToString());
                Assert.AreEqual("community/limited-mnn", repository.models.Single().id);
                Assert.AreEqual(4, search);
                Assert.AreEqual(2, manifest);
            }
        }

        [UnityTest]
        public IEnumerator ModelScope_ManifestFailureKeepsBothPreviousSources()
        {
            using (var client = Client(request => request.RequestUri.AbsolutePath.Contains("repo/files") ? Json("{\"Code\":500}") : MsPage("author/mnn")))
            {
                var repository = new MNNModelRepository();
                var previous = repository.models;
                previous.Add(new MNNModelInfo {id = "previous"});
                var task = repository.DiscoverModelScopeModelsAsync(client);
                yield return Wait(task);
                Assert.IsTrue(task.IsFaulted);
                Assert.AreSame(previous, repository.models);
                Assert.AreEqual("previous", repository.models.Single().id);
            }
        }

        [UnityTest]
        public IEnumerator ModelScope_DeletedSearchResultIsReportedWithoutHidingAvailableModels()
        {
            using (var client = Client(request =>
            {
                if (!request.RequestUri.AbsolutePath.Contains("repo/files"))
                    return MsPage("community/deleted-mnn", "community/available-mnn");
                return request.RequestUri.AbsolutePath.Contains("deleted") ? new HttpResponseMessage(HttpStatusCode.NotFound)
                    : Json("{\"Code\":200,\"Data\":{\"Files\":[{\"Path\":\"model.mnn\",\"Type\":\"blob\"}]}}");
            }))
            {
                var repository = new MNNModelRepository();
                var task = repository.DiscoverModelScopeModelsAsync(client);
                yield return Wait(task);
                Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
                Assert.AreEqual("community/available-mnn", repository.models.Single().id);
                StringAssert.Contains("community/deleted-mnn", repository.DiscoveryWarnings.Single());
            }
        }

        [UnityTest]
        public IEnumerator HuggingFace_DefaultDiscoveryFollowsPagesUnionsOwnersAndKeepsModelScope()
        {
            int queries = 0;
            using (var client = Client(request =>
            {
                if (request.RequestUri.AbsolutePath != "/api/models")
                    return Featured(request);
                string query = request.RequestUri.Query;
                if (query.Contains("cursor=page2"))
                    return Json("[" + Hf("community/second", "variants/int4/model.mnn") + "]");
                ++queries;
                if (query.Contains("author=taobao-mnn"))
                {
                    var response = Json("[" + Hf("taobao-mnn/first", "model.mnn") + "]");
                    response.Headers.TryAddWithoutValidation("Link", "<https://api.unit.test/api/models?cursor=page2>; rel=\"next\"");
                    return response;
                }
                if (query.Contains("filter=mnn"))
                    return Json("[" + Hf("community/tag-only", "model.mnn", "config.json", "llm_config.json", "tokenizer.mtok") + "," + Hf(MNNBertEmbeddingModel.MiniLmRepository, "minilm.mnn") + "]");
                StringAssert.Contains("search=mnn", query);
                return Json("[" + Hf("community/tag-only", "model.mnn", "config.json", "llm_config.json", "tokenizer.mtok") + "," + Hf("irrelevant/mnn-in-name", "model.safetensors") + "," + Hf("community/archive-mnn", "deployment.zip") + "]");
            }))
            {
                var repository = new MNNModelRepository();
                repository.models.Add(new MNNModelInfo {id = "MNN/existing", provider = MNNModelProvider.ModelScope});
                var task = repository.LoadHuggingFaceModelsAsync(httpClient: client, mirrorPool: Pool());
                yield return Wait(task);
                Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
                Assert.AreEqual(3, queries);
                Assert.IsTrue(repository.models.Any(model => model.id == "MNN/existing"));
                Assert.AreEqual(1, repository.models.Count(model => model.id == "hf:community/tag-only"));
                Assert.IsTrue(repository.models.Single(model => model.id == "hf:community/tag-only").runtimeAvailable, "The bundled official tokenizer supports configured mtok pipelines.");
                Assert.IsTrue(repository.models.Any(model => model.id == "hf:community/second"));
                Assert.IsFalse(repository.models.Any(model => model.id == "hf:irrelevant/mnn-in-name"));
                Assert.IsFalse(repository.models.Single(model => model.id == "hf:community/archive-mnn").runtimeAvailable);
                CollectionAssert.AreEqual(new[] {"deployment.zip"}, repository.models.Single(model => model.id == "hf:community/archive-mnn").mnnFiles);
                Assert.IsTrue(MNNModelRepository.FeaturedHuggingFaceRepositories.All(id => repository.models.Any(model => model.repositoryId == id)));
                Assert.IsTrue(repository.models.Where(model => MNNModelRepository.FeaturedHuggingFaceRepositories.Contains(model.repositoryId)).All(model => model.mnnFiles.Length > 0 && model.runtimeAvailable));
                var miniLm = repository.models.Single(model => model.repositoryId == MNNBertEmbeddingModel.MiniLmRepository);
                Assert.AreEqual(MNNModelCategory.EmbeddingModel.ToString(), miniLm.category);
                Assert.AreEqual(MNNBertEmbeddingModel.Revision(MNNBertEmbeddingModel.MiniLmRepository), miniLm.revision);
                Assert.IsTrue(miniLm.runtimeAvailable);
            }
        }

        [UnityTest]
        public IEnumerator HuggingFace_InvalidOrRepeatedNextPageDoesNotPublishPartialCatalog()
        {
            foreach (string next in new[] {"https://api.unit.test/api/models?cursor=repeat", "https://other.unit.test/api/models?cursor=page2"})
            {
                using (var client = Client(request =>
                {
                    var response = Json("[" + Hf("community/model-mnn", "model.mnn") + "]");
                    response.Headers.TryAddWithoutValidation("Link", "<" + next + ">; rel=\"next\"");
                    return response;
                }))
                {
                    var repository = new MNNModelRepository();
                    var previous = repository.models;
                    var task = repository.DiscoverHuggingFaceModelsAsync(client, Pool());
                    yield return Wait(task);
                    Assert.IsTrue(task.IsFaulted);
                    Assert.AreSame(previous, repository.models);
                }
            }
        }

        [UnityTest]
        public IEnumerator HuggingFace_FailedSearchRestartsOnAnotherMirror()
        {
            using (var client = Client(request =>
            {
                if (request.RequestUri.Host == "failed.unit.test")
                    return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
                return request.RequestUri.AbsolutePath == "/api/models" ? Json("[" + Hf("community/model-mnn", "model.mnn") + "]") : Featured(request);
            }))
            {
                var pool = new MNNHuggingFaceMirrorPool(new[] {new MNNHuggingFaceMirror("Failed", "https://failed.unit.test"), new MNNHuggingFaceMirror("Working", "https://api.unit.test")});
                var repository = new MNNModelRepository();
                var task = repository.DiscoverHuggingFaceModelsAsync(client, pool);
                yield return Wait(task);
                Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
                Assert.IsTrue(repository.models.Any(model => model.id == "hf:community/model-mnn"));
            }
        }

        [UnityTest]
        public IEnumerator DiscoveryCancellationKeepsPreviousCatalog()
        {
            using (var cancellation = new CancellationTokenSource())
            using (var client = Client(request => throw new AssertionException("Canceled request reached HTTP.")))
            {
                cancellation.Cancel();
                var repository = new MNNModelRepository();
                var previous = repository.models;
                foreach (var task in new[] {repository.DiscoverModelScopeModelsAsync(client, cancellationToken: cancellation.Token), repository.DiscoverHuggingFaceModelsAsync(client, Pool(), cancellation.Token)})
                {
                    yield return Wait(task);
                    Assert.IsTrue(task.IsCanceled);
                    Assert.AreSame(previous, repository.models);
                }
            }
        }

        [Test]
        public void InstallFoldersSeparateProvidersAndAuthorsAndRecognizeLegacyIdentity()
        {
            var models = new[]
            {
                new MNNModelInfo {name = "same-model", provider = MNNModelProvider.ModelScope, repositoryId = "MNN/same-model"},
                new MNNModelInfo {name = "same-model", provider = MNNModelProvider.ModelScope, repositoryId = "author/same-model"},
                new MNNModelInfo {name = "same-model", provider = MNNModelProvider.HuggingFace, repositoryId = "MNN/same-model"},
                new MNNModelInfo {name = "same-model", provider = MNNModelProvider.HuggingFace, repositoryId = "author/same-model"}
            };
            Assert.AreEqual(4, models.Select(MNNModelRepository.InstallFolderName).Distinct().Count());
            Assert.AreEqual("same-model", MNNModelRepository.InstallFolderName(models[0]));
            Assert.IsTrue(MNNModelRepository.MatchesInstallMarker(models[3], "hf:author/same-model@pinned-revision"));
            Assert.IsFalse(MNNModelRepository.MatchesInstallMarker(models[3], "hf:MNN/same-model@pinned-revision"));
            Assert.IsFalse(MNNModelRepository.MatchesInstallMarker(models[0], "hf:MNN/same-model@pinned-revision"));
        }

        [TestCase("mnneely/ppo-Cartpole", false)]
        [TestCase("someone/model-mnn", true)]
        [TestCase("someone/MNN_model", true)]
        [TestCase("Androidonnxfork/MNNsd", true)]
        public void ArchiveCandidatesRequireAnMnnLabelRatherThanAnUnrelatedAccountSubstring(string repository, bool expected)
        {
            Assert.AreEqual(expected, MNNModelRepository.HasMnnRepositoryArtifacts(repository, null, new[] {"deployment.zip"}));
            Assert.IsTrue(MNNModelRepository.HasMnnRepositoryArtifacts(repository, new[] {"mnn"}, new[] {"deployment.zip"}));
            Assert.IsTrue(MNNModelRepository.HasMnnRepositoryArtifacts(repository, null, new[] {"graph.mnn"}));
        }

        private static HttpResponseMessage Featured(HttpRequestMessage request)
        {
            string id = Uri.UnescapeDataString(request.RequestUri.AbsolutePath.Substring("/api/models/".Length)).Split(new[] {"/revision/"}, StringSplitOptions.None)[0];
            return Json(Hf(id, "model.mnn"));
        }

        [UnityTest, Category("RequiresNetwork"), Explicit("Searches both live public hubs and reads manifests/metadata only. Does not download weights or run inference.")]
        public IEnumerator LiveDiscovery_AuditsBothPublicProviders()
        {
            string output = Path.GetFullPath(Environment.GetEnvironmentVariable("MNN_TEST_CATALOG_OUTPUT") ?? "TestArtifacts~/ModelDiscovery/PackageAudit");
            Directory.CreateDirectory(output);
            var repository = new MNNModelRepository();
            using (var client = new HttpClient {Timeout = TimeSpan.FromSeconds(45)})
            {
                var task = repository.DiscoverModelScopeModelsAsync(client);
                yield return Wait(task, 900);
                Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
                task = repository.DiscoverHuggingFaceModelsAsync(client, new MNNHuggingFaceMirrorPool(new[] {new MNNHuggingFaceMirror("Official", "https://huggingface.co")}));
                yield return Wait(task, 900);
                Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
            }
            File.WriteAllText(Path.Combine(output, "package-catalog.json"), JsonUtility.ToJson(repository, true));
            File.WriteAllLines(Path.Combine(output, "warnings.txt"), repository.DiscoveryWarnings);
            Assert.IsTrue(repository.models.Any(model => model.provider == MNNModelProvider.ModelScope && !model.repositoryId.StartsWith("MNN/", StringComparison.OrdinalIgnoreCase)));
            Assert.IsTrue(repository.models.Any(model => model.provider == MNNModelProvider.HuggingFace && !model.repositoryId.StartsWith("taobao-mnn/", StringComparison.OrdinalIgnoreCase)));
            Assert.IsTrue(repository.models.All(model => model.isRepository && !string.IsNullOrEmpty(model.repositoryId)));
            Assert.AreEqual(repository.models.Count, repository.models.Select(model => model.id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Debug.Log("MNN catalog metadata audit: " + repository.models.Count + " repositories; warnings=" + repository.DiscoveryWarnings.Count + ". No native inference performed.");
        }

        private static string Hf(string id, params string[] files) => "{\"id\":\"" + id + "\",\"sha\":\"pinned\",\"siblings\":[" + string.Join(",", files.Select(file => "{\"rfilename\":\"" + file + "\"}")) + "]}";
        private static HttpResponseMessage MsPage(params string[] ids) => Json("{\"success\":true,\"data\":{\"total_count\":" + ids.Length + ",\"models\":[" + string.Join(",", ids.Select(id => "{\"id\":\"" + id + "\"}")) + "]}}");
        private static MNNHuggingFaceMirrorPool Pool() => new MNNHuggingFaceMirrorPool(new[] {new MNNHuggingFaceMirror("Test", "https://api.unit.test")});
        private static HttpResponseMessage Json(string json) => new HttpResponseMessage(HttpStatusCode.OK) {Content = new StringContent(json, Encoding.UTF8, "application/json")};
        private static HttpClient Client(Func<HttpRequestMessage, HttpResponseMessage> response) => new HttpClient(new Handler(response));

        private sealed class Handler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _response;
            internal Handler(Func<HttpRequestMessage, HttpResponseMessage> response) { _response = response; }
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => Task.FromResult(_response(request));
        }

        private static IEnumerator Wait(Task task, int seconds = 30)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(seconds);
            while (!task.IsCompleted && DateTime.UtcNow < deadline)
                yield return null;
            Assert.IsTrue(task.IsCompleted, "Discovery fixture timed out.");
        }
    }
}
