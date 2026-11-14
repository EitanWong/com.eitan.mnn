using System.Text.Json;
using MNN.Samples.SiriAI.Harnesses;

namespace SiriAI.HarnessHost;

/// <summary>One turn owns its events and controls. Session and command acknowledgements never imply terminal success.</summary>
internal sealed partial class MspExecution
{
    private readonly object _sync = new();
    private readonly bool _fullPermission;
    private readonly TaskCompletionSource<bool> _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<HarnessOutcome> _outcome = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<(string Method, JsonElement Parameters)> _held = new();
    private readonly Dictionary<string, JsonElement> _items = new(StringComparer.Ordinal);
    private readonly Dictionary<string, JsonElement> _requests = new(StringComparer.Ordinal);
    private readonly HashSet<string> _resolved = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _controls = new(1, 1);
    private Action<HarnessActivity> _activity;
    private MspConnection _connection;
    private string _session;
    private string _turn;
    private Task<HarnessOutcome> _cancel;
    private int _heldBytes;
    private bool _released;
    private bool _timedOut;
    internal Task<HarnessOutcome> Outcome => _outcome.Task;
    internal MspExecution(bool fullPermission) { _fullPermission = fullPermission; }
    internal void Observe(Action<HarnessActivity> activity) { lock (_sync) _activity = activity; }
    internal void Bind(MspConnection connection, string session) { lock (_sync) { _connection = connection;_session = session; } }
    internal void SetTurn(string turn)
    {
        lock (_sync)
        {
            if (_turn != null && _turn != turn) throw new IOException("MSP turn identity changed after acknowledgement.");
            _turn = turn;_ready.TrySetResult(true);
            foreach (var held in _held)
            {
                if (_viewFilling && held.Method != "view/gap") HoldView(held.Method, held.Parameters);
                else Apply(held.Method, held.Parameters);
            }
            _held.Clear();_heldBytes = 0;
        }
    }
    internal void Accept(string method, JsonElement parameters)
    {
        lock (_sync)
        {
            if (_released || _outcome.Task.IsCompleted) return;
            try
            {
                if (_session != null && MspWire.Text(parameters, "sessionId") != _session) return;
                if (_viewFilling && method != "view/gap") { HoldView(method, parameters);return; }
                string cursor = MspWire.Text(parameters, "viewCursor");
                if (cursor != null && _pagedTwins.Remove(cursor)) return;
                if (_turn == null)
                {
                    _heldBytes += parameters.GetRawText().Length;
                    if (_held.Count >= 512 || _heldBytes > 1048576) throw new IOException("MSP early events exceed their budget.");
                    _held.Add((method, parameters.Clone()));return;
                }
                Apply(method, parameters);
            }
            catch (Exception) { End(HarnessOutcome.Interrupted("Muse MSP view is incomplete or invalid; inspect the native session before retrying.")); }
        }
    }
    private void Apply(string method, JsonElement parameters)
    {
        if (MspWire.Text(parameters, "sessionId") != _session) return;
        string turn = MspWire.Text(parameters, "turnId") ?? MspWire.Text(MspWire.Field(parameters, "item"), "turnId");
        if (turn != null && turn != _turn) return;
        switch (method)
        {
            case "item/started": case "item/updated": case "item/completed": RememberItem(MspWire.Field(parameters, "item"));break;
            // Final item snapshots own result text. Deltas are presentation-only and must not duplicate it.
            case "item/delta": break;
            case "approval/requested": case "approval/updated": RememberApproval(parameters);break;
            case "approval/resolved": RemoveApproval(MspWire.Text(parameters, "approvalId"));break;
            case "userInput/requested": RememberInput(parameters);break;
            case "userInput/settled": RemoveRequest("input:" + MspWire.Text(parameters, "userInputId"));break;
            case "turn/completed":
                if (turn == _turn) Terminal(MspWire.Required(parameters, "terminal"));break;
            case "turn/unqueued":
                if (turn == _turn) End(HarnessOutcome.Cancelled("Muse confirmed that the queued turn was removed."));break;
            case "view/gap":
                BeginViewFill(parameters);break;
            case "turn/retryScheduled": case "turn/modelRetryScheduled":
                _activity?.Invoke(new HarnessActivity(HarnessWaitingFor.None, null, "Muse is retrying its active turn."));break;
        }
    }
    private void RememberItem(JsonElement item)
    {
        if (MspWire.Text(item, "turnId") != _turn) return;
        string id = MspWire.Required(item, "itemId");
        if (_items.TryGetValue(id, out var prior))
        {
            var revision = MspWire.Field(item, "revision");var oldRevision = MspWire.Field(prior, "revision");
            if (revision.ValueKind == JsonValueKind.Number && oldRevision.ValueKind == JsonValueKind.Number && revision.GetInt64() <= oldRevision.GetInt64()) return;
        }
        if (_items.Count >= 10000) throw new IOException("MSP item count exceeds its budget.");
        // Retain only result fields; large tool payloads belong to the native session view.
        _items[id] = JsonSerializer.SerializeToElement(new
        {
            kind = MspWire.Text(item, "kind"), text = MspWire.Text(item, "text"),
            revision = MspWire.Field(item, "revision")
        });
        if (_items.Values.Sum(i => (MspWire.Text(i, "text") ?? "").Length) > 65536) throw new IOException("MSP text result exceeds the host budget.");
        string kind = MspWire.Text(item, "kind");
        if (_requests.Count == 0 && kind is "toolCall" or "userShell" or "reasoning" or "subagent" or "workflow" or "compaction")
            _activity?.Invoke(new HarnessActivity(HarnessWaitingFor.None, null, MspWire.Bounded(MspWire.Text(item, "tool") ?? MspWire.Text(item, "commandText") ?? "Muse " + kind)));
    }
    private static string ApprovalKey(JsonElement request) => "approval:" + MspWire.Required(request, "approvalId") + ":" + MspWire.Field(request, "currentRequirementId").GetRawText();
    private void RememberApproval(JsonElement request)
    {
        string key = ApprovalKey(request);
        if (_resolved.Contains(key)) return;
        // A new stage fences stale UI replies for the earlier stage of the same approval.
        string prefix = "approval:" + MspWire.Required(request, "approvalId") + ":";
        foreach (string old in _requests.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal) && k != key).ToArray()) _requests.Remove(old);
        if (_requests.TryGetValue(key, out var prior))
        {
            if (MspWire.Text(request, "turnId") == null)
            {
                var merged = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(prior.GetRawText());
                foreach (var field in request.EnumerateObject()) merged[field.Name] = field.Value.Clone();
                request = JsonSerializer.SerializeToElement(merged);
            }
            else return;
        }
        _requests[key] = request.Clone();
        if (_fullPermission) _ = AutoApproveAsync(key);
        else PublishNext();
    }
    private void RememberInput(JsonElement request)
    {
        string key = "input:" + MspWire.Required(request, "userInputId");
        if (_resolved.Contains(key) || _requests.ContainsKey(key)) return;
        _requests[key] = request.Clone();PublishNext();
    }
    private void PublishNext()
    {
        if (_requests.Count > 128 || _requests.Values.Sum(r => (long)r.GetRawText().Length) > 1048576)
            throw new IOException("MSP pending requests exceed the host budget.");
        if (_requests.Count == 0 || _released) return;
        var next = _requests.First();
        if (next.Key.StartsWith("approval:", StringComparison.Ordinal))
        {
            var subject = MspWire.Field(next.Value, "subject");
            _activity?.Invoke(new HarnessActivity(HarnessWaitingFor.Permission, PublicRequestId(next.Key), MspWire.Bounded(
                (MspWire.Text(subject, "command") ?? MspWire.Text(subject, "path") ?? MspWire.Text(subject, "kind") ?? "Muse operation") + " — permission required")));
        }
        else
        {
            string prompt = string.Join("\n", MspWire.Array(MspWire.Field(next.Value, "questions")).Select(q =>
                (MspWire.Text(q, "question") ?? "Question") + string.Join("", MspWire.Array(MspWire.Field(q, "options")).Select(o => " [" + MspWire.Text(o, "label") + "]"))));
            _activity?.Invoke(new HarnessActivity(HarnessWaitingFor.Input, PublicRequestId(next.Key), MspWire.Bounded(prompt)));
        }
    }
    private static string PublicRequestId(string key) => MspWire.Hash(key);
    private void RemoveApproval(string id)
    {
        foreach (string key in _requests.Keys.Where(k => k.StartsWith("approval:" + id + ":", StringComparison.Ordinal)).ToArray()) RemoveRequest(key);
    }
    private void RemoveRequest(string key) { _resolved.Add(key);_requests.Remove(key);PublishNext(); }
    private void Terminal(string terminal)
    {
        if (terminal == "cancelled")
        {
            End(_timedOut ? HarnessOutcome.Failed("Muse task timed out; native turn cancellation was confirmed.") : HarnessOutcome.Cancelled("Muse MSP confirmed turn cancellation."));return;
        }
        if (terminal != "completed") { End(HarnessOutcome.Failed("Muse turn ended with a confirmed unsuccessful terminal state."));return; }
        string text = string.Join("\n\n", _items.Values.Where(i => MspWire.Text(i, "kind") == "agentMessage").Select(i => MspWire.Text(i, "text")?.Trim()).Where(t => !string.IsNullOrEmpty(t)));
        End(HarnessOutcome.Completed(string.IsNullOrEmpty(text) ? "Muse Code completed the task." : text));
    }
    internal void Restore(JsonElement read)
    {
        lock (_sync)
        {
            if (MspWire.Text(MspWire.Field(read, "session"), "sessionId") != _session) throw new IOException("MSP history belongs to another session.");
            var history = MspWire.Field(read, "history");
            var items = MspWire.Field(history, "items");
            if (items.ValueKind != JsonValueKind.Array) items = MspWire.Field(MspWire.Field(MspWire.Field(history, "snapshot"), "state"), "items");
            foreach (var item in MspWire.Array(items)) RememberItem(item);
            var last = MspWire.Field(read, "lastTurn");
            if (MspWire.Text(last, "turnId") == _turn) Terminal(MspWire.Required(last, "terminal"));
        }
    }
    internal HarnessOutcome End(HarnessOutcome outcome)
    {
        _outcome.TrySetResult(outcome);_ready.TrySetResult(false);
        return _outcome.Task.GetAwaiter().GetResult();
    }
    internal void TransportLost(MspConnection connection) { lock (_sync) { if (_connection == connection) End(HarnessOutcome.Interrupted("Muse MSP transport was lost; native turn outcome is unknown. Inspect the session before retrying.")); } }
    internal void HostStopping(CancellationToken stopping) { _ready.TrySetCanceled(stopping);_outcome.TrySetCanceled(stopping); }
    internal void ReleaseView() { lock (_sync) { _released = true;_viewLifetime.Cancel();_activity = null;_held.Clear();_items.Clear();_requests.Clear();_viewBuffer.Clear(); } }
    internal async Task RestorePendingAsync(CancellationToken stopping)
    {
        if (Outcome.IsCompleted) return;
        var pending = await _connection.RequestAsync("approval/listPending", new { sessionId = _session }, stopping);
        foreach (var approval in MspWire.Array(MspWire.Field(pending, "approvals"))) Accept("approval/requested", approval);
        foreach (var input in MspWire.Array(MspWire.Field(pending, "userInputs"))) Accept("userInput/requested", input);
    }
    internal Task<HarnessOutcome> CancelAsync(CancellationToken cancellation)
    {
        lock (_sync) return _cancel ??= CancelCoreAsync(cancellation);
    }
    internal async Task<HarnessOutcome> TimeoutAsync(CancellationToken stopping)
    {
        lock (_sync)
        {
            if (Outcome.IsCompleted) return Outcome.GetAwaiter().GetResult();
            _timedOut = true;
            if (!_ready.Task.IsCompletedSuccessfully || !_ready.Task.Result)
                return End(HarnessOutcome.Interrupted("Muse request timed out before its turn identity was confirmed; native outcome is unknown. No unrelated turn was cancelled."));
        }
        using var confirmation = CancellationTokenSource.CreateLinkedTokenSource(stopping);
        confirmation.CancelAfter(TimeSpan.FromSeconds(10));
        try { return await CancelAsync(confirmation.Token); }
        catch (Exception) when (!stopping.IsCancellationRequested)
        { return End(HarnessOutcome.Interrupted("Muse task timed out; native cancellation remains unconfirmed. Inspect its session before retrying.")); }
    }
    private async Task<HarnessOutcome> CancelCoreAsync(CancellationToken cancellation)
    {
        if (!await _ready.Task.WaitAsync(cancellation)) return await Outcome.WaitAsync(cancellation);
        if (Outcome.IsCompleted) return await Outcome;
        string command = MspWire.CommandId();
        var result = await _connection.RequestAsync("turn/cancel", new { commandId = command, sessionId = _session, turnId = _turn }, cancellation);
        ConfirmCommand(result, command);
        if (MspWire.Text(result, "turnId") != _turn) throw new IOException("MSP cancellation acknowledged a different turn.");
        return await Outcome.WaitAsync(cancellation);
    }
    internal async Task SteerAsync(string instruction, CancellationToken cancellation)
    {
        if (!await _ready.Task.WaitAsync(cancellation) || Outcome.IsCompleted) throw new InvalidOperationException("Muse turn is no longer accepting updates.");
        string command = MspWire.CommandId();
        var result = await _connection.RequestAsync("turn/steer", new { commandId = command, sessionId = _session, expectedTurnId = _turn, input = new[] { new { type = "text", text = instruction } } }, cancellation);
        ConfirmCommand(result, command);
        if (MspWire.Text(result, "turnId") != _turn) throw new IOException("MSP steering acknowledged a different turn.");
    }
    private async Task AutoApproveAsync(string key)
    {
        try { await DecideAsync(key, "", true, CancellationToken.None); }
        catch (Exception) { End(HarnessOutcome.Failed("Muse did not offer or confirm the required once-only approval.")); }
    }
    internal Task RespondAsync(string publicId, string reply, bool allow, CancellationToken cancellation, HarnessInputAction action = HarnessInputAction.Answer)
    {
        string key;
        lock (_sync) key = _requests.Keys.FirstOrDefault(k => PublicRequestId(k) == publicId) ?? throw new InvalidOperationException("Muse input or approval stage has expired.");
        return DecideAsync(key, reply, allow, cancellation, action);
    }
    private async Task DecideAsync(string key, string reply, bool allow, CancellationToken cancellation, HarnessInputAction action = HarnessInputAction.Answer)
    {
        await _controls.WaitAsync(cancellation);
        try
        {
            JsonElement request;
            lock (_sync)
            {
                if (!_requests.TryGetValue(key, out request) || Outcome.IsCompleted) throw new InvalidOperationException("Muse request stage has expired.");
            }
            if (key.StartsWith("approval:", StringComparison.Ordinal)) await DecideApprovalAsync(request, reply, allow, cancellation);
            else await AnswerInputAsync(request, reply, action, cancellation);
            lock (_sync) { RemoveRequest(key); }
        }
        finally { _controls.Release(); }
    }
    private async Task DecideApprovalAsync(JsonElement request, string reply, bool allow, CancellationToken cancellation)
    {
        var choices = MspWire.Array(MspWire.Field(request, "availableChoices"));
        var choice = choices.FirstOrDefault(c => MspWire.Text(c, "decision") == (allow ? "approved" : "denied") && MspWire.Text(c, "scope") == "once");
        if (choice.ValueKind == JsonValueKind.Undefined && !allow) choice = choices.FirstOrDefault(c => MspWire.Text(c, "decision") == "abort");
        if (choice.ValueKind == JsonValueKind.Undefined) throw new InvalidOperationException("Muse did not offer the requested once-only approval choice.");
        string command = MspWire.CommandId();
        var parameters = new Dictionary<string, object> { ["commandId"] = command, ["sessionId"] = _session,
            ["approvalId"] = MspWire.Required(request, "approvalId"), ["choiceId"] = MspWire.Required(choice, "choiceId"), ["requirementId"] = MspWire.Field(request, "currentRequirementId") };
        if (MspWire.Field(choice, "acceptsFeedback").ValueKind == JsonValueKind.True && !string.IsNullOrWhiteSpace(reply)) parameters["feedback"] = reply;
        var result = await _connection.RequestAsync("approval/decide", parameters, cancellation);ConfirmCommand(result, command);
    }
    private async Task AnswerInputAsync(JsonElement request, string reply, HarnessInputAction action, CancellationToken cancellation)
    {
        if (action == HarnessInputAction.Decline) throw new NotSupportedException("Muse MSP does not expose an input decline action.");
        string command = MspWire.CommandId();
        var parameters = new Dictionary<string, object> { ["commandId"] = command, ["sessionId"] = _session, ["userInputId"] = MspWire.Required(request, "userInputId") };
        string method;
        if (action == HarnessInputAction.Cancel) { method = "userInput/cancel";parameters["reason"] = reply; }
        else
        {
            var questions = MspWire.Array(MspWire.Field(request, "questions")).ToArray();
            if (questions.Length == 1 && action == HarnessInputAction.Answer)
            {
                var answer = new Dictionary<string, object> { ["questionId"] = MspWire.Required(questions[0], "id") };
                bool selected = MspWire.Array(MspWire.Field(questions[0], "options")).Any(o => MspWire.Text(o, "label") == reply);
                answer[selected ? "selectedLabel" : "freeText"] = reply;
                if (selected && MspWire.Text(MspWire.Field(questions[0], "selection"), "mode") == "multiple") { answer.Remove("selectedLabel");answer["selectedLabels"] = new[] { reply }; }
                method = "userInput/answer";parameters["answers"] = new[] { answer };
            }
            else { method = "userInput/clarify";parameters["clarification"] = new { format = "text", content = reply }; }
        }
        var result = await _connection.RequestAsync(method, parameters, cancellation);ConfirmCommand(result, command);
    }
    private static void ConfirmCommand(JsonElement result, string command)
    {
        if (MspWire.Text(result, "commandId") != command || MspWire.Text(result, "status") is not ("accepted" or "noop")) throw new IOException("MSP did not confirm the requested command identity.");
    }
}
