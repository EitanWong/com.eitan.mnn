using System.Runtime.Serialization;
using System.Text.Json;
using MNN.Samples.SiriAI.Harnesses;

namespace SiriAI.HarnessHost;

/// <summary>One prompt's tool field projection. Null typed optional fields preserve their previous value.</summary>
internal sealed class AcpToolUpdates
{
    private static readonly string[] Fields = ["title", "kind", "status", "name", "content", "locations", "rawInput", "rawOutput", "_meta"];
    private readonly Dictionary<string, Tool> _tools = new(StringComparer.Ordinal);
    private readonly List<string> _order = new();
    private bool _truncated;
    internal HarnessNativeToolCall Apply(JsonElement update)
    {
        string id = MspWire.Required(update, "toolCallId");
        if (id.Length > 4096) throw new IOException("Native tool identity exceeds its budget.");
        if (!_tools.TryGetValue(id, out var tool))
        {
            tool = new Tool(id); _tools.Add(id, tool);
        }
        foreach (string field in Fields)
        {
            if (!update.TryGetProperty(field, out var value)) continue;
            if (value.ValueKind == JsonValueKind.Null && field is not ("rawInput" or "rawOutput" or "_meta")) continue;
            ValidateField(field, value);
            int maximum = field == "title" ? 1024 : 8192;
            if (value.GetRawText().Length > maximum)
            { tool.Values.Remove(field); tool.Omitted.Add(field); _truncated = true; }
            else { tool.Values[field] = value.Clone(); tool.Omitted.Remove(field); }
        }
        while (JsonSerializer.Serialize(tool.Values).Length > 32768)
        {
            string field = tool.Values.Where(p => p.Key != "toolCallId").OrderByDescending(p => p.Value.GetRawText().Length).First().Key;
            tool.Values.Remove(field); tool.Omitted.Add(field); _truncated = true;
        }
        _order.Remove(id); _order.Add(id);
        while (_order.Count > 32) EvictFirst();
        // Serializer escaping contributes to the actual shared task response budget.
        while (true)
        {
            try { Snapshot(); break; }
            catch (SerializationException) when (_order.Count > 1) { EvictFirst(); }
            catch (SerializationException) when (tool.Values.Count > 1)
            {
                string field = tool.Values.Where(p => p.Key != "toolCallId").OrderByDescending(p => p.Value.GetRawText().Length).First().Key;
                tool.Values.Remove(field); tool.Omitted.Add(field); _truncated = true;
            }
        }
        return tool.Snapshot();
    }
    private static void ValidateField(string field, JsonElement value)
    {
        if (field is "title" or "kind" or "status" or "name")
        {
            if (value.ValueKind != JsonValueKind.String) throw new IOException("Native tool string field is malformed.");
            if (field == "kind" && value.GetString().Length > 128) throw new IOException("Native tool kind exceeds its budget.");
            if (field == "status" && value.GetString() is not ("pending" or "in_progress" or "completed" or "failed"))
                throw new IOException("Native tool status is invalid.");
        }
        if (field is "content" or "locations" && value.ValueKind != JsonValueKind.Array)
            throw new IOException("Native tool collection is malformed.");
    }
    private void EvictFirst() { _tools.Remove(_order[0]); _order.RemoveAt(0); _truncated = true; }
    internal HarnessNativeToolState Snapshot() => new(_order.Select(id => _tools[id].Snapshot()).ToArray(), _truncated);
    private sealed class Tool
    {
        internal readonly Dictionary<string, JsonElement> Values;
        internal readonly HashSet<string> Omitted = new(StringComparer.Ordinal);
        internal Tool(string id) { Values = new(StringComparer.Ordinal) { ["toolCallId"] = JsonSerializer.SerializeToElement(id) }; }
        internal HarnessNativeToolCall Snapshot() => new(Values["toolCallId"].GetString(), Text("title"), Text("kind"), Text("status"),
            JsonSerializer.Serialize(Values), Omitted.OrderBy(v => v, StringComparer.Ordinal).ToArray());
        private string Text(string name) => Values.TryGetValue(name, out var value) ? value.GetString() : null;
    }
}
