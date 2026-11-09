using System.Globalization;
using System.Text.Json;
using MNN.Samples.SiriAI.Harnesses;

namespace SiriAI.HarnessHost;

internal sealed partial class AcpSession
{
    private bool _historyMode;
    private string _historyLoadId;
    private HistoryCapture _historyReplay;
    internal async Task<HarnessNativeHistory> LoadHistoryAsync(string workspace, string sessionId,
        Action beforeLoad, CancellationToken cancellation)
    {
        _historyMode = true;
        await StartAsync(workspace, cancellation);
        if (MspWire.Field(_agentCapabilities, "loadSession").ValueKind != JsonValueKind.True)
            return new HarnessNativeHistory { SessionId = sessionId };
        _historyReplay = new HistoryCapture(sessionId);
        _historyLoadId = Interlocked.Increment(ref _nextId).ToString(CultureInfo.InvariantCulture);
        try
        {
            beforeLoad();
            var response = await CallEnvelopeAsync("session/load", new { sessionId, cwd = workspace, mcpServers = Array.Empty<object>() }, cancellation, _historyLoadId);
            if (response.TryGetProperty("error", out _) || MspWire.Field(response, "result").ValueKind != JsonValueKind.Object)
                throw new IOException("ACP did not confirm native history loading.");
            return _historyReplay.Snapshot();
        }
        finally { _historyReplay.Stop(); }
    }
    private bool CaptureHistory(JsonElement parameters)
    {
        if (!_historyMode) return false;
        _historyReplay?.Add(parameters);
        return true;
    }
    private sealed class HistoryCapture
    {
        private readonly object _sync = new();
        private readonly string _sessionId;
        private readonly Queue<HarnessNativeHistoryEntry> _entries = new();
        private int _characters, _received;
        private bool _stopped, _truncated;
        internal HistoryCapture(string sessionId) { _sessionId = sessionId; }
        internal void Stop() { lock (_sync) _stopped = true; }
        internal void Add(JsonElement parameters)
        {
            lock (_sync)
            {
                if (_stopped || MspWire.Text(parameters, "sessionId") != _sessionId) return;
                if (++_received > 10000) throw new IOException("ACP history replay exceeds its notification budget.");
                var update = MspWire.Field(parameters, "update");
                string kind = MspWire.Text(update, "sessionUpdate");
                if (string.IsNullOrWhiteSpace(kind) || kind.Length > 128) throw new IOException("ACP history update has no bounded type.");
                string payload = update.GetRawText(), text = payload;
                if (kind is "user_message_chunk" or "agent_message_chunk" or "agent_thought_chunk")
                {
                    var content = MspWire.Field(update, "content");
                    if (content.ValueKind != JsonValueKind.Object) throw new IOException("ACP historical content is malformed.");
                    if (MspWire.Text(content, "type") == "text")
                    {
                        var value = MspWire.Field(content, "text");
                        if (value.ValueKind != JsonValueKind.String) throw new IOException("ACP historical text is malformed.");
                        text = value.GetString();
                    }
                }
                string messageId = MspWire.Text(update, "messageId");
                if (messageId?.Length > 4096) throw new IOException("ACP historical message identity exceeds its budget.");
                int size = payload.Length + text.Length + (messageId?.Length ?? 0) + kind.Length;
                if (size > 65536) { _truncated = true; return; }
                _entries.Enqueue(new HarnessNativeHistoryEntry { Sequence = _received, Kind = kind, MessageId = messageId, Text = text, Payload = payload });
                _characters += size;
                while (_entries.Count > 256 || _characters > 65536)
                {
                    var old = _entries.Dequeue(); _characters -= old.Payload.Length + old.Text.Length + (old.MessageId?.Length ?? 0) + old.Kind.Length;
                    _truncated = true;
                }
            }
        }
        internal HarnessNativeHistory Snapshot()
        {
            lock (_sync)
            {
                if (!_stopped) throw new IOException("ACP history replay has not reached its native response boundary.");
                return new HarnessNativeHistory { Supported = true, SessionId = _sessionId, Truncated = _truncated,
                    ReceivedEntries = _received, Entries = _entries.ToArray() };
            }
        }
    }
}
