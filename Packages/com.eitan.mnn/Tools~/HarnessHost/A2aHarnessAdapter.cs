using System.Net.Http.Headers;
using System.Text.Json;
using MNN.Samples.SiriAI.Harnesses;

namespace SiriAI.HarnessHost;

/// <summary>Polling A2A client for explicitly advertised 1.0/0.3 JSONRPC and HTTP+JSON bindings.</summary>
internal sealed partial class A2aHarnessAdapter : RemotePollingAdapter
{
    private readonly HttpClient _http = new(new HttpClientHandler { AllowAutoRedirect = false });
    private readonly string _token;
    private readonly HashSet<string> _origins;
    protected override bool ReplayAdmission => false;
    public override HarnessCapabilities Capabilities => HarnessCapabilities.Cancel | HarnessCapabilities.Input | HarnessCapabilities.Attachments;
    internal A2aHarnessAdapter(HarnessConfiguration configuration) : base(configuration)
    {
        _token = string.IsNullOrWhiteSpace(configuration.TokenEnvironmentVariable) ? null : Environment.GetEnvironmentVariable(configuration.TokenEnvironmentVariable);
        if (configuration.TokenEnvironmentVariable != null && string.IsNullOrWhiteSpace(_token)) throw new ArgumentException("Configured A2A token is missing.");
        _origins = new HashSet<string>(configuration.AllowedOrigins.Select(value => HarnessHostAddress.Parse(value).GetLeftPart(UriPartial.Authority)), StringComparer.OrdinalIgnoreCase)
            { Endpoint.GetLeftPart(UriPartial.Authority) };
        _http.Timeout = TimeSpan.FromSeconds(30);
    }
    protected override async Task<RemoteSnapshot> AdmitAsync(HarnessTask task, RemoteCheckpoint state, Action save, CancellationToken cancellation)
    {
        var cardUri = new Uri(Endpoint, Configuration.AgentCardPath);
        var card = await RequestAsync(HttpMethod.Get, cardUri, null, cancellation, "1.0");
        SelectInterface(card, state);
        state.Streaming = card.TryGetProperty("capabilities", out var capabilities) && capabilities.TryGetProperty("streaming", out var streaming) && streaming.ValueKind == JsonValueKind.True;
        // Save negotiated binding before sending; a missing remote ID remains explicitly unrecoverable.
        save();
        var request = A2aWire.Send(task.Objective, state, false, task.Attachments, state.Streaming);
        return state.Streaming ? await OpenStreamAsync(state, request, false, null, cancellation) : await CallAsync(state, "send", request, cancellation);
    }
    private void SelectInterface(JsonElement card, RemoteCheckpoint state)
    {
        if (card.TryGetProperty("supportedInterfaces", out var interfaces))
        {
            foreach (var item in interfaces.EnumerateArray())
            {
                if (!Supported(A2aWire.String(item, "protocolVersion"), A2aWire.String(item, "protocolBinding"))) continue;
                if (!string.IsNullOrEmpty(A2aWire.String(item, "tenant"))) continue; // Tenant routing is not implemented.
                Assign(state, A2aWire.String(item, "url"), A2aWire.String(item, "protocolBinding"), A2aWire.String(item, "protocolVersion"));
                return;
            }
        }
        else
        {
            string version = A2aWire.String(card, "protocolVersion"), binding = A2aWire.String(card, "preferredTransport") ?? "JSONRPC";
            if (Supported(version, binding)) { Assign(state, A2aWire.String(card, "url"), binding, version); return; }
            if (card.TryGetProperty("additionalInterfaces", out var additional))
                foreach (var item in additional.EnumerateArray())
                    if (Supported(version, A2aWire.String(item, "transport")))
                    { Assign(state, A2aWire.String(item, "url"), A2aWire.String(item, "transport"), version); return; }
        }
        throw new NotSupportedException("Agent Card has no supported non-tenant A2A 1.0/0.3 JSONRPC or HTTP+JSON interface.");
    }
    private static bool Supported(string version, string binding) => (version is "1.0" or "1.0.0" or "0.3" or "0.3.0") && (binding is "JSONRPC" or "HTTP+JSON");
    private void Assign(RemoteCheckpoint state, string url, string binding, string version)
    {
        HarnessHostAddress.Parse(url);
        var endpoint = new Uri(url, UriKind.Absolute);
        RequireOrigin(endpoint);
        state.Endpoint = endpoint.AbsoluteUri; state.Binding = binding; state.Version = version.StartsWith("0.3") ? "0.3" : "1.0";
    }
    private void RequireOrigin(Uri address)
    {
        if (!_origins.Contains(address.GetLeftPart(UriPartial.Authority))) throw new UnauthorizedAccessException("Agent Card selected an unconfigured origin.");
    }
    private Task<RemoteSnapshot> SendAsync(RemoteCheckpoint state, string text, bool continuation, CancellationToken cancellation) =>
        CallAsync(state, "send", A2aWire.Send(text, state, continuation), cancellation);
    protected override async Task<RemoteSnapshot> GetAsync(RemoteCheckpoint state, CancellationToken cancellation)
    {
        if (_streams.TryGetValue(state.RemoteId, out var stream))
        {
            var update = await stream.NextAsync();
            if (update != null) return update;
            return await CallAsync(state, "get", new { id = state.RemoteId, historyLength = 20 }, cancellation);
        }
        var snapshot = await CallAsync(state, "get", new { id = state.RemoteId, historyLength = 20 }, cancellation);
        return state.Streaming && snapshot.Outcome == null && snapshot.Activity.WaitingFor == HarnessWaitingFor.None
            ? await OpenStreamAsync(state, new { id = state.RemoteId }, true, snapshot, cancellation) : snapshot;
    }
    protected override Task<RemoteSnapshot> CancelRemoteAsync(RemoteCheckpoint state, CancellationToken cancellation) =>
        CallAsync(state, "cancel", new { id = state.RemoteId }, cancellation);
    protected override async Task<RemoteSnapshot> RespondRemoteAsync(RemoteCheckpoint state, string requestId, string reply, bool allow, CancellationToken cancellation)
    {
        await FinishAsync(state);
        var snapshot = await SendAsync(state, reply, true, cancellation);
        return state.Streaming && snapshot.Outcome == null && snapshot.Activity.WaitingFor == HarnessWaitingFor.None
            ? await OpenStreamAsync(state, new { id = state.RemoteId }, true, snapshot, cancellation) : snapshot;
    }
    private async Task<RemoteSnapshot> CallAsync(RemoteCheckpoint state, string operation, object parameters, CancellationToken cancellation)
    {
        JsonElement response;
        if (state.Binding == "JSONRPC")
        {
            string method = (A2aWire.Legacy(state), operation) switch
            { (true, "send") => "message/send", (true, "get") => "tasks/get", (true, _) => "tasks/cancel", (false, "send") => "SendMessage", (false, "get") => "GetTask", _ => "CancelTask" };
            string requestId = Guid.NewGuid().ToString("N");
            response = await RequestAsync(HttpMethod.Post, new Uri(state.Endpoint), new { jsonrpc = "2.0", id = requestId, method, @params = parameters }, cancellation, state.Version);
            if (A2aWire.String(response, "jsonrpc") != "2.0" || A2aWire.String(response, "id") != requestId || response.TryGetProperty("error", out _))
                throw new IOException("A2A JSONRPC returned an error or an unrelated response.");
            response = response.GetProperty("result");
        }
        else
        {
            string prefix = A2aWire.Legacy(state) ? "/v1" : "";
            string path = operation == "send" ? "/message:send" : "/tasks/" + Uri.EscapeDataString(state.RemoteId) + (operation == "cancel" ? ":cancel" : "?historyLength=20");
            response = await RequestAsync(operation == "get" ? HttpMethod.Get : HttpMethod.Post, new Uri(state.Endpoint.TrimEnd('/') + prefix + path), operation == "get" ? null : operation == "send" ? parameters : new { }, cancellation, state.Version);
        }
        return A2aWire.Project(response, state, operation == "send");
    }
    private async Task<JsonElement> RequestAsync(HttpMethod method, Uri address, object body, CancellationToken cancellation, string version = null)
    {
        RequireOrigin(address);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        using var request = new HttpRequestMessage(method, address);
        if (version != null) request.Headers.Add("A2A-Version", version);
        if (_token != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        if (body != null) request.Content = JsonContent.Create(body);
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        int status = (int)response.StatusCode;
        if (status == 408 || status == 429 || status >= 500) throw new RemoteTransientException(status);
        if (!response.IsSuccessStatusCode) throw new IOException("A2A HTTP request failed with status " + status + ".");
        if (response.Content.Headers.ContentLength > 1024 * 1024) throw new IOException("A2A response exceeds 1 MiB.");
        using var input = await response.Content.ReadAsStreamAsync(timeout.Token);
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        int count;
        while ((count = await input.ReadAsync(buffer, timeout.Token)) > 0)
        {
            if (output.Length + count > 1024 * 1024) throw new IOException("A2A response exceeds 1 MiB.");
            output.Write(buffer, 0, count);
        }
        using var document = JsonDocument.Parse(output.ToArray());
        return document.RootElement.Clone();
    }
    public override async ValueTask DisposeAsync()
    {
        foreach (var stream in _streams.Values) await stream.DisposeAsync();
        _streams.Clear();_http.Dispose();
    }
}
