using System.Security.Cryptography;
using System.Text.Json;
using MNN.Samples.SiriAI.Harnesses;

namespace SiriAI.HarnessHost;

internal sealed partial class AcpHarnessAdapter
{
    private readonly byte[] _listCursorKey = RandomNumberGenerator.GetBytes(32);
    private readonly SemaphoreSlim _listGate = new(2, 2);
    internal async Task<HarnessNativeSessionPage> ListOwnedSessionsAsync(HarnessTask task, string cursor, CancellationToken cancellation)
    {
        string key = ContextKey(task);
        string nativeCursor = OpenCursor(cursor, task.Id, key);
        var record = _index.Get(key);
        if (record?.SessionId == null) throw new InvalidOperationException("No confirmed native context identity exists for this task.");
        // Bound probe processes independently of live task contexts. Reads never clear an uncertainty fence.
        if (!await _listGate.WaitAsync(0, cancellation)) throw new InvalidOperationException("Native session listing is busy; try again after the current read.");
        try
        {
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            limit.CancelAfter(TimeSpan.FromSeconds(15));
            await using var probe = new AcpSession(_configuration);
            var page = await probe.ListOwnedAsync(task.WorkspaceKey, record.SessionId, nativeCursor, limit.Token);
            page.Uncertain = _index.Get(key)?.Uncertain == true;
            if (page.NextCursor != null) page.NextCursor = SealCursor(page.NextCursor, task.Id, key);
            return page;
        }
        finally { _listGate.Release(); }
    }
    private string SealCursor(string native, string task, string context)
    {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new Cursor(task, context, native, DateTime.UtcNow.AddMinutes(5).Ticks));
        string token = Convert.ToBase64String(payload) + "." + Convert.ToBase64String(HMACSHA256.HashData(_listCursorKey, payload));
        if (token.Length > 16384) throw new IOException("ACP list cursor exceeds its transport budget.");
        return token;
    }
    private string OpenCursor(string cursor, string task, string context)
    {
        if (cursor == null) return null;
        try
        {
            if (cursor.Length > 16384) throw new FormatException();
            var parts = cursor.Split('.');
            if (parts.Length != 2) throw new FormatException();
            byte[] payload = Convert.FromBase64String(parts[0]), signature = Convert.FromBase64String(parts[1]);
            // Reject alternate encodings as well as payload/signature tampering.
            if (Convert.ToBase64String(payload) != parts[0] || Convert.ToBase64String(signature) != parts[1] ||
                !CryptographicOperations.FixedTimeEquals(HMACSHA256.HashData(_listCursorKey, payload), signature)) throw new FormatException();
            var value = JsonSerializer.Deserialize<Cursor>(payload);
            if (value == null || value.Task != task || value.Context != context || value.Expires < DateTime.UtcNow.Ticks ||
                string.IsNullOrEmpty(value.Native) || value.Native.Length > 4096) throw new FormatException();
            return value.Native;
        }
        catch (Exception error) when (error is FormatException or JsonException)
        { throw new ArgumentException("Native list cursor is invalid, expired or belongs to another task/host scope."); }
    }
    private sealed record Cursor(string Task, string Context, string Native, long Expires);
}
