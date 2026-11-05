using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    // Metadata requests share a hub cooldown. Weight transfers keep their own range/retry policy.
    internal sealed class MNNModelCatalogRequests
    {
        internal static readonly MNNModelCatalogRequests Shared = new MNNModelCatalogRequests();
        private static readonly TimeSpan MaximumWait = TimeSpan.FromSeconds(30);
        private readonly ConcurrentDictionary<string, Origin> _origins = new ConcurrentDictionary<string, Origin>();
        private readonly Func<DateTimeOffset> _now;
        private readonly Func<TimeSpan, CancellationToken, Task> _delay;

        private sealed class Origin
        {
            internal readonly object Gate = new object();
            internal DateTimeOffset BlockedUntil, NextRequest, LastPenalty;
            internal TimeSpan Spacing;
            internal int Successes;
        }

        internal MNNModelCatalogRequests(Func<DateTimeOffset> now = null, Func<TimeSpan, CancellationToken, Task> delay = null)
        {
            _now = now ?? (() => DateTimeOffset.UtcNow);
            _delay = delay ?? ((duration, token) => Task.Delay(duration, token));
        }

        internal async Task<HttpResponseMessage> GetAsync(HttpClient client, string url, CancellationToken cancellationToken)
        {
            string authority = new Uri(url).GetComponents(UriComponents.SchemeAndServer, UriFormat.UriEscaped);
            var origin = _origins.GetOrAdd(authority, _ => new Origin());
            for (int attempt = 0; ; ++attempt)
            {
                await WaitForSlotAsync(origin, authority, cancellationToken).ConfigureAwait(false);
                var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                if ((int)response.StatusCode != 429)
                {
                    if (response.IsSuccessStatusCode)
                        lock (origin.Gate)
                            if (++origin.Successes >= 32)
                            {
                                origin.Successes = 0;
                                origin.Spacing = origin.Spacing < TimeSpan.FromMilliseconds(50) ? TimeSpan.Zero
                                    : TimeSpan.FromTicks(origin.Spacing.Ticks / 2);
                            }
                    return response;
                }

                var now = _now();
                var retry = response.Headers.RetryAfter;
                var pause = retry?.Delta ?? (retry?.Date.HasValue == true ? retry.Date.Value - now : TimeSpan.FromSeconds(1 << attempt));
                if (pause < TimeSpan.Zero) pause = TimeSpan.Zero;
                lock (origin.Gate)
                {
                    origin.Successes = 0;
                    // Concurrent responses from one burst count as one rate-limit event.
                    if (now - origin.LastPenalty >= TimeSpan.FromSeconds(1))
                    {
                        origin.Spacing = TimeSpan.FromMilliseconds(Math.Min(2000, Math.Max(100, origin.Spacing.TotalMilliseconds * 2)));
                        origin.LastPenalty = now;
                    }
                    var until = pause > DateTimeOffset.MaxValue - now ? DateTimeOffset.MaxValue : now + pause;
                    if (until > origin.BlockedUntil) origin.BlockedUntil = until;
                }
                response.Dispose();
                // Do not hold a window open for a server's minute/hour-long cooldown or retry earlier than requested.
                if (attempt == 3 || pause > MaximumWait)
                    throw RateLimited(authority, pause);
            }
        }

        private async Task WaitForSlotAsync(Origin origin, string authority, CancellationToken cancellationToken)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                TimeSpan wait;
                lock (origin.Gate)
                {
                    var now = _now();
                    var next = origin.BlockedUntil > origin.NextRequest ? origin.BlockedUntil : origin.NextRequest;
                    wait = next - now;
                    if (wait <= TimeSpan.Zero)
                    {
                        origin.NextRequest = now + origin.Spacing;
                        return;
                    }
                }
                if (wait > MaximumWait) throw RateLimited(authority, wait);
                await _delay(wait, cancellationToken).ConfigureAwait(false);
                // Recheck: another in-flight response may have extended the shared cooldown.
            }
        }

        private static HttpRequestException RateLimited(string authority, TimeSpan pause) => new HttpRequestException(
            authority + " is rate limited (HTTP 429). Retry after " + Math.Ceiling(pause.TotalSeconds) + " seconds.");
    }
}
