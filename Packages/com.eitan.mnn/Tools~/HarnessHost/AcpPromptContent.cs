using System.Text;
using System.Text.Json;
using MNN.Samples.SiriAI.Harnesses;

namespace SiriAI.HarnessHost;

/// <summary>ACP1 content projection. URI inputs remain references; the Host never fetches them.</summary>
internal static class AcpPromptContent
{
    internal static JsonElement[] Create(string objective, HarnessContentPart[] attachments)
    {
        var blocks = new List<object> { new { type = "text", text = objective } };
        for (int i = 0; i < attachments.Length; i++) blocks.Add(Attachment(attachments[i], i + 1));
        // Reserve room for the bounded native session identity and JSON-RPC envelope.
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(blocks);
        RequireWireBudget(json.Length);
        using var document = JsonDocument.Parse(json);
        return document.RootElement.EnumerateArray().Select(block => block.Clone()).ToArray();
    }

    internal static void RequireWireBudget(JsonElement[] blocks) => RequireWireBudget(JsonSerializer.SerializeToUtf8Bytes(blocks).Length);
    private static void RequireWireBudget(int bytes)
    {
        if (bytes > 900000) throw new NotSupportedException("ACP prompt exceeds the escaped JSON wire budget.");
    }

    private static object Attachment(HarnessContentPart part, int index)
    {
        string name = string.IsNullOrWhiteSpace(part.FileName) ? "attachment-" + index : part.FileName;
        string uri = "siriai://input/" + index + "/" + Uri.EscapeDataString(name);
        string mime = string.IsNullOrWhiteSpace(part.MediaType)
            ? part.Kind == HarnessContentKind.Text ? "text/plain" : part.Kind == HarnessContentKind.Json ? "application/json" : "application/octet-stream"
            : part.MediaType;
        if (part.Kind == HarnessContentKind.Url)
            return new { type = "resource_link", uri = part.Payload, name, mimeType = mime };
        if (part.Kind is HarnessContentKind.Text or HarnessContentKind.Json)
            return new { type = "resource", resource = new { uri, mimeType = mime, text = part.Payload } };
        byte[] bytes = Convert.FromBase64String(part.Payload);
        string data = Convert.ToBase64String(bytes);
        if (mime.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return new { type = "image", mimeType = mime, data, uri };
        if (mime.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
            return new { type = "audio", mimeType = mime, data };
        string type = mime.Split(';')[0].Trim().ToLowerInvariant();
        if (type.StartsWith("text/", StringComparison.Ordinal) || type is "application/json" or "application/javascript" or "application/xml" or "application/yaml" or "application/x-yaml")
        {
            string text;
            try { text = new UTF8Encoding(false, true).GetString(bytes); }
            catch (DecoderFallbackException error) { throw new ArgumentException("ACP embedded text requires valid UTF8 bytes.", error); }
            return new { type = "resource", resource = new { uri, mimeType = mime, text } };
        }
        return new { type = "resource", resource = new { uri, mimeType = mime, blob = data } };
    }

    internal static void RequireCapabilities(JsonElement[] blocks, JsonElement capabilities)
    {
        foreach (var block in blocks)
        {
            string capability = MspWire.Text(block, "type") switch
            { "image" => "image", "audio" => "audio", "resource" => "embeddedContext", _ => null };
            if (capability != null && MspWire.Field(capabilities, capability).ValueKind != JsonValueKind.True)
                throw new AcpPromptCapabilityException("Agent did not advertise prompt capability " + capability + ". No prompt was sent.");
        }
    }
}

internal sealed class AcpPromptCapabilityException : NotSupportedException
{
    internal AcpPromptCapabilityException(string message) : base(message) { }
}
