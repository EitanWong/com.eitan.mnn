using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MNN.Samples.SiriAI.Harnesses;

namespace SiriAI.HarnessHost;

/// <summary>Explicit 1.0 and 0.3 schema/protobuf-JSON boundaries; no A2A type escapes into the Unity runtime.</summary>
internal static class A2aWire
{
    internal static string String(JsonElement value, string name) => value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String ? property.GetString() : null;
    internal static bool Legacy(RemoteCheckpoint state) => state.Version == "0.3";
    internal static bool Schema(RemoteCheckpoint state) => Legacy(state) && state.Binding == "JSONRPC";
    internal static object Send(string text, RemoteCheckpoint state, bool continuation, HarnessContentPart[] attachments = null, bool stream = false)
    {
        bool schema = Schema(state);
        var message = new Dictionary<string, object> { ["messageId"] = Guid.NewGuid().ToString("N"), ["role"] = schema ? "user" : "ROLE_USER" };
        if (schema) message["kind"] = "message";
        object part = schema ? new { kind = "text", text } : (object)new { text, mediaType = "text/plain" };
        message[Legacy(state) && !schema ? "content" : "parts"] = new[] { part }.Concat((attachments ?? []).Select(value => OutgoingPart(value, state))).ToArray();
        if (continuation) message["taskId"] = state.RemoteId;
        if (state.ContextId != null) message["contextId"] = state.ContextId;
        var configuration = new Dictionary<string, object> { ["acceptedOutputModes"] = new[] { "text/plain", "application/json" }, ["historyLength"] = 20 };
        configuration[Legacy(state) ? "blocking" : "returnImmediately"] = Legacy(state) ? stream : !stream;
        return new Dictionary<string, object> { ["message"] = message, ["configuration"] = configuration };
    }
    internal static object OutgoingPart(HarnessContentPart part, RemoteCheckpoint state)
    {
        bool schema = Schema(state);
        var result = new Dictionary<string, object>();
        if (!A2aWire.Legacy(state))
        {
            if (!string.IsNullOrWhiteSpace(part.MediaType)) result["mediaType"] = part.MediaType;
            if (!string.IsNullOrWhiteSpace(part.FileName)) result["filename"] = part.FileName;
        }
        if (part.Kind == HarnessContentKind.Text)
        {
            if (schema) result["kind"] = "text";
            result["text"] = part.Payload;return result;
        }
        if (part.Kind == HarnessContentKind.Json)
        {
            using var document = JsonDocument.Parse(part.Payload);
            if (document.RootElement.ValueKind != JsonValueKind.Object) throw new ArgumentException("A2A data parts require a JSON object.");
            object data = document.RootElement.Clone();
            if (schema) result["kind"] = "data";
            result["data"] = Legacy(state) && !schema ? new { data } : data;
            return result;
        }
        if (!Legacy(state))
        { result[part.Kind == HarnessContentKind.Url ? "url" : "raw"] = part.Payload;return result; }
        var file = new Dictionary<string, object> { ["mimeType"] = part.MediaType ?? "application/octet-stream" };
        file[part.Kind == HarnessContentKind.Url ? schema ? "uri" : "fileWithUri" : schema ? "bytes" : "fileWithBytes"] = part.Payload;
        if (schema && part.FileName != null) file["name"] = part.FileName;
        if (schema) result["kind"] = "file";
        result["file"] = file;return result;
    }
    internal static RemoteSnapshot Project(JsonElement root, RemoteCheckpoint state, bool send)
    {
        if (send)
        {
            if (root.TryGetProperty("task", out var task)) root = task;
            else if (root.TryGetProperty("message", out var message) || root.TryGetProperty("msg", out message)) return Message(message, state);
            else if (String(root, "kind") == "message") return Message(root, state);
        }
        if (!root.TryGetProperty("status", out var status)) throw new IOException("A2A returned neither a Task nor a Message.");
        string id = Required(String(root, "id"), 128);
        string context = String(root, "contextId");
        if (context?.Length > 128) throw new IOException("A2A context ID exceeds its budget.");
        string value = Required(String(status, "state"), 64).Replace("TASK_STATE_", "").ToLowerInvariant().Replace('_', '-');
        string text = status.TryGetProperty("message", out var msg) ? Text(msg, state) : null;
        if (string.IsNullOrWhiteSpace(text) && root.TryGetProperty("history", out var history))
            foreach (var item in history.EnumerateArray().Reverse())
                if (String(item, "role") == "ROLE_AGENT" || String(item, "role") == "agent")
                { text = Text(item, state); if (!string.IsNullOrWhiteSpace(text)) break; }
        var artifacts = Artifacts(root, state);
        HarnessOutcome outcome = value switch
        {
            "completed" => HarnessOutcome.Completed(text ?? "Remote task completed. See attached artifacts.", artifacts),
            "failed" or "rejected" => HarnessOutcome.Failed(text ?? "Remote agent reported " + value + "."),
            "canceled" or "cancelled" => HarnessOutcome.Cancelled(),
            "submitted" or "working" or "unspecified" or "input-required" or "auth-required" or "cancelling" => null,
            _ => throw new IOException("Unsupported A2A task state.")
        };
        HarnessActivity activity = null;
        if (outcome == null)
        {
            bool input = value == "input-required" || value == "auth-required";
            string promptIdentity = status.TryGetProperty("message", out var prompt) ? String(prompt, "messageId") : null;
            string requestId = input ? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id + "\n" + value + "\n" + promptIdentity + "\n" + text))) : null;
            string summary = text ?? "Remote task: " + value;
            activity = new HarnessActivity(input ? HarnessWaitingFor.Input : HarnessWaitingFor.None, requestId, Trim(summary, 4096), artifacts);
        }
        return new RemoteSnapshot(id, context, activity, outcome);
    }
    private static RemoteSnapshot Message(JsonElement message, RemoteCheckpoint state)
    {
        string text = Text(message, state);
        var parts = Parts(message, state, true);
        return new RemoteSnapshot(null, String(message, "contextId"), null, HarnessOutcome.Completed(text ?? "Remote agent returned content.",
            parts.Length == 0 ? null : new[] { new HarnessArtifact("message", "Agent message", null, parts) }));
    }
    private static string Text(JsonElement message, RemoteCheckpoint state)
    {
        string field = Legacy(state) && !Schema(state) ? "content" : "parts";
        if (!message.TryGetProperty(field, out var parts)) return null;
        string text = string.Join("\n", parts.EnumerateArray().Select(part => String(part, "text")).Where(value => !string.IsNullOrWhiteSpace(value)));
        return text.Length == 0 ? null : Trim(text, 65536);
    }
    private static HarnessArtifact[] Artifacts(JsonElement task, RemoteCheckpoint state)
    {
        if (!task.TryGetProperty("artifacts", out var values)) return [];
        if (values.GetArrayLength() > 32) throw new IOException("Too many A2A artifacts.");
        return values.EnumerateArray().Select(item => new HarnessArtifact(Required(String(item, "artifactId"), 160),
            String(item, "name"), String(item, "description"), Parts(item, state, false))).ToArray();
    }
    private static HarnessContentPart[] Parts(JsonElement value, RemoteCheckpoint state, bool message)
    {
        string field = message && Legacy(state) && !Schema(state) ? "content" : "parts";
        if (!value.TryGetProperty(field, out var parts)) return [];
        if (parts.GetArrayLength() > 64) throw new IOException("Too many A2A content parts.");
        return parts.EnumerateArray().Select(part => Part(part, state)).ToArray();
    }
    private static HarnessContentPart Part(JsonElement part, RemoteCheckpoint state)
    {
        string mime = String(part, "mediaType"), name = String(part, "filename");
        if (String(part, "text") is string text) return new HarnessContentPart(HarnessContentKind.Text, text, mime ?? "text/plain", name);
        if (String(part, "url") is string url) return new HarnessContentPart(HarnessContentKind.Url, url, mime, name);
        if (String(part, "raw") is string raw) return new HarnessContentPart(HarnessContentKind.Base64, raw, mime, name);
        if (part.TryGetProperty("file", out var file))
        {
            mime = String(file, "mimeType"); name = String(file, "name");
            string uri = String(file, Schema(state) ? "uri" : "fileWithUri");
            return uri != null ? new HarnessContentPart(HarnessContentKind.Url, uri, mime, name) :
                new HarnessContentPart(HarnessContentKind.Base64, String(file, Schema(state) ? "bytes" : "fileWithBytes"), mime, name);
        }
        if (part.TryGetProperty("data", out var data))
        {
            if (Legacy(state) && !Schema(state)) data = data.GetProperty("data");
            return new HarnessContentPart(HarnessContentKind.Json, data.GetRawText(), "application/json", name);
        }
        throw new IOException("Unsupported A2A content part.");
    }
    private static string Required(string value, int limit) => !string.IsNullOrWhiteSpace(value) && value.Length <= limit ? value : throw new IOException("Missing or oversized A2A identity/state.");
    private static string Trim(string text, int limit) => text.Length <= limit ? text : text[..limit];
}
