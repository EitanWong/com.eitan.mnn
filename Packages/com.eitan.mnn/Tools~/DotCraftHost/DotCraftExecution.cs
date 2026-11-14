using System.Text.Json;
using DotCraft.Sessions;
using Microsoft.Extensions.AI;
using MNN.Samples.SiriAI.Harnesses;

namespace SiriAI.HarnessHost;

internal sealed class DotCraftExecution
{
    private readonly Action<HarnessActivity> _activity;
    private readonly TaskCompletionSource<string> _turnReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly SemaphoreSlim _control = new(1, 1);
    private readonly object _pending = new();
    private ISessionService _sessions;
    private string _thread, _turn, _requestId;
    private ApprovalRequestPayload _approval;
    private UserInputRequestPayload _input;
    internal DotCraftExecution(Action<HarnessActivity> activity) => _activity = activity;
    internal void Bind(ISessionService sessions, string thread) { _sessions = sessions; _thread = thread; }
    internal void SetTurn(string turn) { _turn = turn; _turnReady.TrySetResult(turn); }
    internal void Close() => _turnReady.TrySetCanceled();
    internal void Accept(SessionEvent item)
    {
        lock (_pending)
        {
            if (item.EventType == SessionEventType.ApprovalRequested && item.ItemPayload?.Payload is ApprovalRequestPayload approval)
            {
                _approval = approval; _input = null; _requestId = Guid.NewGuid().ToString("N");
                Publish(HarnessWaitingFor.Permission, JsonSerializer.Serialize(approval));
            }
            else if (item.EventType == SessionEventType.UserInputRequested && item.ItemPayload?.Payload is UserInputRequestPayload input)
            {
                _input = input; _approval = null; _requestId = Guid.NewGuid().ToString("N");
                Publish(HarnessWaitingFor.Input, "DotCraft needs your input. Choose a suggestion or enter your own answer.", InputForm(input));
            }
            else if (item.EventType == SessionEventType.ItemStarted && _requestId == null)
                Publish(HarnessWaitingFor.None, "DotCraft: " + item.ItemPayload?.Type);
        }
    }
    private void Publish(HarnessWaitingFor waiting, string text, HarnessInputForm form = null)
    {
        if (text.Length > 4096) text = text[..4080] + "… [see history]";
        _activity(new HarnessActivity(waiting, waiting == HarnessWaitingFor.None ? null : _requestId, text, inputForm: form));
    }
    private static HarnessInputForm InputForm(UserInputRequestPayload input) => new()
    {
        Fields = input.Questions.Select(q => new HarnessInputField
        {
            Name = q.Id, Type = "string", Label = q.Header, Description = q.Question,
            Required = true, AllowOther = q.IsOther, Secret = q.IsSecret,
            Choices = q.Options.Select(o => new HarnessInputChoice { Value = o.Label, Label = o.Label, Description = o.Description }).ToArray()
        }).ToArray()
    };
    private static RequestUserInputResponse Answer(UserInputRequestPayload input, string text)
    {
        var values = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (input.Questions.Count == 1 && !text.TrimStart().StartsWith("{", StringComparison.Ordinal))
            values.Add(input.Questions[0].Id, [text]); // Existing single-question clients may still send plain text.
        else values = ReadFormValues(text, input.Questions.Count > 1);
        InputForm(input).EncodeValues(values); // Preserve strict choices for backends that do not permit freeform.
        if (values.Values.Any(v => string.IsNullOrWhiteSpace(v[0]))) throw new ArgumentException("Answer every native question.");
        return new RequestUserInputResponse
        {
            Answers = values.ToDictionary(p => p.Key, p => new RequestUserInputAnswer { Answers = [p.Value[0]] }, StringComparer.Ordinal)
        };
    }
    private static Dictionary<string, string[]> ReadFormValues(string text, bool multiple)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            var fields = document.RootElement;
            if (fields.ValueKind != JsonValueKind.Object) throw new ArgumentException("Answer form fields as a JSON object.");
            bool legacy = multiple && fields.EnumerateObject().Count() == 1 && fields.TryGetProperty("Answers", out _);
            if (legacy) fields = fields.GetProperty("Answers");
            if (fields.ValueKind != JsonValueKind.Object) throw new ArgumentException("Native Answers requires question fields.");
            var values = new Dictionary<string, string[]>(StringComparer.Ordinal);
            foreach (var field in fields.EnumerateObject())
            {
                var value = AnswerValue(field.Value, legacy);
                if (value.ValueKind != JsonValueKind.String || !values.TryAdd(field.Name, [value.GetString()]))
                    throw new ArgumentException("Each form field requires one string answer and may occur only once.");
            }
            return values;
        }
        catch (JsonException error) { throw new ArgumentException("Form answer requires valid JSON.", error); }
    }
    private static JsonElement AnswerValue(JsonElement value, bool legacy)
    {
        if (!legacy) return value;
        if (value.ValueKind != JsonValueKind.Object || value.EnumerateObject().Count() != 1 ||
            !value.TryGetProperty("Answers", out var answers) || answers.ValueKind != JsonValueKind.Array || answers.GetArrayLength() != 1)
            throw new ArgumentException("Each native question requires exactly one answer.");
        return answers[0];
    }
    internal static HarnessOutcome Result(SessionTurn turn)
    {
        if (turn.Status == TurnStatus.Cancelled) return HarnessOutcome.Cancelled("Cancelled by the native DotCraft turn; external tool side effects are not reverted.");
        if (turn.Status == TurnStatus.Failed) return HarnessOutcome.Failed("DotCraft native turn failed; inspect its persisted error and tool history.");
        if (turn.Status != TurnStatus.Completed) return HarnessOutcome.Interrupted("DotCraft native turn has no confirmed terminal outcome.");
        string result = string.Join("\n", turn.Items.Where(i => i.Type == ItemType.AgentMessage).Select(i => (i.Payload as AgentMessagePayload)?.Text).Where(t => !string.IsNullOrWhiteSpace(t)));
        if (result.Length > 65536) return HarnessOutcome.Failed("DotCraft result exceeds the foreground text budget; inspect native history.");
        return HarnessOutcome.Completed(string.IsNullOrWhiteSpace(result) ? "DotCraft completed the turn. Inspect native tool history for details." : result);
    }
    internal async Task<HarnessOutcome> NativeOutcomeAsync(CancellationToken cancellation)
    {
        if (_sessions == null || _turn == null) return HarnessOutcome.Interrupted("DotCraft turn admission is unconfirmed; input was not replayed.");
        var thread = await _sessions.GetThreadAsync(_thread, cancellation);
        var turn = thread.Turns.FirstOrDefault(t => t.Id == _turn);
        return turn == null ? HarnessOutcome.Interrupted("DotCraft native turn is not in the loaded history.") : Result(turn);
    }
    private async Task<HarnessOutcome> WaitTerminalAsync(CancellationToken cancellation)
    {
        while (true)
        {
            var thread = await _sessions.GetThreadAsync(_thread, cancellation);
            var turn = thread.Turns.FirstOrDefault(t => t.Id == _turn);
            if (turn?.Status is TurnStatus.Completed or TurnStatus.Failed or TurnStatus.Cancelled) return Result(turn);
            await Task.Delay(50, cancellation);
        }
    }
    internal async Task<HarnessOutcome> CancelAsync(CancellationToken cancellation)
    {
        await _turnReady.Task.WaitAsync(cancellation);
        await _sessions.CancelTurnAsync(_thread, _turn, cancellation);
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        limit.CancelAfter(TimeSpan.FromSeconds(10));
        try { return await WaitTerminalAsync(limit.Token); }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
        { return HarnessOutcome.Interrupted("DotCraft accepted cancellation but native termination is unconfirmed."); }
    }
    internal async Task<HarnessOutcome> TimeoutAsync()
    {
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            if (_turn == null) return HarnessOutcome.Interrupted("DotCraft timed out before confirmed admission; no input was retried.");
            var result = await CancelAsync(limit.Token);
            return result.State == HarnessTaskState.Cancelled ? HarnessOutcome.Failed("DotCraft timed out and its native turn was cancelled; tool side effects are not reverted.") : result;
        }
        catch { return HarnessOutcome.Interrupted("DotCraft timed out; native stop is unconfirmed. Inspect history before retrying."); }
    }
    internal async Task SteerAsync(IList<AIContent> content, CancellationToken cancellation)
    {
        await _turnReady.Task.WaitAsync(cancellation);
        string identity = await _sessions.SteerTurnAsync(_thread, _turn, content, ct: cancellation);
        if (string.IsNullOrWhiteSpace(identity)) throw new IOException("DotCraft did not confirm the native instruction identity.");
    }
    internal async Task RespondAsync(string requestId, string text, bool allow, HarnessInputAction action, CancellationToken cancellation)
    {
        await _control.WaitAsync(cancellation);
        try
        {
            ApprovalRequestPayload approval;
            UserInputRequestPayload input;
            lock (_pending)
            {
                if (_requestId == null || requestId != _requestId) throw new InvalidOperationException("DotCraft request is no longer current.");
                approval = _approval; input = _input;
            }
            if (approval != null)
                await _sessions.ResolveApprovalAsync(_thread, _turn, approval.RequestId, allow ? SessionApprovalDecision.AcceptOnce : SessionApprovalDecision.Reject, cancellation);
            else if (action == HarnessInputAction.Cancel) await _sessions.CancelTurnAsync(_thread, _turn, cancellation);
            else
            {
                if (action is not (HarnessInputAction.Answer or HarnessInputAction.Decline))
                    throw new NotSupportedException("DotCraft supports input answers, declining a request or turn cancellation.");
                var response = action == HarnessInputAction.Decline ? new RequestUserInputResponse() : Answer(input, text);
                await _sessions.ResolveUserInputRequestAsync(_thread, _turn, input.RequestId, response, cancellation);
            }
            lock (_pending)
            {
                if (_requestId == requestId)
                {
                    _requestId = null; _approval = null; _input = null;
                    Publish(HarnessWaitingFor.None, "DotCraft accepted the foreground reply.");
                }
            }
        }
        finally { _control.Release(); }
    }
}
