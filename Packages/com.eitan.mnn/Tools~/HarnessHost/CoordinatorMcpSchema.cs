namespace SiriAI.HarnessHost;

internal static class CoordinatorMcpSchema
{
    private static object Text(int limit) => new { type = "string", minLength = 1, maxLength = limit };
    private static object Input(Dictionary<string, object> properties, params string[] required)
    {
        properties.Add("turn_id", Text(64));
        return new { type = "object", properties, required, additionalProperties = false };
    }
    private static object Tool(string name, string description, object inputSchema, bool readOnly = false) =>
        new { name, description, inputSchema, annotations = new { readOnlyHint = readOnly, openWorldHint = !readOnly } };
    internal static readonly object[] Tools =
    [
        Tool("siriai_sessions_list", "List this grant's durable delegated sessions and latest task; result is a bounded preview. native_session_id is the confirmed native context when available; directory is the original project cwd.",
            Input(new() { ["query"] = Text(256), ["limit"] = new { type = "integer", minimum = 1, maximum = 100 } }), true),
        Tool("siriai_session_start", "Start independent work asynchronously in the configured project. Supply a new request_id for each intent; reuse it only for an exact retry. Return after admission to free the workspace lane. A live initial coordinator turn forwards only that parent's current attached inputs unchanged; tools cannot supply paths or replacement attachments.",
            Input(new() { ["request_id"] = Text(128), ["prompt"] = Text(32768), ["title"] = Text(256) }, "request_id", "prompt")),
        Tool("siriai_session_send", "Continue an idle exact session using its returned managed session_id or confirmed native_session_id. Native IDs resolve only to existing sessions in this grant; they never import backend-global sessions. A busy session rejects ordinary sends. Only the current original run's update turn_id can steer its own child; reuse request_id only for exact retries.",
            Input(new() { ["request_id"] = Text(128), ["session_id"] = Text(4096), ["prompt"] = Text(32768) }, "request_id", "session_id", "prompt")),
        Tool("siriai_session_status", "Get the exact delegation or latest task of the exact session (managed or confirmed native ID). Supply at least one identity; both must refer to the same task/session.",
            Input(new() { ["delegation_id"] = Text(128), ["session_id"] = Text(4096) }), true),
        Tool("siriai_session_cancel", "Request cancellation for the exact delegated task using a managed or confirmed native session ID and report the observed native outcome. Supply at least one exact identity.",
            Input(new() { ["delegation_id"] = Text(128), ["session_id"] = Text(4096) }))
    ];
}
