using System.Collections.Concurrent;
using System.Net.Http.Headers;

namespace SiriAI.HarnessHost;

internal sealed partial class A2aHarnessAdapter
{
    private readonly ConcurrentDictionary<string, A2aStreamSession> _streams = new();
    private async Task<RemoteSnapshot> OpenStreamAsync(RemoteCheckpoint state, object parameters, bool subscribe, RemoteSnapshot seed, CancellationToken cancellation)
    {
        string rpcId = state.Binding == "JSONRPC" ? Guid.NewGuid().ToString("N") : null;
        var session = new A2aStreamSession(token => ConnectStreamAsync(state, parameters, subscribe, rpcId, token), state, rpcId, seed, cancellation);
        try
        {
            if (subscribe && seed != null)
            {
                // GetTask already confirmed the handle. A silent subscription cannot delay Ready or task controls.
                if (!_streams.TryAdd(state.RemoteId, session)) throw new InvalidOperationException("A stream already owns this remote task.");
                return seed;
            }
            var first = ObserveContext(state, await session.FirstAsync(cancellation));
            if (first.Id == null || first.Outcome != null) await session.DisposeAsync();
            else if (!_streams.TryAdd(first.Id, session)) throw new InvalidOperationException("A stream already owns this remote task.");
            return first;
        }
        catch { await session.DisposeAsync();throw; }
    }
    private async Task<HttpResponseMessage> ConnectStreamAsync(RemoteCheckpoint state, object parameters, bool subscribe, string rpcId, CancellationToken cancellation)
    {
        bool legacy = A2aWire.Legacy(state);
        object body = parameters;
        HttpMethod method = HttpMethod.Post;
        Uri address;
        if (state.Binding == "JSONRPC")
        {
            string operation = subscribe ? legacy ? "tasks/resubscribe" : "SubscribeToTask" : legacy ? "message/stream" : "SendStreamingMessage";
            body = new { jsonrpc = "2.0", id = rpcId, method = operation, @params = parameters };
            address = new Uri(state.Endpoint);
        }
        else
        {
            string path = subscribe ? "/tasks/" + Uri.EscapeDataString(state.RemoteId) + ":subscribe" : "/message:stream";
            address = new Uri(state.Endpoint.TrimEnd('/') + (legacy ? "/v1" : "") + path);
            if (subscribe) { body = null;if (legacy) method = HttpMethod.Get; }
        }
        RequireOrigin(address);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        using var request = new HttpRequestMessage(method, address);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        request.Headers.Add("A2A-Version", state.Version);
        if (_token != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        if (body != null) request.Content = JsonContent.Create(body);
        var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        int status = (int)response.StatusCode;
        if (!response.IsSuccessStatusCode)
        {
            response.Dispose();
            if (status == 408 || status == 429 || status >= 500) throw new RemoteTransientException(status);
            throw new IOException("A2A streaming request failed with status " + status + ".");
        }
        if (response.Content.Headers.ContentType?.MediaType != "text/event-stream")
        { response.Dispose();throw new A2aStreamProtocolException("A2A stream requires text/event-stream content."); }
        return response;
    }
    private async ValueTask CloseStreamAsync(RemoteCheckpoint state)
    {
        if (state.RemoteId != null && _streams.TryRemove(state.RemoteId, out var stream)) await stream.DisposeAsync();
    }
    protected override async ValueTask FinishAsync(RemoteCheckpoint state)
    {
        try
        {
            await CloseStreamAsync(state);
        }
        finally
        {
            if (_contextLeases.TryRemove(state, out var lease)) lease.Dispose();
        }
    }
}
