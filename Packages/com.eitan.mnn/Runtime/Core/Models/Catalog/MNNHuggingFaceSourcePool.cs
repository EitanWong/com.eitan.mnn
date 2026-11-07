using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    /// <summary>Known and discovered Hugging Face-compatible sources with failure cooling.</summary>
    public sealed class MNNHuggingFaceSourcePool
    {
        public static readonly string[] DefaultDiscoveryUrls = { "https://hf-mirror.com/", "https://hf-mirror.net/", "https://aifasthub.com/", "https://hf-cdn.sufy.com/" };
        private sealed class State
        {
            public readonly MNNHuggingFaceSource Mirror;
            public int Failures;
            public DateTime CooldownUntil;
            public long Successes;
            public State(MNNHuggingFaceSource mirror)
            {
                Mirror = mirror;
            }
        }

        private readonly object _sync = new object();
        private readonly List<State> _states = new List<State>();
        private readonly TimeSpan _failureCooldown;
        public MNNHuggingFaceSourcePool(IEnumerable<MNNHuggingFaceSource> mirrors = null, TimeSpan? failureCooldown = null)
        {
            _failureCooldown = failureCooldown ?? TimeSpan.FromMinutes(2);
            foreach (var mirror in mirrors ?? CreateDefaultMirrors())
                Add(mirror);
        }

        public static MNNHuggingFaceSourcePool Shared { get; } = new MNNHuggingFaceSourcePool();
        public IReadOnlyList<MNNHuggingFaceSource> GetApiCandidates()
        {
            return GetCandidates(true);
        }

        public IReadOnlyList<MNNHuggingFaceSource> GetFileCandidates()
        {
            return GetCandidates(false);
        }

        public IReadOnlyList<MNNHuggingFaceSource> GetApiProbeCandidates()
        {
            lock (_sync)
                return _states.Where(state => state.Mirror.SupportsApi && state.CooldownUntil <= DateTime.UtcNow).OrderByDescending(state => state.Successes).ThenBy(state => state.Failures).Select(state => state.Mirror).ToArray();
        }

        public void Add(MNNHuggingFaceSource mirror)
        {
            if (mirror == null)
                throw new ArgumentNullException(nameof(mirror));
            lock (_sync)
            {
                if (_states.Any(state => string.Equals(state.Mirror.FileRoot, mirror.FileRoot, StringComparison.OrdinalIgnoreCase)))
                    return;
                _states.Add(new State(mirror));
            }
        }

        public void ReportSuccess(MNNHuggingFaceSource mirror)
        {
            if (mirror == null)
                return;
            lock (_sync)
            {
                var state = Find(mirror);
                if (state == null)
                    return;
                state.Successes++;
                state.Failures = 0;
                state.CooldownUntil = DateTime.MinValue;
            }
        }

        public void ReportFailure(MNNHuggingFaceSource mirror)
        {
            if (mirror == null)
                return;
            lock (_sync)
            {
                var state = Find(mirror);
                if (state == null)
                    return;
                state.Failures++;
                state.CooldownUntil = DateTime.UtcNow + TimeSpan.FromTicks(_failureCooldown.Ticks * Math.Min(state.Failures, 4));
            }
        }

        /// <summary>
        /// Reads HTTPS origins from operator-supplied pages or plain-text indexes. Only origins are accepted;
        /// paths and credentials are discarded before a source is added to the pool.
        /// </summary>
        public async Task<int> RefreshFromWebAsync(HttpClient client, IEnumerable<string> indexUrls, CancellationToken cancellationToken = default)
        {
            if (client == null)
                throw new ArgumentNullException(nameof(client));
            var urls = (indexUrls ?? DefaultDiscoveryUrls).ToArray();
            var discoveryTasks = urls.Select(async indexUrl =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                    {
                        timeout.CancelAfter(TimeSpan.FromSeconds(8));
                        using (var response = await client.GetAsync(indexUrl, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false))
                        {
                            if (!response.IsSuccessStatusCode)
                                return 0;
                            var body = await MNNModelMetadata.ReadTextAsync(response.Content, timeout.Token, 1024 * 1024).ConfigureAwait(false);
                            var added = 0;
                            foreach (var origin in ExtractHttpsOrigins(body))
                            {
                                var before = GetAllCandidates().Count;
                                Add(new MNNHuggingFaceSource(origin, origin, origin, false));
                                if (GetAllCandidates().Count > before)
                                    added++;
                            }

                            return added;
                        }
                    }
                }
                catch (HttpRequestException)
                {
                    return 0;
                }
                catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    return 0;
                }
            }).ToArray();
            return (await Task.WhenAll(discoveryTasks).ConfigureAwait(false)).Sum();
        }

        private IReadOnlyList<MNNHuggingFaceSource> GetAllCandidates()
        {
            lock (_sync)
                return _states.Select(state => state.Mirror).ToArray();
        }

        private IReadOnlyList<MNNHuggingFaceSource> GetCandidates(bool api)
        {
            lock (_sync)
            {
                var now = DateTime.UtcNow;
                return _states.Where(state => (!api || state.Mirror.SupportsApi) && state.CooldownUntil <= now).OrderByDescending(state => state.Successes).ThenBy(state => state.Failures).Select(state => state.Mirror).ToArray();
            }
        }

        private State Find(MNNHuggingFaceSource mirror)
        {
            return _states.FirstOrDefault(state => string.Equals(state.Mirror.FileRoot, mirror.FileRoot, StringComparison.OrdinalIgnoreCase));
        }

        private static IEnumerable<MNNHuggingFaceSource> CreateDefaultMirrors()
        {
            yield return new MNNHuggingFaceSource("Hugging Face", "https://huggingface.co", "https://huggingface.co", true);
            yield return new MNNHuggingFaceSource("HF-Mirror", "https://hf-mirror.com", "https://hf-mirror.com", true);
            yield return new MNNHuggingFaceSource("HF-Mirror Net", "https://hf-mirror.net", "https://hf-mirror.net", true);
            yield return new MNNHuggingFaceSource("AI Fast Hub", "https://aifasthub.com", "https://aifasthub.com", true);
            // This CDN currently serves resolve paths but may reject /api requests.
            yield return new MNNHuggingFaceSource("HF-CDN Sufy", "https://hf-cdn.sufy.com", "https://hf-cdn.sufy.com", false);
        }

        private static IEnumerable<string> ExtractHttpsOrigins(string body)
        {
            var origins = new List<string>();
            if (string.IsNullOrEmpty(body))
                return origins;
            var matches = Regex.Matches(body, @"https://[A-Za-z0-9.-]+(?:/[^\s""'<>)]*)?", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match match in matches)
            {
                Uri uri;
                if (!Uri.TryCreate(match.Value.TrimEnd('.', ',', ';'), UriKind.Absolute, out uri) || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo))
                    continue;
                var origin = uri.GetLeftPart(UriPartial.Authority);
                var host = uri.Host;
                var likelyMirror = host.IndexOf("huggingface", StringComparison.OrdinalIgnoreCase) >= 0 || host.IndexOf("hf-", StringComparison.OrdinalIgnoreCase) >= 0 || host.StartsWith("hf.", StringComparison.OrdinalIgnoreCase) || host.IndexOf("aifast", StringComparison.OrdinalIgnoreCase) >= 0 || host.IndexOf("sufy", StringComparison.OrdinalIgnoreCase) >= 0;
                if (likelyMirror && seen.Add(origin) && origin.IndexOf("huggingface.co/api", StringComparison.OrdinalIgnoreCase) < 0)
                    origins.Add(origin);
            }

            return origins;
        }
    }
}
