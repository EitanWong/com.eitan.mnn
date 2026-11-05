using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MNN.Unity
{
    public sealed class MNNRouteLlmClient
    {
        private readonly MNNBoundedHttpClient _http;
        private readonly Uri _endpoint;
        private readonly float _threshold;
        public MNNRouteLlmClient(MNNBoundedHttpClient http, Uri endpoint, float threshold = .5f)
        {
            if (http == null) throw new ArgumentNullException(nameof(http));
            if (endpoint == null || !endpoint.IsAbsoluteUri || !endpoint.IsLoopback || !(endpoint.Scheme == "http" || endpoint.Scheme == "https") ||
                endpoint.UserInfo.Length != 0 || endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0)
                throw new ArgumentException("RouteLLM requires HTTP(S) loopback without credentials or query.", nameof(endpoint));
            if (float.IsNaN(threshold) || threshold < 0 || threshold > 1) throw new ArgumentOutOfRangeException(nameof(threshold));
            _http = http; _endpoint = endpoint; _threshold = threshold;
        }
        public async Task<bool> SelectCloudAsync(string prompt, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(prompt) || prompt.Length > 65536) throw new ArgumentException("Provide a bounded routing request.");
            string json = JsonUtility.ToJson(new Request { prompt = prompt, threshold = _threshold });
            var result = JsonUtility.FromJson<Reply>(await _http.SendAsync(_endpoint, json, token).ConfigureAwait(false));
            if (result == null || !(result.route == "local" || result.route == "cloud")) throw new ArgumentException("Invalid RouteLLM selection.");
            return result.route == "cloud";
        }
        [Serializable] private sealed class Request { public string prompt; public float threshold; }
        [Serializable] private sealed class Reply { public string route; }
    }
}
