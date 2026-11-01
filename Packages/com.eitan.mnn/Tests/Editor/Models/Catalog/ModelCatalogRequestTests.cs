using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MNN.Unity.Tests
{
    public class ModelCatalogRequestTests
    {
        private static readonly DateTimeOffset Start = new DateTimeOffset(2026, 10, 18, 0, 0, 0, TimeSpan.Zero);

        [UnityTest]
        public IEnumerator RetryAfterDeltaAndDateDelayTheSameMetadataRequest()
        {
            foreach (bool date in new[] {false, true})
            {
                var now = Start;
                var waits = new List<TimeSpan>();
                int calls = 0;
                var policy = new MNNModelCatalogRequests(() => now, (duration, token) =>
                {
                    waits.Add(duration); now += duration; return Task.CompletedTask;
                });
                using (var client = new HttpClient(new Handler(request =>
                {
                    Assert.AreEqual("https://catalog.test/models?page=2", request.RequestUri.AbsoluteUri);
                    if (++calls != 1) return Ok();
                    var response = Limited();
                    response.Headers.RetryAfter = date ? new RetryConditionHeaderValue(Start.AddSeconds(3))
                        : new RetryConditionHeaderValue(TimeSpan.FromSeconds(3));
                    return response;
                })))
                {
                    var task = policy.GetAsync(client, "https://catalog.test/models?page=2", CancellationToken.None);
                    yield return Until(() => task.IsCompleted);
                    Assert.False(task.IsFaulted, task.Exception?.ToString());
                    using (var response = task.Result) Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                    Assert.AreEqual(2, calls);
                    Assert.That(waits[0], Is.EqualTo(TimeSpan.FromSeconds(3)));
                    Assert.That(now, Is.GreaterThanOrEqualTo(Start.AddSeconds(3)));
                }
            }
        }

        [UnityTest]
        public IEnumerator RateLimitCooldownIsSharedByNewReadersAtTheSameHub()
        {
            var now = Start;
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            int calls = 0, waiting = 0;
            var gate = new object();
            var policy = new MNNModelCatalogRequests(() => { lock (gate) return now; }, async (duration, token) =>
            {
                DateTimeOffset target;
                lock (gate) target = now + duration;
                Interlocked.Increment(ref waiting);
                await release.Task;
                lock (gate) if (target > now) now = target;
            });
            using (var client = new HttpClient(new Handler(request =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                {
                    var response = Limited(); response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(3));
                    return response;
                }
                lock (gate) Assert.That(now, Is.GreaterThanOrEqualTo(Start.AddSeconds(3)));
                return Ok();
            })))
            {
                var first = policy.GetAsync(client, "https://shared.test/search", CancellationToken.None);
                var second = policy.GetAsync(client, "https://shared.test/manifest", CancellationToken.None);
                yield return Until(() => Volatile.Read(ref waiting) >= 2);
                Assert.AreEqual(1, calls, "A new reader must honor another request's cooldown.");
                release.SetResult(true);
                var both = Task.WhenAll(first, second);
                yield return Until(() => both.IsCompleted);
                Assert.False(both.IsFaulted, both.Exception?.ToString());
                foreach (var response in both.Result) response.Dispose();
                Assert.AreEqual(3, calls);
            }
        }

        [UnityTest]
        public IEnumerator LongCooldownAndRepeatedLimitsHaveBoundedRequestsAndKeepOtherHubsIndependent()
        {
            var now = Start;
            int calls = 0;
            var policy = new MNNModelCatalogRequests(() => now, (duration, token) => { now += duration; return Task.CompletedTask; });
            using (var client = new HttpClient(new Handler(request =>
            {
                ++calls;
                if (request.RequestUri.Host == "available.test") return Ok();
                var response = Limited(); response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMinutes(1)); return response;
            })))
            {
                var limited = policy.GetAsync(client, "https://limited.test/search", CancellationToken.None);
                yield return Until(() => limited.IsCompleted);
                Assert.True(limited.IsFaulted);
                Assert.That(limited.Exception.ToString(), Does.Contain("HTTP 429"));
                var retry = policy.GetAsync(client, "https://limited.test/search", CancellationToken.None);
                yield return Until(() => retry.IsCompleted);
                Assert.True(retry.IsFaulted);
                Assert.AreEqual(1, calls, "A long Retry-After must not trigger early polling.");
                var available = policy.GetAsync(client, "https://available.test/search", CancellationToken.None);
                yield return Until(() => available.IsCompleted);
                Assert.False(available.IsFaulted); available.Result.Dispose();
            }
            calls = 0;
            var burstWaits = new List<TimeSpan>();
            policy = new MNNModelCatalogRequests(() => now, (duration, token) => { burstWaits.Add(duration); now += duration; return Task.CompletedTask; });
            using (var client = new HttpClient(new Handler(request =>
            {
                ++calls; var response = Limited(); response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero); return response;
            })))
            {
                var exhausted = policy.GetAsync(client, "https://exhausted.test/search", CancellationToken.None);
                yield return Until(() => exhausted.IsCompleted);
                Assert.True(exhausted.IsFaulted);
                Assert.AreEqual(4, calls);
                Assert.IsNotEmpty(burstWaits);
                Assert.True(burstWaits.TrueForAll(duration => duration == burstWaits[0]), "One response burst must not multiply the throttle repeatedly.");
            }
        }

        [UnityTest]
        public IEnumerator CancellationDuringBackoffDoesNotIssueAnotherRequest()
        {
            int calls = 0;
            bool waiting = false;
            var policy = new MNNModelCatalogRequests(() => Start, (duration, token) =>
            {
                waiting = true; return Task.Delay(Timeout.Infinite, token);
            });
            using (var cancellation = new CancellationTokenSource())
            using (var client = new HttpClient(new Handler(request => { ++calls; return Limited(); })))
            {
                var task = policy.GetAsync(client, "https://cancel.test/search", cancellation.Token);
                yield return Until(() => waiting);
                cancellation.Cancel();
                yield return Until(() => task.IsCompleted);
                Assert.True(task.IsCanceled);
                Assert.AreEqual(1, calls);
            }
        }

        private static HttpResponseMessage Ok() => new HttpResponseMessage(HttpStatusCode.OK) {Content = new StringContent("[]")};
        private static HttpResponseMessage Limited() => new HttpResponseMessage((HttpStatusCode)429);
        private static IEnumerator Until(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (!condition() && DateTime.UtcNow < deadline) yield return null;
            Assert.True(condition(), "Metadata request did not settle.");
        }
        private sealed class Handler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _send;
            internal Handler(Func<HttpRequestMessage, HttpResponseMessage> send) => _send = send;
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => Task.FromResult(_send(request));
        }
    }
}
