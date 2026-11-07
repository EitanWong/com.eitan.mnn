using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MNN.Unity
{
    public sealed partial class MNNCloudSpeechProvider
    {
        private const int MaximumBytes = 16 * 1024 * 1024;
        private async Task<string> PostJsonAsync(string path, object body, CancellationToken token) =>
            Encoding.UTF8.GetString(await PostBytesAsync(path, body, token).ConfigureAwait(false));
        private async Task<byte[]> PostBytesAsync(string path, object body, CancellationToken token)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Post, _settings.Resolve(path)))
            {
                request.Content = new StringContent(JsonUtility.ToJson(body), Encoding.UTF8, "application/json");
                bool volc = _settings.Protocol == MNNCloudSpeechProtocol.VolcengineRecognition || _settings.Protocol == MNNCloudSpeechProtocol.VolcengineSpeech;
                if (volc)
                {
                    if (string.IsNullOrWhiteSpace(_settings.ApplicationId)) request.Headers.Add("X-Api-Key", _key);
                    else
                    {
                        request.Headers.Add("X-Api-App-Key", _settings.ApplicationId);
                        request.Headers.Add("X-Api-Access-Key", _key);
                    }
                    request.Headers.Add("X-Api-Resource-Id", _settings.ResourceId);
                    request.Headers.Add("X-Api-Request-Id", Guid.NewGuid().ToString());
                }
                else if (!string.IsNullOrEmpty(_key)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _key);
                return await SendAsync(request, token, _settings.Protocol == MNNCloudSpeechProtocol.VolcengineRecognition).ConfigureAwait(false);
            }
        }
        private async Task<byte[]> SendAsync(HttpRequestMessage request, CancellationToken token, bool checkVolcStatus = false)
        {
            token.ThrowIfCancellationRequested();
            using (var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false))
            {
                token.ThrowIfCancellationRequested();
                if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Cloud speech HTTP " + (int)response.StatusCode + ". Check the service, key and model access.");
                if (checkVolcStatus && (!response.Headers.TryGetValues("X-Api-Status-Code", out var codes) || codes.FirstOrDefault() != "20000000"))
                    throw InvalidResponse();
                if (response.Content.Headers.ContentLength > MaximumBytes) throw InvalidResponse();
                using (var source = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var cancel = token.Register(source.Dispose))
                using (var buffer = new MemoryStream())
                {
                    var chunk = new byte[8192];
                    try
                    {
                        while (true)
                        {
                            token.ThrowIfCancellationRequested();
                            int read = await source.ReadAsync(chunk, 0, chunk.Length, token).ConfigureAwait(false);
                            token.ThrowIfCancellationRequested();
                            if (read == 0) break;
                            if (buffer.Length + read > MaximumBytes) throw InvalidResponse();
                            buffer.Write(chunk, 0, read);
                        }
                    }
                    catch (Exception) when (token.IsCancellationRequested) { throw new OperationCanceledException(token); }
                    return buffer.ToArray();
                }
            }
        }
        private Task<byte[]> DownloadAudioAsync(string address, CancellationToken token)
        {
            if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) || !uri.Host.EndsWith(".aliyuncs.com", StringComparison.OrdinalIgnoreCase) ||
                !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Fragment) || !uri.IsDefaultPort ||
                (uri.Scheme != "https" && uri.Scheme != "http")) throw InvalidResponse();
            // Some official Qwen responses contain an HTTP OSS URL. Upgrade the same signed URL to HTTPS.
            if (uri.Scheme == "http") uri = new UriBuilder(uri) { Scheme = "https", Port = -1 }.Uri;
            return DownloadAsync(uri, token);
        }
        private async Task<byte[]> DownloadAsync(Uri uri, CancellationToken token)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, uri))
                return await SendAsync(request, token).ConfigureAwait(false); // No credentials on the signed object URL.
        }
    }
}
