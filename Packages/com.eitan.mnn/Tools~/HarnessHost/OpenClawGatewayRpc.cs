using System.Net.WebSockets;
using System.Text.Json;

namespace SiriAI.HarnessHost;

/// <summary>OpenClaw Gateway v3/v4: one authenticated challenge/connect and one correlated RPC per socket.</summary>
internal sealed class OpenClawGatewayRpc
{
    private readonly Uri _endpoint;
    private readonly string _token;
    internal OpenClawGatewayRpc(HarnessConfiguration configuration)
    {
        var address = new Uri(configuration.Endpoint ?? "http://127.0.0.1:18789");
        _endpoint = new UriBuilder(address) { Scheme = address.Scheme is "https" or "wss" ? "wss" : "ws",
            Port = address.IsDefaultPort ? -1 : address.Port, Path = "/", Query = "", Fragment = "" }.Uri;
        _token = configuration.ProcessEnvironment.GetValueOrDefault("OPENCLAW_GATEWAY_TOKEN") ??
            (configuration.ForwardEnvironment.Contains("OPENCLAW_GATEWAY_TOKEN", StringComparer.Ordinal)
                ? Environment.GetEnvironmentVariable("OPENCLAW_GATEWAY_TOKEN") : null) ?? "";
    }
    internal async Task<JsonElement> CallAsync(string method, object parameters, int timeout, CancellationToken cancellation)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellation); budget.CancelAfter(timeout);
        using var socket = new ClientWebSocket(); socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
        try
        {
            await socket.ConnectAsync(_endpoint, budget.Token);
            while (true)
            {
                var challenge = await ReadAsync(socket, budget.Token);
                if (MspWire.Text(challenge, "type") == "event" && MspWire.Text(challenge, "event") == "connect.challenge") break;
            }
            var hello = await RequestAsync(socket, "connect", new
            {
                minProtocol = 3, maxProtocol = 4,
                client = new { id = "gateway-client", displayName = "SiriAI", version = "0.2.0", platform = Environment.OSVersion.Platform.ToString().ToLowerInvariant(), mode = "backend" },
                caps = new[] { "tool-events" }, role = "operator", scopes = new[] { "operator.read", "operator.write", "operator.admin" },
                auth = string.IsNullOrEmpty(_token) ? null : new { token = _token }
            }, budget.Token);
            if (MspWire.Text(hello, "type") != "hello-ok" || !MspWire.Field(hello, "protocol").TryGetInt32(out int protocol) || protocol is < 3 or > 4)
                throw new IOException("OpenClaw Gateway did not confirm protocol3/4.");
            return await RequestAsync(socket, method, parameters, budget.Token);
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
        { throw new IOException("OpenClaw Gateway operation exceeded its response budget."); }
        catch (WebSocketException) { throw new IOException("OpenClaw Gateway connection ended without a confirmed response."); }
    }
    private static async Task<JsonElement> RequestAsync(ClientWebSocket socket, string method, object parameters, CancellationToken cancellation)
    {
        string id = Guid.NewGuid().ToString("N");
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new { type = "req", id, method, @params = parameters },
            new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
        if (bytes.Length > 65536) throw new IOException("OpenClaw Gateway request exceeds its wire budget.");
        await socket.SendAsync(bytes.AsMemory(), WebSocketMessageType.Text, true, cancellation);
        while (true)
        {
            var frame = await ReadAsync(socket, cancellation);
            if (MspWire.Text(frame, "type") != "res" || MspWire.Text(frame, "id") != id) continue;
            if (MspWire.Field(frame, "ok").ValueKind != JsonValueKind.True)
                throw new IOException("OpenClaw Gateway rejected " + method + "; no operation was replayed.");
            return MspWire.Field(frame, "payload").Clone();
        }
    }
    private static async Task<JsonElement> ReadAsync(ClientWebSocket socket, CancellationToken cancellation)
    {
        using var data = new MemoryStream(); byte[] chunk = new byte[8192];
        while (true)
        {
            var result = await socket.ReceiveAsync(chunk.AsMemory(), cancellation);
            if (result.MessageType != WebSocketMessageType.Text || data.Length + result.Count > 1048576)
                throw new IOException("OpenClaw Gateway returned a closed, nontext or oversized frame.");
            data.Write(chunk, 0, result.Count);
            if (!result.EndOfMessage) continue;
            using var document = JsonDocument.Parse(data.ToArray());
            if (document.RootElement.ValueKind != JsonValueKind.Object) throw new IOException("Malformed OpenClaw Gateway response.");
            return document.RootElement.Clone();
        }
    }
}
