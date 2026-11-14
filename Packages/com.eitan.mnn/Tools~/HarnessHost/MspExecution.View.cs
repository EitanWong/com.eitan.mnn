using System.Text.Json;
using MNN.Samples.SiriAI.Harnesses;

namespace SiriAI.HarnessHost;

internal sealed partial class MspExecution
{
    private bool _viewFilling;
    private string _viewTarget;
    private readonly List<(string Method, JsonElement Parameters)> _viewBuffer = new();
    private readonly HashSet<string> _pagedTwins = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _viewLifetime = new();
    private int _viewBytes;
    private void HoldView(string method, JsonElement parameters)
    {
        _viewBytes += parameters.GetRawText().Length;
        if (_viewBuffer.Count >= 10000 || _viewBytes > 1048576) throw new IOException("MSP view buffer exceeds the host budget.");
        _viewBuffer.Add((method, parameters.Clone()));
    }
    private void BeginViewFill(JsonElement gap)
    {
        _viewTarget = MspWire.Required(gap, "next");
        if (_viewFilling) return;
        _viewFilling = true;
        _ = FillViewAsync(MspWire.Required(gap, "after"));
    }
    private async Task FillViewAsync(string cursor)
    {
        var paged = new List<(string Method, JsonElement Parameters)>();
        var seenCursors = new HashSet<string>(StringComparer.Ordinal);
        int bytes = 0;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_viewLifetime.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            while (true)
            {
                string target;
                lock (_sync) target = _viewTarget;
                var response = await _connection.RequestAsync("view/page", new { sessionId = _session, cursor, limit = 200 }, timeout.Token);
                bool reached = false;
                var events = MspWire.Array(MspWire.Field(response, "events")).ToArray();
                foreach (var entry in events)
                {
                    string method = MspWire.Required(entry, "method");var parameters = MspWire.Field(entry, "params");
                    if (MspWire.Text(parameters, "sessionId") != _session) throw new IOException("MSP view page belongs to another session.");
                    string eventCursor = MspWire.Required(parameters, "viewCursor");
                    reached |= eventCursor == target;
                    bytes += parameters.GetRawText().Length;
                    if (paged.Count >= 10000 || bytes > 1048576) throw new IOException("MSP view fill exceeds the host budget.");
                    if (seenCursors.Add(eventCursor)) paged.Add((method, parameters.Clone()));
                }
                var next = MspWire.Field(response, "nextCursor");
                lock (_sync)
                {
                    if (_released) return;
                    // Opaque cursors are compared only for equality. A second gap extends this walk.
                    if ((reached || next.ValueKind == JsonValueKind.Null) && target == _viewTarget)
                    {
                        foreach (var frame in paged)
                        {
                            string served = MspWire.Text(frame.Parameters, "viewCursor");
                            _pagedTwins.Add(served);Apply(frame.Method, frame.Parameters);
                        }
                        foreach (var frame in _viewBuffer)
                        {
                            string served = MspWire.Text(frame.Parameters, "viewCursor");
                            if (served != null && _pagedTwins.Remove(served)) continue;
                            Apply(frame.Method, frame.Parameters);
                        }
                        _viewBuffer.Clear();_viewBytes = 0;_viewFilling = false;return;
                    }
                }
                string following = next.ValueKind == JsonValueKind.String ? next.GetString() :
                    events.Length > 0 ? MspWire.Text(MspWire.Field(events[^1], "params"), "viewCursor") : null;
                if (events.Length == 0 || string.IsNullOrEmpty(following) || following == cursor) throw new IOException("MSP view paging made no progress.");
                cursor = following;
            }
        }
        catch (Exception) when (!_released)
        {
            End(HarnessOutcome.Interrupted("Muse view recovery was not confirmed; complete output is unavailable. Inspect the native session before retrying."));
        }
        catch (Exception) { }
    }
}
