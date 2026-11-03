using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class AudioModelDiscoveryTests
    {
        private sealed class Hub : HttpMessageHandler
        {
            private static readonly string[] Repositories = { "EitanWang/Silero-v5.1-MNN", "EitanWang/VoxTurn-v1-MNN",
                "EitanWang/CueLight-Nano-MNN", "EitanWang/SpExPlus-WSJ02Mix-8K-MNN", "publisher/denoiser-MNN",
                "publisher/diarizer-MNN", "publisher/classifier-MNN" };
            private static readonly string[] Tasks = { "voice-activity-detection", "audio-classification", "voice-activity-detection",
                "speech-separation", "acoustic-noise-suppression", "speaker-diarization", "audio-classification" };
            internal int WeightRequests;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                string body;
                if (request.RequestUri.AbsolutePath == "/openapi/v1/models")
                    body = "{\"success\":true,\"data\":{\"total_count\":7,\"models\":[" + string.Join(",", Repositories.Select((id, index) =>
                        "{\"id\":\"" + id + "\",\"tasks\":[\"" + Tasks[index] + "\"]}")) + "]}}";
                else if (request.RequestUri.AbsolutePath == "/api/models")
                    body = "[" + string.Join(",", Repositories.Select((id, index) => "{\"id\":\"" + id +
                        "\",\"sha\":\"revision\",\"tags\":[\"mnn\"],\"pipeline_tag\":\"" + Tasks[index] +
                        "\",\"siblings\":[{\"rfilename\":\"model.mnn\"}]}")) + "]";
                else if (request.RequestUri.AbsolutePath.EndsWith("/repo/files", StringComparison.Ordinal))
                    body = "{\"Code\":200,\"Data\":{\"Files\":[{\"Path\":\"silero_v5_16k_fp32.mnn\",\"Type\":\"blob\",\"Size\":4}," +
                        "{\"Path\":\"mnn_silero_vad.json\",\"Type\":\"blob\",\"Size\":4}]}}";
                else { ++WeightRequests; throw new InvalidOperationException("Classification must only fetch metadata."); }
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
            }
        }

        [TestCase(false), TestCase(true)]
        public void HubSearchPagesPublishSpecificAudioCategoriesWithoutDownloadingWeights(bool huggingFace)
        {
            var handler = new Hub();
            using (var client = new HttpClient(handler))
            {
                var catalog = new MNNModelCatalog();
                var models = huggingFace
                    ? catalog.FetchHuggingFaceSearchPageAsync(0, null, "audio", client,
                        new MNNHuggingFaceSourcePool(new[] { new MNNHuggingFaceSource("Fixture", "https://audio-hub.test") }))
                        .GetAwaiter().GetResult().Models
                    : catalog.FetchModelScopeSearchPageAsync(0, 1, "audio", client, "https://audio-hub.test")
                        .GetAwaiter().GetResult().Models;
                CollectionAssert.AreEqual(new[] { "VoiceActivityDetection", "TurnDetection", "TurnDetection", "TargetSpeakerExtraction",
                    "SpeechEnhancement", "SpeakerDiarization", "AudioClassification" }, models.Select(model => model.category));
                var editor = new MNNModelRepository { models = models.Select(MNNModelRepository.FromRemote).ToList() };
                Assert.AreEqual(1, editor.GetModelsByCategory(MNNModelCategory.VoiceActivityDetection).Count);
                Assert.AreEqual(2, editor.GetModelsByCategory(MNNModelCategory.TurnDetection).Count);
                Assert.AreEqual(1, editor.SearchModels("目标说话人").Count);
                Assert.Zero(handler.WeightRequests);
            }
        }
    }
}
