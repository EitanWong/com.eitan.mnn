using System.Text.Json;
using MNN.Samples.SiriAI.Harnesses;

namespace SiriAI.HarnessHost;

internal sealed partial class AcpSession
{
    // A dedicated initialized connection never attaches a session or accepts historical output.
    internal async Task<HarnessNativeSessionPage> ListOwnedAsync(string workspace, string ownedSession,
        string cursor, CancellationToken cancellation)
    {
        await StartAsync(workspace, cancellation);
        if (!_canList) return new HarnessNativeSessionPage();
        var response = await CallAsync("session/list", new { cwd = workspace, cursor, _meta = new { limit = 100 } }, cancellation);
        var sessions = MspWire.Field(response, "sessions");
        if (sessions.ValueKind != JsonValueKind.Array || sessions.GetArrayLength() > 100)
            throw new IOException("ACP returned an invalid or excessive session list page.");
        var next = OptionalText(response, "nextCursor", 4096);
        if (next == "") next = null;
        if (next != null && next == cursor) throw new IOException("ACP repeated its session list cursor.");
        var matches = new List<HarnessNativeSession>();
        foreach (var session in sessions.EnumerateArray())
        {
            string id = OptionalText(session, "sessionId", 4096), cwd = OptionalText(session, "cwd", 4096);
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(cwd) || !Path.IsPathFullyQualified(cwd)) throw new IOException("ACP returned an invalid session identity.");
            if (id != ownedSession || cwd != workspace) continue;
            if (matches.Count != 0) throw new IOException("ACP returned duplicate native context identities.");
            matches.Add(new HarnessNativeSession { SessionId = id, Cwd = cwd,
                Title = OptionalText(session, "title", 4096), UpdatedAt = OptionalText(session, "updatedAt", 128) });
        }
        return new HarnessNativeSessionPage { Supported = true, Sessions = matches.ToArray(), NextCursor = next };
    }
    private static string OptionalText(JsonElement value, string name, int maximum)
    {
        var text = MspWire.Field(value, name);
        if (text.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return null;
        if (text.ValueKind != JsonValueKind.String) throw new IOException("ACP returned an invalid list field.");
        string result = text.GetString();
        if (result.Length > maximum) throw new IOException("ACP returned an unbounded list field.");
        return result;
    }
}
