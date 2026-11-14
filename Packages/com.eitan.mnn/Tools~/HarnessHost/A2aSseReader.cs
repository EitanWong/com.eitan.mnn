using System.Text;
using System.Text.Json;

namespace SiriAI.HarnessHost;

internal sealed class A2aStreamProtocolException : IOException
{
    internal A2aStreamProtocolException(string text) : base(text) { }
}

/// <summary>Incremental UTF-8 SSE decoder; bounds each line and event independently of total stream duration.</summary>
internal static class A2aSseReader
{
    internal static async IAsyncEnumerable<JsonElement> ReadAsync(Stream input,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellation)
    {
        const int maximum = 1024 * 1024;
        var utf8 = new UTF8Encoding(false, true);
        var line = new MemoryStream();
        var data = new StringBuilder();
        var buffer = new byte[8192];
        string eventType = "message";
        bool skipLf = false, firstLine = true;
        int eventBytes = 0;
        while (true)
        {
            using var idle = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            idle.CancelAfter(TimeSpan.FromSeconds(45));
            int count = await input.ReadAsync(buffer, idle.Token);
            if (count == 0) yield break; // EOF is transport closure, never a task terminal signal.
            for (int i = 0; i < count; i++)
            {
                byte value = buffer[i];
                if (skipLf && value == 10) { skipLf = false; continue; }
                skipLf = value == 13;
                if (value != 10 && value != 13)
                {
                    if (line.Length >= maximum) throw new A2aStreamProtocolException("SSE line exceeds 1 MiB.");
                    line.WriteByte(value);continue;
                }
                string text;
                try { text = utf8.GetString(line.GetBuffer(), 0, (int)line.Length); }
                catch (DecoderFallbackException) { throw new A2aStreamProtocolException("SSE contains invalid UTF-8."); }
                line.SetLength(0);
                if (firstLine) { text = text.TrimStart('\ufeff'); firstLine = false; }
                if (text.Length == 0)
                {
                    if (data.Length > 0)
                    {
                        if (eventType == "error") throw new A2aStreamProtocolException("A2A emitted an SSE error event.");
                        JsonDocument document;
                        try { document = JsonDocument.Parse(data.ToString()); }
                        catch (JsonException) { throw new A2aStreamProtocolException("SSE data is not valid JSON."); }
                        using (document) yield return document.RootElement.Clone();
                    }
                    data.Clear();eventType = "message";eventBytes = 0;continue;
                }
                if (text[0] == ':') continue;
                int colon = text.IndexOf(':');
                string field = colon < 0 ? text : text[..colon];
                string content = colon < 0 ? "" : text[(colon + 1)..];
                if (content.StartsWith(' ')) content = content[1..];
                if (field == "event") eventType = content;
                else if (field == "data")
                {
                    eventBytes += utf8.GetByteCount(content) + 1;
                    if (eventBytes > maximum) throw new A2aStreamProtocolException("SSE event exceeds 1 MiB.");
                    if (data.Length > 0) data.Append('\n');
                    data.Append(content);
                }
            }
        }
    }
}
