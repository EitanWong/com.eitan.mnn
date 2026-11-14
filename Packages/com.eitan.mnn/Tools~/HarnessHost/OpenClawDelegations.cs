using System.Text.Json;
using MNN.Samples.SiriAI.Harnesses;

namespace SiriAI.HarnessHost;

internal sealed class OpenClawCheckpoint
{
    public string Fingerprint { get; set; }
    public string Scope { get; set; }
    public string TaskId { get; set; }
    public string Phase { get; set; } = "native";
    public string NativeCheckpoint { get; set; }
    public long DeadlineUtcTicks { get; set; }
    public OpenClawChild[] Children { get; set; } = [];
    public string Error { get; set; }
}
internal sealed class OpenClawChild
{
    public string RunId { get; set; }
    public string SessionKey { get; set; }
    public string Title { get; set; }
    public string Directory { get; set; }
    public bool AbortRequested { get; set; }
    public bool AbortConfirmed { get; set; }
}

/// <summary>Full public ACP tool updates grant only exact child run/session pairs observed in this task.</summary>
internal sealed class OpenClawDelegations
{
    private readonly Dictionary<string, Dictionary<string, JsonElement>> _tools = new(StringComparer.Ordinal);
    internal static bool Valid(OpenClawCheckpoint state) => state != null && state.Scope?.Length == 64 &&
        state.Phase is "native" or "collecting" or "summarizing" && (state.Phase == "native" || state.TaskId != null) && (state.TaskId == null || state.TaskId.Length is > 0 and <= 128) && state.Children != null && state.Children.Length <= 16 &&
        state.DeadlineUtcTicks >= 0 && state.DeadlineUtcTicks <= DateTime.MaxValue.Ticks &&
        state.Children.All(c => c != null && Bounded(c.RunId) && Bounded(c.SessionKey) && c.Title?.Length <= 160 && c.Directory?.Length <= 4096) &&
        state.Children.Select(c => c.RunId).Distinct(StringComparer.Ordinal).Count() == state.Children.Length;
    private static bool Bounded(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 4096 && !value.Any(char.IsControl);
    internal bool Observe(JsonElement update, OpenClawCheckpoint state, string workspace)
    {
        string id = MspWire.Required(update, "toolCallId");
        if (!_tools.TryGetValue(id, out var fields))
        {
            if (_tools.Count >= 128) throw new IOException("OpenClaw native tool tracking exceeds its budget.");
            _tools[id] = fields = new(StringComparer.Ordinal);
        }
        foreach (string key in new[] { "name", "title", "status", "rawInput", "rawOutput" })
            if (update.TryGetProperty(key, out var value) && value.ValueKind != JsonValueKind.Null) fields[key] = value.Clone();
        if (_tools.Values.Sum(values => values.Values.Sum(value => value.GetRawText().Length)) > 262144)
            throw new IOException("OpenClaw native tool tracking exceeds its wire budget.");
        string Text(string key) => fields.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        string name = (Text("name") ?? Text("title") ?? "").Trim().ToLowerInvariant();
        if (Text("status") != "completed" || !name.Contains("sessions_spawn") && !name.Contains("sessions_send")) return false;
        if (!fields.TryGetValue("rawOutput", out var output)) throw new IOException("Completed OpenClaw delegation has no native output grant.");
        output = Output(output);
        string session = new[] { "childSessionKey", "sessionKey", "session_id", "sessionId" }.Select(k => MspWire.Text(output, k)).FirstOrDefault(Bounded);
        string run = MspWire.Text(output, "runId");
        if (!Bounded(session) || !Bounded(run)) throw new IOException("Completed OpenClaw delegation omitted its exact native run/session identity.");
        var existing = state.Children.FirstOrDefault(c => c.RunId == run);
        if (existing != null)
        {
            if (existing.SessionKey != session) throw new IOException("OpenClaw native run identity changed its session grant.");
            return false;
        }
        if (state.Children.Length >= 16) throw new IOException("OpenClaw native child count exceeds its checkpoint budget.");
        var input = fields.GetValueOrDefault("rawInput");
        string title = MspWire.Text(input, "label") ?? MspWire.Text(input, "task") ?? MspWire.Text(input, "message") ?? "OpenClaw child task";
        string directory = MspWire.Text(input, "cwd") ?? MspWire.Text(input, "directory") ?? workspace;
        if (directory.Length > 4096) throw new IOException("OpenClaw native child directory exceeds its budget.");
        state.Children = state.Children.Append(new OpenClawChild { RunId = run, SessionKey = session, Title = title[..Math.Min(title.Length, 160)], Directory = directory }).ToArray();
        return true;
    }
    private static JsonElement Output(JsonElement value, int depth = 0)
    {
        if (depth > 12) return default;
        if (value.ValueKind == JsonValueKind.String)
        {
            try { using var doc = JsonDocument.Parse(value.GetString()); return Output(doc.RootElement, depth + 1).Clone(); }
            catch (JsonException) { return default; }
        }
        if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray()) { var found = Output(item, depth + 1); if (found.ValueKind == JsonValueKind.Object) return found; }
        }
        if (value.ValueKind != JsonValueKind.Object) return default;
        if (new[] { "childSessionKey", "sessionKey", "session_id", "sessionId" }.Any(k => MspWire.Text(value, k) != null)) return value.Clone();
        var details = Output(MspWire.Field(value, "details"), depth + 1);
        if (details.ValueKind == JsonValueKind.Object) return details;
        foreach (var block in MspWire.Array(MspWire.Field(value, "content")))
        {
            var found = Output(MspWire.Field(block, "text"), depth + 1);
            if (found.ValueKind != JsonValueKind.Object) found = Output(MspWire.Field(block, "content"), depth + 1);
            if (found.ValueKind == JsonValueKind.Object) return found;
        }
        return default;
    }
}
