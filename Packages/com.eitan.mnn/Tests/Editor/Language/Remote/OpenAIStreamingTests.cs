using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class OpenAIStreamingTests
    {
        [TestCase("\n")]
        [TestCase("\r\n")]
        [TestCase("\r")]
        public void FragmentedUtf8SseDeliversTextBeforeEof(string newline)
        {
            Run(async () =>
            {
                string eventText = "data: {\"choices\":[{\"index\":0,\"delta\":{\"content\":\"你好🎙\"}}]}" + newline + newline +
                    "data: {\"choices\":[{\"index\":0,\"finish_reason\":\"stop\"}]}" + newline + newline + "data: [DONE]" + newline + newline;
                using (var handler = new OpenAITransportFixture(Encoding.UTF8.GetBytes(eventText), "text/event-stream"))
                using (var http = new HttpClient(handler))
                using (var client = Client(http))
                {
                    string text = "";
                    await client.GenerateTextAsync("model", new[] { new MNNOpenAIMessage(MNNChatRole.User, "Hello") }, delta =>
                    {
                        Assert.False(handler.Stream.ReachedEnd, "A delta must be delivered before the response ends.");
                        text += delta;
                        return Task.CompletedTask;
                    }, CancellationToken.None).ConfigureAwait(false);
                    Assert.AreEqual("你好🎙", text);
                    Assert.AreEqual("/v1/chat/completions", handler.Path);
                    Assert.AreEqual("Bearer test-only-key", handler.Authorization);
                }
            });
        }
        [Test]
        public void AudioHistoryIsSerializedAsBoundedWavInputAudio()
        {
            Run(async () =>
            {
                const string events = "data: {\"choices\":[{\"index\":0,\"finish_reason\":\"stop\"}]}\n\ndata: [DONE]\n\n";
                var audio = new[] { -0.5f, 0f, 0.5f };
                string wav = Convert.ToBase64String(MNNWaveAudio.EncodePcm16(audio, 16000));
                using (var handler = new OpenAITransportFixture(Encoding.UTF8.GetBytes(events), "text/event-stream"))
                using (var http = new HttpClient(handler))
                using (var client = Client(http))
                {
                    await client.GenerateTextAsync("model", new[] { new MNNOpenAIMessage(audio, 16000) },
                        _ => Task.CompletedTask, CancellationToken.None).ConfigureAwait(false);

                    StringAssert.Contains("\"type\":\"input_audio\"", handler.Body);
                    StringAssert.Contains("\"format\":\"wav\"", handler.Body);
                    StringAssert.Contains(wav, handler.Body);
                }
            });
        }
        [Test]
        public void LongerRepliesRemainUsableInAnExpandedSubsequentHistory()
        {
            Run(async () =>
            {
                const string events = "data: {\"choices\":[{\"index\":0,\"finish_reason\":\"stop\"}]}\n\ndata: [DONE]\n\n";
                var history = new MNNOpenAIMessage[514];
                history[0] = new MNNOpenAIMessage(MNNChatRole.System, "Conversation instructions.");
                string longReply = new string('x', 65536);
                for (int i = 1; i < history.Length; ++i)
                    history[i] = new MNNOpenAIMessage(i % 2 == 0 ? MNNChatRole.Assistant : MNNChatRole.User, i == 2 ? longReply : "Turn " + i);
                using (var handler = new OpenAITransportFixture(Encoding.UTF8.GetBytes(events), "text/event-stream"))
                using (var http = new HttpClient(handler))
                using (var client = Client(http))
                {
                    await client.GenerateTextAsync("model", history, _ => Task.CompletedTask, CancellationToken.None,
                        maximumCharacters: 65536, maximumTokens: 8192).ConfigureAwait(false);
                    StringAssert.Contains(longReply, handler.Body);
                    StringAssert.Contains("Turn 513", handler.Body);
                    StringAssert.Contains("\"max_tokens\":8192", handler.Body);
                    history[2] = new MNNOpenAIMessage(MNNChatRole.Assistant, longReply + "x");
                    Assert.Throws<ArgumentException>(() => client.GenerateTextAsync("model", history,
                        _ => Task.CompletedTask, CancellationToken.None).GetAwaiter().GetResult());
                    Assert.Throws<ArgumentException>(() => client.GenerateTextAsync("model", new MNNOpenAIMessage[515],
                        _ => Task.CompletedTask, CancellationToken.None).GetAwaiter().GetResult());
                }
            });
        }

        [Test]
        public void PcmOddByteFragmentsAreDecodedAndPublishedBeforeEof()
        {
            Run(async () =>
            {
                using (var handler = new OpenAITransportFixture(new byte[] { 0, 128, 0, 0, 255, 127 }, "application/octet-stream"))
                using (var http = new HttpClient(handler))
                using (var client = Client(http))
                {
                    var samples = new System.Collections.Generic.List<float>();
                    await client.SynthesizeAsync("tts", "alloy", "Hello.", (pcm, count) =>
                    {
                        Assert.False(handler.Stream.ReachedEnd);
                        for (int i = 0; i < count; ++i) samples.Add(pcm[i]);
                        return Task.CompletedTask;
                    }, CancellationToken.None).ConfigureAwait(false);
                    CollectionAssert.AreEqual(new[] { -1f, 0, 32767f / 32768 }, samples);
                    StringAssert.Contains("\"response_format\":\"pcm\"", handler.Body);
                }
            });
        }
        [TestCase("data: {}\n\n", typeof(EndOfStreamException))]
        [TestCase("data: [DONE]", typeof(EndOfStreamException))]
        [TestCase("data: {\"choices\":[{\"delta\":{\"content\":\"long\"}}]}\n\n", typeof(InvalidDataException))]
        public void IncompleteAndOverBudgetTextFailExplicitly(string input, Type failure)
        {
            Run(async () =>
            {
                using (var handler = new OpenAITransportFixture(Encoding.UTF8.GetBytes(input), "text/event-stream"))
                using (var http = new HttpClient(handler))
                using (var client = Client(http))
                {
                    Exception observed = null;
                    try { await client.GenerateTextAsync("m", Array.Empty<MNNOpenAIMessage>(), _ => Task.CompletedTask, CancellationToken.None, maximumCharacters: 2); }
                    catch (Exception error) { observed = error; }
                    Assert.IsInstanceOf(failure, observed);
                }
            });
        }
        [Test]
        public void CancellationReleasesPendingResponseRead()
        {
            Run(async () =>
            {
                using (var handler = new OpenAITransportFixture(new byte[] { 1, 0 }, "application/octet-stream"))
                using (var http = new HttpClient(handler))
                using (var client = Client(http))
                using (var cancellation = new CancellationTokenSource())
                {
                    handler.Stream.HoldAtEnd = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    var task = client.SynthesizeAsync("m", "v", "Hello", (_, __) => { cancellation.Cancel(); return Task.CompletedTask; }, cancellation.Token);
                    try { await task; Assert.Fail("Cancellation expected."); }
                    catch (OperationCanceledException) { }
                }
            });
        }
        [TestCase(401)]
        [TestCase(302)]
        public void HttpFailuresExposeStatusWithoutResponseBody(int status)
        {
            Run(async () =>
            {
                using (var handler = new OpenAITransportFixture(Encoding.UTF8.GetBytes("secret-provider-body"), "text/plain") { Status = (HttpStatusCode)status })
                using (var http = new HttpClient(handler))
                using (var client = Client(http))
                {
                    try { await client.SynthesizeAsync("m", "v", "Hello", (_, __) => Task.CompletedTask, CancellationToken.None); Assert.Fail(); }
                    catch (MNNOpenAIHttpException error)
                    {
                        Assert.AreEqual(status, (int)error.Status);
                        StringAssert.DoesNotContain("secret-provider-body", error.ToString());
                        StringAssert.DoesNotContain("test-only-key", error.ToString());
                    }
                }
            });
        }
        [TestCase(307)]
        [TestCase(308)]
        public void DefaultTransportDoesNotResendSpeechToRedirectTargets(int status)
        {
            Run(async () =>
            {
                using (var server = new OpenAIRedirectFixture(status))
                using (var client = new MNNOpenAIClient(server.Endpoint, "test-only-key", TimeSpan.FromSeconds(3)))
                {
                    try
                    {
                        await client.SynthesizeAsync("model", "voice", "Private speech.", (_, __) => Task.CompletedTask,
                            CancellationToken.None).ConfigureAwait(false);
                        Assert.Fail("The initial redirect must be returned as a status failure.");
                    }
                    catch (MNNOpenAIHttpException error) { Assert.AreEqual(status, (int)error.Status); }
                    Assert.AreEqual(1, server.Requests, "The request body must not be resent to the redirect target.");
                    StringAssert.Contains("Authorization: Bearer test-only-key", server.InitialHeaders);
                    StringAssert.Contains("Private speech.", server.InitialBody);
                }
            });
        }
        [Test]
        public void SseLineBudgetIsFinite()
        {
            Run(async () =>
            {
                using (var source = new MemoryStream(Encoding.UTF8.GetBytes("data: " + new string('x', 65536) + "\n\n")))
                {
                    try { await MNNOpenAIClient.ReadEventsAsync(source, _ => Task.FromResult(true), CancellationToken.None); Assert.Fail(); }
                    catch (InvalidDataException) { }
                }
            });
        }
        private static MNNOpenAIClient Client(HttpClient http) => new MNNOpenAIClient(new Uri("https://example.invalid/v1/"), "test-only-key", TimeSpan.FromSeconds(3), http);
        private static void Run(Func<Task> test)
        {
            var task = Task.Run(test);
            Assert.AreSame(task, Task.WhenAny(task, Task.Delay(5000)).GetAwaiter().GetResult(), "Transport test timed out.");
            task.GetAwaiter().GetResult();
        }
    }
}
