using System.Text.Json;
using MNN.Samples.SiriAI.Harnesses;

namespace SiriAI.HarnessHost;

/// <summary>Stateless Streamable HTTP JSON responses, authenticated by dedicated execution grants.</summary>
internal sealed class CoordinatorMcpServer
{
    private static readonly string[] Versions = ["2024-11-05", "2025-03-26", "2025-06-18", "2025-11-25"];
    private readonly CoordinatorMcpScope[] _scopes;
    private readonly CoordinatorMcpTools _tools;
    internal CoordinatorMcpServer(HostConfiguration configuration, PersistentTaskRuntime runtime, CoordinatorMcpRuns runs)
    { _scopes = configuration.CoordinatorMcpScopes; _tools = new CoordinatorMcpTools(runtime, runs); }
    internal async Task HandleAsync(HttpContext context)
    {
        string authorization = context.Request.Headers.Authorization.ToString();
        string token = authorization.StartsWith("Bearer ", StringComparison.Ordinal) ? authorization[7..] : "";
        var scope = token.Length is >= 32 and <= 4096 ? _scopes.FirstOrDefault(s => s.Authenticate(token)) : null;
        if (scope == null) { context.Response.StatusCode = 404; return; }
        string origin = context.Request.Headers.Origin.ToString();
        if (origin.Length > 0 && (!Uri.TryCreate(origin, UriKind.Absolute, out var address) || !address.IsLoopback || address.Scheme is not ("http" or "https")))
        { context.Response.StatusCode = 403; return; }
        if (context.Request.Method != "POST") { context.Response.StatusCode = 405; context.Response.Headers.Allow = "POST"; return; }
        string version = context.Request.Headers["MCP-Protocol-Version"].ToString();
        if (version.Length > 0 && !Versions.Contains(version, StringComparer.Ordinal)) { context.Response.StatusCode = 400; return; }
        JsonElement id = default;
        try
        {
            using var stream = new MemoryStream();
            var buffer = new byte[8192]; int count;
            while ((count = await context.Request.Body.ReadAsync(buffer, context.RequestAborted)) > 0)
            {
                if (stream.Length + count > 128 * 1024) throw new ArgumentException("MCP request exceeds its budget.");
                stream.Write(buffer, 0, count);
            }
            using var document = JsonDocument.Parse(stream.ToArray());
            var frame = document.RootElement;
            if (frame.ValueKind != JsonValueKind.Object || MspWire.Text(frame, "jsonrpc") != "2.0") throw new ArgumentException("Invalid JSON-RPC request.");
            string method = CoordinatorMcpTools.Text(frame, "method", 128);
            if (frame.TryGetProperty("id", out var request))
            {
                if (request.ValueKind is not (JsonValueKind.String or JsonValueKind.Number) || request.GetRawText().Length > 256) throw new ArgumentException("Invalid JSON-RPC id.");
                id = request.Clone();
            }
            if (id.ValueKind == JsonValueKind.Undefined) { context.Response.StatusCode = 202; return; }
            var parameters = frame.TryGetProperty("params", out var value) ? value : JsonSerializer.SerializeToElement(new { });
            if (parameters.ValueKind != JsonValueKind.Object) throw new ArgumentException("MCP params must be an object.");
            object result = method switch
            {
                "initialize" => Initialize(parameters),
                "ping" => new { },
                "tools/list" => new { tools = CoordinatorMcpSchema.Tools },
                "tools/call" => await CallAsync(scope, parameters),
                _ => throw new NotSupportedException("Unknown MCP method.")
            };
            await WriteAsync(context, new { jsonrpc = "2.0", id, result });
        }
        catch (Exception error) when (!context.Response.HasStarted && !context.RequestAborted.IsCancellationRequested)
        {
            object responseId = id.ValueKind == JsonValueKind.Undefined ? null : id;
            int code = error is JsonException ? -32700 : error is NotSupportedException ? -32601 : error is ArgumentException ? -32602 : -32603;
            await WriteAsync(context, new { jsonrpc = "2.0", id = responseId, error = new { code, message = code == -32603 ? "Coordinator MCP operation failed." : "Invalid or unsupported MCP request." } });
        }
    }
    private static object Initialize(JsonElement parameters)
    {
        string requested = CoordinatorMcpTools.Text(parameters, "protocolVersion", 32);
        if (!parameters.TryGetProperty("capabilities", out var capabilities) || capabilities.ValueKind != JsonValueKind.Object ||
            !parameters.TryGetProperty("clientInfo", out var info) || info.ValueKind != JsonValueKind.Object) throw new ArgumentException("MCP client information is required.");
        CoordinatorMcpTools.Text(info, "name", 128); CoordinatorMcpTools.Text(info, "version", 128);
        return new { protocolVersion = Versions.Contains(requested) ? requested : "2025-06-18", capabilities = new { tools = new { } },
            serverInfo = new { name = "siriai-coordinator", version = "1.0.0" },
            instructions = "Delegate independent work asynchronously. Keep request_id stable only when retrying the same intent. Use exact returned session_id/delegation_id. Work keeps the configured workspace and returns results to the foreground conversation. A running coordinator holds its workspace lane: return after admission so delegated work can run." };
    }
    private async Task<object> CallAsync(CoordinatorMcpScope scope, JsonElement parameters)
    {
        CoordinatorMcpTools.Fields(parameters, "name", "arguments", "_meta");
        string name = CoordinatorMcpTools.Text(parameters, "name", 128);
        var arguments = parameters.TryGetProperty("arguments", out var value) ? value : JsonSerializer.SerializeToElement(new { });
        try { return Result(await _tools.CallAsync(scope, name, arguments), false); }
        catch (Exception error) when (error is ArgumentException or UnauthorizedAccessException or NotSupportedException or InvalidOperationException)
        { return Result(new { status = "failed", error = "Coordinator tool input, scope or task state is unavailable." }, true); }
    }
    private static object Result(object value, bool error) => new
    { content = new[] { new { type = "text", text = JsonSerializer.Serialize(value) } }, isError = error };
    private static async Task WriteAsync(HttpContext context, object value)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value);
        if (bytes.Length > 1024 * 1024) throw new ArgumentException("MCP response exceeds its budget.");
        context.Response.ContentType = "application/json";
        await context.Response.Body.WriteAsync(bytes, context.RequestAborted);
    }
}
