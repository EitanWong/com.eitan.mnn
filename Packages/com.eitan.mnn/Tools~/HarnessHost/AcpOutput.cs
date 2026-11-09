using System.Text;
using System.Text.Json;
using MNN.Samples.SiriAI.Harnesses;

namespace SiriAI.HarnessHost;

/// <summary>One prompt's bounded native content; terminal state and partial artifacts are independent.</summary>
internal sealed class AcpOutput
{
    private readonly object _sync = new();
    private readonly StringBuilder _text = new();
    private readonly HarnessTask _task;
    private HarnessArtifact[] _artifacts = [];
    private string _error;
    internal AcpOutput(HarnessTask task = null) { _task = task?.Copy(); }
    internal HarnessArtifact[] Artifacts { get { lock (_sync) return (HarnessArtifact[])_artifacts.Clone(); } }
    internal bool Add(JsonElement content)
    {
        lock (_sync)
        {
            if (_error != null) return false;
            try
            {
                if (Required(content, "type") == "text")
                {
                    string text = Required(content, "text", allowEmpty: true);
                    if (_text.Length + text.Length > 65536) throw new ArgumentException("Text exceeds the result budget.");
                    RequireWireBudget(_artifacts, _text.ToString() + text);
                    _text.Append(text); return false;
                }
                int index = _artifacts.Length + 1;
                var part = Part(content, "agent-output-" + index);
                string display = part.FileName;
                if (display.Length > 240) display = display[..(char.IsHighSurrogate(display[239]) ? 239 : 240)];
                var artifact = new HarnessArtifact("acp-content-" + index, display, null, [part]);
                var next = HarnessArtifact.Checked(_artifacts.Append(artifact).ToArray());
                RequireWireBudget(next, _text.ToString());
                _artifacts = next;
                return true;
            }
            catch (Exception error) when (error is ArgumentException or FormatException or InvalidOperationException or KeyNotFoundException or JsonException)
            {
                _error = "ACP output rejected: " + (error.Message.Length > 1000 ? error.Message[..1000] : error.Message);
                return false;
            }
        }
    }
    private void RequireWireBudget(HarnessArtifact[] artifacts, string text)
    {
        if (_task == null) return;
        var snapshot = _task.Copy(); snapshot.Artifacts = artifacts; snapshot.Result = text;
        // Leave space for terminal/control/lease fields and escaped native checkpoint metadata.
        if (TaskHostJson.Write(snapshot).Length > 900000)
            throw new ArgumentException("Serialized output exceeds the foreground response budget.");
    }
    internal HarnessOutcome Finish(string reason)
    {
        lock (_sync)
        {
            var outcome = reason == "cancelled" ? HarnessOutcome.Cancelled() : reason != "end_turn"
                ? HarnessOutcome.Failed("ACP ended without successful completion: " + reason)
                : _error != null ? HarnessOutcome.Failed(_error)
                : _text.Length > 0 ? HarnessOutcome.Completed(_text.ToString())
                : _artifacts.Length > 0 ? HarnessOutcome.Completed("ACP completed. See attached artifacts.")
                : HarnessOutcome.Failed("ACP completed without a text result or an artifact.");
            return outcome.WithArtifacts(_artifacts);
        }
    }
    internal HarnessOutcome Interrupted(string message)
    {
        lock (_sync) return HarnessOutcome.Interrupted(_error == null ? message : message + " " + _error).WithArtifacts(_artifacts);
    }
    private static HarnessContentPart Part(JsonElement block, string fallback)
    {
        string type = Required(block, "type");
        if (type is "image" or "audio")
            return new HarnessContentPart(HarnessContentKind.Base64, Convert.ToBase64String(Convert.FromBase64String(Required(block, "data", true))),
                Required(block, "mimeType"), FileName(Optional(block, "uri"), fallback));
        if (type == "resource")
        {
            var resource = block.GetProperty("resource"); string uri = Required(resource, "uri");
            bool text = resource.TryGetProperty("text", out _), blob = resource.TryGetProperty("blob", out _);
            if (text == blob) throw new ArgumentException("Embedded resource requires exactly one text or blob payload.");
            string payload = Required(resource, text ? "text" : "blob", true);
            if (blob) payload = Convert.ToBase64String(Convert.FromBase64String(payload));
            return new HarnessContentPart(text ? HarnessContentKind.Text : HarnessContentKind.Base64, payload,
                Optional(resource, "mimeType") ?? (text ? "text/plain" : "application/octet-stream"), FileName(uri, fallback));
        }
        if (type != "resource_link") throw new ArgumentException("Unsupported native content type: " + type);
        string reference = Required(block, "uri"), name = Optional(block, "name");
        if (string.IsNullOrWhiteSpace(name)) name = FileName(reference, fallback);
        string mime = Optional(block, "mimeType");
        if (reference.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return DataUri(reference, name, mime);
        if (Uri.TryCreate(reference, UriKind.Absolute, out var parsed) && parsed.Scheme is "http" or "https")
            return new HarnessContentPart(HarnessContentKind.Url, reference, mime ?? "application/octet-stream", name);
        return new HarnessContentPart(HarnessContentKind.Json, block.GetRawText(), "application/vnd.siriai.acp-resource-link+json", name);
    }
    private static HarnessContentPart DataUri(string uri, string name, string mime)
    {
        int comma = uri.IndexOf(',');
        if (comma < 5) throw new ArgumentException("Malformed data URI.");
        string header = uri[5..comma], body = uri[(comma + 1)..];
        var bytes = new List<byte>();
        for (int offset = 0; offset < body.Length;)
        {
            if (body[offset] == '%')
            {
                if (offset + 2 >= body.Length || !byte.TryParse(body.AsSpan(offset + 1, 2), System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out byte value)) throw new ArgumentException("Invalid data URI percent encoding.");
                bytes.Add(value); offset += 3;
            }
            else
            {
                int end = body.IndexOf('%', offset); if (end < 0) end = body.Length;
                bytes.AddRange(new UTF8Encoding(false, true).GetBytes(body[offset..end])); offset = end;
            }
        }
        byte[] data = bytes.ToArray();
        bool encoded = header.EndsWith(";base64", StringComparison.OrdinalIgnoreCase);
        if (encoded) data = Convert.FromBase64String(Encoding.ASCII.GetString(data));
        string declared = encoded ? header[..^7] : header;
        if (string.IsNullOrWhiteSpace(declared)) declared = "text/plain;charset=US-ASCII";
        else if (declared.StartsWith(';')) declared = "text/plain" + declared;
        return new HarnessContentPart(HarnessContentKind.Base64, Convert.ToBase64String(data),
            mime ?? declared, name);
    }
    private static string FileName(string uri, string fallback)
    {
        if (string.IsNullOrWhiteSpace(uri) || uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return fallback;
        string path = Uri.TryCreate(uri, UriKind.Absolute, out var parsed) ? parsed.GetComponents(UriComponents.Path, UriFormat.UriEscaped) : uri;
        string name = Uri.UnescapeDataString(path.TrimEnd('/').Split('/').Last());
        return string.IsNullOrWhiteSpace(name) ? fallback : name;
    }
    private static string Required(JsonElement value, string key, bool allowEmpty = false)
    {
        string text = Optional(value, key);
        return text != null && (allowEmpty || !string.IsNullOrWhiteSpace(text)) ? text : throw new ArgumentException("Missing native output " + key + ".");
    }
    private static string Optional(JsonElement value, string key)
    {
        var field = MspWire.Field(value, key);
        return field.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? null : field.ValueKind == JsonValueKind.String
            ? field.GetString() : throw new ArgumentException("Native output " + key + " must be text.");
    }
}
