using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity.Tests
{
    internal sealed class OpenAITransportFixture : HttpMessageHandler
    {
        internal readonly FragmentedStream Stream;
        internal readonly string Media;
        internal string Path, Authorization;
        internal string Body;
        internal HttpStatusCode Status = HttpStatusCode.OK;
        internal OpenAITransportFixture(byte[] bytes, string media, int fragment = 1)
        { Stream = new FragmentedStream(bytes, fragment); Media = media; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Path = request.RequestUri.AbsolutePath;
            Authorization = request.Headers.Authorization?.ToString();
            Body = await request.Content.ReadAsStringAsync().ConfigureAwait(false);
            var response = new HttpResponseMessage(Status) { Content = new StreamContent(Stream) };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue(Media);
            return response;
        }
        internal sealed class FragmentedStream : MemoryStream
        {
            private readonly int fragment;
            internal bool ReachedEnd;
            internal TaskCompletionSource<bool> HoldAtEnd;
            internal FragmentedStream(byte[] bytes, int fragment) : base(bytes) { this.fragment = fragment; }
            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                if (Position == Length)
                {
                    ReachedEnd = true;
                    if (HoldAtEnd != null)
                        using (token.Register(() => HoldAtEnd.TrySetCanceled())) await HoldAtEnd.Task.ConfigureAwait(false);
                }
                return await base.ReadAsync(buffer, offset, Math.Min(fragment, count), token).ConfigureAwait(false);
            }
        }
    }
}
