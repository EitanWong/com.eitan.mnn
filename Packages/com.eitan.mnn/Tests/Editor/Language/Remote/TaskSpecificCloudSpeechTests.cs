using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MNN.Unity;
using NUnit.Framework;
namespace MNN.Unity.Tests
{
    public sealed class TaskSpecificCloudSpeechTests
    {
        private sealed class Http : HttpMessageHandler
        {
            internal Func<HttpRequestMessage, Task<HttpResponseMessage>> Reply;
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => Reply(request);
        }
        private static HttpResponseMessage Json(string json) => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        private static MNNCloudSpeechOptions Config(string service, MNNCloudSpeechTask task)
        {
            var protocol = service == "dashscope" ? task == MNNCloudSpeechTask.Recognition ? MNNCloudSpeechProtocol.DashScopeRecognition : MNNCloudSpeechProtocol.DashScopeSpeech :
                service == "volcengine" ? task == MNNCloudSpeechTask.Recognition ? MNNCloudSpeechProtocol.VolcengineRecognition : MNNCloudSpeechProtocol.VolcengineSpeech :
                service == "minimax" ? MNNCloudSpeechProtocol.MiniMaxSpeech : MNNCloudSpeechProtocol.OpenAISpeech;
            string endpoint = service == "dashscope" ? task == MNNCloudSpeechTask.Recognition ? "https://example.com/compatible-mode/v1" : "https://example.com/api/v1" :
                service == "volcengine" ? "https://example.com/api/v3" : "https://example.com/v1";
            return new MNNCloudSpeechOptions { Protocol = protocol, Task = task, Endpoint = endpoint, Model = service == "volcengine" ? "bigmodel" : "fixture-model", Voice = "fixture-voice", ResourceId = "fixture-resource" };
        }
        [Test]
        public void InvalidCredentialIsRejectedWithoutEchoingItsValue()
        {
            var error = Assert.Throws<ArgumentException>(() =>
            {
                using (var provider = new MNNCloudSpeechProvider(Config("minimax", MNNCloudSpeechTask.Synthesis), "fixture-secret\n")) { }
            });
            StringAssert.DoesNotContain("fixture-secret", error.Message);
        }
        [Test]
        public void QwenRecognitionUploadsWavInAudioMessage()
        {
            var http = new Http
            {
                Reply = async request =>
                {
                    Assert.AreEqual("/compatible-mode/v1/chat/completions", request.RequestUri.AbsolutePath);
                    Assert.AreEqual("Bearer test-key", request.Headers.Authorization.ToString());
                    string body = await request.Content.ReadAsStringAsync();
                    StringAssert.Contains("input_audio", body);
                    StringAssert.Contains("data:audio/wav;base64,UklGR", body);
                    return Json("{\"choices\":[{\"message\":{\"content\":\"你好\"}}]}");
                }
            };
            using (var provider = new MNNCloudSpeechProvider(Config("dashscope", MNNCloudSpeechTask.Recognition), "test-key", http))
                Assert.AreEqual("你好", provider.TranscribeAsync(new float[1600], 16000, CancellationToken.None).GetAwaiter().GetResult());
        }
        [Test]
        public void DoubaoRecognitionUsesSpeechKeyAndChecksServiceStatus()
        {
            var http = new Http
            {
                Reply = async request =>
                {
                    Assert.AreEqual("/api/v3/auc/bigmodel/recognize/flash", request.RequestUri.AbsolutePath);
                    Assert.IsTrue(request.Headers.Contains("X-Api-Key"));
                    Assert.IsFalse(request.Headers.Contains("Authorization"));
                    StringAssert.Contains("bigmodel", await request.Content.ReadAsStringAsync());
                    var response = Json("{\"result\":{\"text\":\"recognized\"}}");
                    response.Headers.Add("X-Api-Status-Code", "20000000"); return response;
                }
            };
            using (var provider = new MNNCloudSpeechProvider(Config("volcengine", MNNCloudSpeechTask.Recognition), "test-key", http))
                Assert.AreEqual("recognized", provider.TranscribeAsync(new float[1600], 16000, CancellationToken.None).GetAwaiter().GetResult());
        }
        [TestCase("minimax", "/v1/t2a_v2", "voice_setting", "{\"base_resp\":{\"status_code\":0},\"data\":{\"audio\":\"00000040\"}}")]
        [TestCase("volcengine", "/api/v3/tts/unidirectional", "req_params", "{\"code\":0,\"data\":\"AAAAQA==\"}\n{\"code\":20000000}\n")]
        public void NativeSpeechDecodesPcmAndRequests24K(string service, string path, string field, string response)
        {
            var http = new Http
            {
                Reply = async request =>
                {
                    Assert.AreEqual(path, request.RequestUri.AbsolutePath);
                    string body = await request.Content.ReadAsStringAsync();
                    StringAssert.Contains(field, body); StringAssert.Contains("24000", body);
                    return Json(response.Replace("\\n", "\n"));
                }
            };
            using (var provider = new MNNCloudSpeechProvider(Config(service, MNNCloudSpeechTask.Synthesis), "test-key", http))
            {
                float[] result = null;
                provider.SynthesizeAsync("你好", (pcm, count) => { result = pcm; return Task.CompletedTask; }, CancellationToken.None).GetAwaiter().GetResult();
                Assert.AreEqual(24000, provider.SampleRate);
                Assert.AreEqual(2, result.Length); Assert.AreEqual(.5f, result[1], .0001f);
            }
        }
        [Test]
        public void SiliconFlowSpeechExplicitlySelectsPcmAndRate()
        {
            var http = new Http
            {
                Reply = async request =>
                {
                    StringAssert.EndsWith("/audio/speech", request.RequestUri.AbsolutePath);
                    string body = await request.Content.ReadAsStringAsync();
                    StringAssert.Contains("\"response_format\":\"pcm\"", body); StringAssert.Contains("24000", body);
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[] { 0, 0, 0, 64 }) };
                }
            };
            using (var provider = new MNNCloudSpeechProvider(Config("siliconflow", MNNCloudSpeechTask.Synthesis), "test-key", http))
            {
                int frames = 0;
                provider.SynthesizeAsync("你好", (pcm, count) => { frames += count; return Task.CompletedTask; }, CancellationToken.None).GetAwaiter().GetResult();
                Assert.AreEqual(2, frames);
            }
        }
        [Test]
        public void QwenDownloadsWaveWithoutForwardingKey()
        {
            int calls = 0;
            var http = new Http
            {
                Reply = request =>
                {
                    ++calls;
                    if (calls == 1) return Task.FromResult(Json("{\"output\":{\"audio\":{\"url\":\"https://test.oss-cn-beijing.aliyuncs.com/test.wav\"}}}"));
                    Assert.IsNull(request.Headers.Authorization);
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(MNNWaveAudio.EncodePcm16(new float[2400], 24000)) });
                }
            };
            using (var provider = new MNNCloudSpeechProvider(Config("dashscope", MNNCloudSpeechTask.Synthesis), "test-key", http))
            {
                int frames = 0;
                provider.SynthesizeAsync("你好", (pcm, count) => { frames += count; return Task.CompletedTask; }, CancellationToken.None).GetAwaiter().GetResult();
                Assert.AreEqual(2, calls); Assert.AreEqual(2400, frames);
            }
        }
        [TestCase("minimax", "{\"base_resp\":{},\"data\":{\"audio\":\"0000\"}}")]
        [TestCase("dashscope", "null")]
        [TestCase("minimax", "null")]
        [TestCase("minimax", "{\"base_resp\":{\"status_code\":1004},\"data\":{\"audio\":\"0000\"}}")]
        [TestCase("volcengine", "{\"code\":55000000,\"message\":\"service failure\"}")]
        [TestCase("dashscope", "{\"output\":{\"audio\":{\"url\":\"https://untrusted.example/audio\"}}}")]
        public void ServiceErrorsNeverPublishAudio(string service, string response)
        {
            var http = new Http { Reply = request => Task.FromResult(Json(response)) };
            using (var provider = new MNNCloudSpeechProvider(Config(service, MNNCloudSpeechTask.Synthesis), "test-key", http))
                Assert.Throws<InvalidOperationException>(() => provider.SynthesizeAsync("hello", (pcm, count) => { Assert.Fail("invalid audio published"); return Task.CompletedTask; }, CancellationToken.None).GetAwaiter().GetResult());
        }
        [Test]
        public void CancellationBetweenResponseAndPublicationStopsSpeech()
        {
            using (var cancel = new CancellationTokenSource())
            {
                var http = new Http { Reply = request => { cancel.Cancel(); return Task.FromResult(Json("{\"base_resp\":{\"status_code\":0},\"data\":{\"audio\":\"0000\"}}")); } };
                using (var provider = new MNNCloudSpeechProvider(Config("minimax", MNNCloudSpeechTask.Synthesis), "test-key", http))
                    Assert.Catch<OperationCanceledException>(() => provider.SynthesizeAsync("hello", (pcm, count) => { Assert.Fail("audio after cancel"); return Task.CompletedTask; }, cancel.Token).GetAwaiter().GetResult());
            }
        }
    }
}
