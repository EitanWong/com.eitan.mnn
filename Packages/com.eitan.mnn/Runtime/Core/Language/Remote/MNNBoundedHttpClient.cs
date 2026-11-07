using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    public sealed class MNNBoundedHttpClient : IDisposable
    {
        private readonly HttpClient _client;
        public MNNBoundedHttpClient(HttpMessageHandler handler = null)
        {
            _client = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = Timeout.InfiniteTimeSpan };
        }
        public Task<string> GetAsync(Uri uri, CancellationToken token) => SendAsync(uri, null, token);
        public async Task<string> SendAsync(Uri uri, string body, CancellationToken token)
        {
            if (uri == null || !uri.IsAbsoluteUri || !(uri.Scheme == "https" || uri.Scheme == "http" && uri.IsLoopback) || uri.UserInfo.Length != 0)
                throw new ArgumentException("Use HTTPS or HTTP loopback without URL credentials.", nameof(uri));
            if (body != null && body.Length > 65536) throw new ArgumentException("Request body exceeds its budget.", nameof(body));
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(token))
            using (var request = new HttpRequestMessage(body == null ? HttpMethod.Get : HttpMethod.Post, uri))
            {
                deadline.CancelAfter(TimeSpan.FromSeconds(10));
                if (body != null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");
                using (var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    if (response.Content.Headers.ContentLength > 65536) throw new InvalidDataException("Tool response exceeds its budget.");
                    using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (deadline.Token.Register(() => stream.Dispose()))
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        var text = new StringBuilder();
                        var buffer = new char[2048];
                        int count;
                        try
                        {
                            while ((count = await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
                            {
                                deadline.Token.ThrowIfCancellationRequested();
                                if (text.Length + count > 32768) throw new InvalidDataException("Tool response exceeds its budget.");
                                text.Append(buffer, 0, count);
                            }
                        }
                        catch (Exception) when (deadline.IsCancellationRequested) { throw new OperationCanceledException(deadline.Token); }
                        deadline.Token.ThrowIfCancellationRequested();
                        return text.ToString();
                    }
                }
            }
        }
        public void Dispose() => _client.Dispose();
    }
}
