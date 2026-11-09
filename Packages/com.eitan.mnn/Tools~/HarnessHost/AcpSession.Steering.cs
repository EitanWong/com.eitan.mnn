using System.Text.Json;
using MNN.Samples.SiriAI.Harnesses;

namespace SiriAI.HarnessHost;

internal sealed partial class AcpSession
{
    private readonly object _steeringSync = new();
    private readonly List<AcpInstruction> _instructions = new();
    private AcpInstruction[] _followup = [];
    private TaskCompletionSource<bool> _steeringWake = NewWake();
    private CancellationTokenSource _steeringCancellation;
    private bool _steeringExtension, _steeringPull, _steeringClosed = true, _steeringStopped;
    private static TaskCompletionSource<bool> NewWake() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private void ResetSteering()
    {
        lock (_steeringSync)
        {
            _steeringCancellation?.Dispose(); _steeringCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            _steeringClosed = false; _steeringStopped = false; _steeringWake = NewWake(); _followup = [];
        }
    }
    internal async Task SteerAsync(string text, MNN.Samples.SiriAI.Harnesses.HarnessContentPart[] attachments, long generation, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        var blocks = AcpPromptContent.Create(text, attachments);
        RequirePromptCapabilities(blocks);
        var entry = new AcpInstruction(blocks, new(TaskCreationOptions.RunContinuationsAsynchronously));
        lock (_steeringSync)
        {
            if (_steeringClosed || _steeringStopped || generation != _promptGeneration) throw new InvalidOperationException("ACP task is no longer accepting updates.");
            if (_instructions.Count >= 16) throw new ArgumentException("ACP instruction queue exceeds its budget.");
            Followup(_instructions.Append(entry).ToArray()); // Validate the combined escaped envelope before queue admission.
            _instructions.Add(entry); _steeringWake.TrySetResult(true);
        }
        await entry.Completion.Task.WaitAsync(cancellation);
    }
    private AcpInstruction[] TakeInstructions()
    {
        lock (_steeringSync) { var values = _instructions.ToArray(); _instructions.Clear(); _steeringWake = NewWake(); return values; }
    }
    private static JsonElement[] Followup(AcpInstruction[] entries)
    {
        var prefix = JsonSerializer.SerializeToElement(new { type = "text", text = "用户更新了当前工作的要求。请结合已有上下文和已执行的操作，按以下新要求继续：" });
        var prompt = new[] { prefix }.Concat(entries.SelectMany(e => e.Prompt)).ToArray();
        AcpPromptContent.RequireWireBudget(prompt);
        return prompt;
    }
    internal static void ValidateInstructionContent(string text, MNN.Samples.SiriAI.Harnesses.HarnessContentPart[] attachments) =>
        Followup([new AcpInstruction(AcpPromptContent.Create(text, attachments), null)]);
    private void ConfirmFollowup()
    {
        lock (_steeringSync) { foreach (var e in _followup) e.Completion.TrySetResult(true); _followup = []; }
    }
    private void StopSteering()
    {
        lock (_steeringSync) { _steeringStopped = true; _steeringCancellation?.Cancel(); _steeringWake.TrySetResult(true); }
    }
    private void CloseSteering()
    {
        lock (_steeringSync)
        {
            _steeringClosed = true;
            foreach (var e in _instructions.Concat(_followup)) e.Completion.TrySetException(new IOException("ACP instruction delivery ended without confirmation."));
            _instructions.Clear(); _followup = []; _steeringCancellation?.Cancel();
        }
    }
    private async Task<HarnessOutcome> RunSteerablePromptAsync(JsonElement[] prompt, CancellationToken cancellation)
    {
        AcpInstruction[] inFlight = [];
        try
        {
            _collect = true;
            _promptResponse = CallAsync("session/prompt", new { sessionId = _sessionId, prompt }, _lifetime.Token);
            _promptReady.TrySetResult(true);
            while (true)
            {
                Task wake; lock (_steeringSync) wake = _steeringWake.Task;
                await Task.WhenAny(_promptResponse, wake).WaitAsync(cancellation);
                if (_steeringStopped) return Outcome(await _promptResponse.WaitAsync(TimeSpan.FromSeconds(15), cancellation));
                if (_promptResponse.IsCompleted)
                {
                    var response = await _promptResponse;
                    ConfirmFollowup();
                    lock (_steeringSync)
                    {
                        if (_instructions.Count == 0) { _steeringClosed = true; return Outcome(response); }
                    }
                    if (MspWire.Text(response, "stopReason") is not ("cancelled" or "end_turn")) return Outcome(response);
                    inFlight = TakeInstructions();
                }
                else if (_steeringExtension)
                {
                    inFlight = TakeInstructions();
                    if (inFlight.Length == 0) continue;
                    using var rpc = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _steeringCancellation.Token);
                    rpc.CancelAfter(TimeSpan.FromSeconds(15));
                    JsonElement reply;
                    try
                    {
                        reply = await CallEnvelopeAsync("_session/steering", new { sessionId = _sessionId, prompt = Followup(inFlight),
                            _meta = new { steering = new { idleBehavior = "promptRequired" } } }, rpc.Token, expectedGeneration: _promptGeneration);
                    }
                    catch (OperationCanceledException) when (_steeringStopped)
                    { return Outcome(await _promptResponse.WaitAsync(TimeSpan.FromSeconds(15), cancellation)); }
                    if (reply.TryGetProperty("error", out var nativeError))
                    {
                        if (!nativeError.TryGetProperty("code", out var code) || code.GetInt32() is not (-32601 or -32602)) throw new IOException("Native steering failed after possible execution.");
                        foreach (var e in inFlight) e.Completion.TrySetException(new NotSupportedException("Agent explicitly rejected steering."));
                        inFlight = []; continue;
                    }
                    string outcome = MspWire.Text(MspWire.Field(reply, "result"), "outcome");
                    if (outcome == "injected")
                    {
                        foreach (var e in inFlight) e.Completion.TrySetResult(true);
                        inFlight = []; continue;
                    }
                    if (outcome != "promptRequired") throw new IOException("Native steering outcome is unconfirmed; no instruction was replayed.");
                    var requiredStop = await _promptResponse.WaitAsync(TimeSpan.FromSeconds(15), cancellation);
                    if (MspWire.Text(requiredStop, "stopReason") is not ("cancelled" or "end_turn")) throw new IOException("Native promptRequired did not confirm a restart boundary.");
                    inFlight = inFlight.Concat(TakeInstructions()).ToArray();
                }
                else if (_steeringPull)
                {
                    lock (_steeringSync) _steeringWake = NewWake();
                    continue; // Only the Agent's explicit loop boundary removes these entries.
                }
                else
                {
                    inFlight = TakeInstructions();
                    if (inFlight.Length == 0) continue;
                    await RequestCancelAsync(cancellation, _promptGeneration, forSteering: true);
                    var stopped = await _promptResponse.WaitAsync(TimeSpan.FromSeconds(15), cancellation);
                    if (MspWire.Text(stopped, "stopReason") is not ("cancelled" or "end_turn")) throw new IOException("Native prompt did not confirm a restart boundary.");
                    inFlight = inFlight.Concat(TakeInstructions()).ToArray();
                }
                if (_steeringStopped) return Outcome(await _promptResponse.WaitAsync(TimeSpan.FromSeconds(15), cancellation));
                // The same native Session retains prior actions. Never resend the original objective.
                _turnCancellation.Cancel(); _turnCancellation.Dispose();
                _turnCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
                foreach (var p in _permissions.Values) p.Decision.TrySetCanceled();
                foreach (var p in _inputs.Values) p.Decision.TrySetCanceled();
                lock (_steeringSync) _followup = inFlight;
                prompt = Followup(inFlight); inFlight = [];
                _promptResponse = FollowupPromptAsync(prompt, cancellation);
            }
        }
        finally
        {
            foreach (var e in inFlight) e.Completion.TrySetException(new IOException("ACP instruction outcome is unconfirmed; no retry was sent."));
            CloseSteering();
        }
    }
    private async Task<JsonElement> FollowupPromptAsync(JsonElement[] prompt, CancellationToken cancellation)
    {
        var reply = await CallEnvelopeAsync("session/prompt", new { sessionId = _sessionId, prompt }, cancellation, expectedGeneration: _promptGeneration);
        if (reply.TryGetProperty("error", out _)) throw new IOException("ACP rejected the followup prompt.");
        return reply.GetProperty("result").Clone();
    }
    private async Task DrainInstructionsAsync(JsonElement requestId, JsonElement parameters, long generation, CancellationToken cancellation)
    {
        AcpInstruction[] entries = [];
        try
        {
            lock (_steeringSync)
            {
                if (!_collect || _steeringClosed || _steeringStopped || generation != _promptGeneration || MspWire.Text(parameters, "sessionId") != _sessionId)
                    throw new ArgumentException("Inactive steering scope.");
                _steeringPull = true; entries = TakeInstructions();
            }
            object[] items = entries.Length == 0 ? [] : [new { content = entries.SelectMany(e => e.Prompt).ToArray() }];
            await SendAsync(new { jsonrpc = "2.0", id = requestId, result = new { items, hasQueuedPrompt = false } }, cancellation, generation, requireCurrent: true);
            foreach (var e in entries) e.Completion.TrySetResult(true);
        }
        catch (ArgumentException)
        { await SendAsync(new { jsonrpc = "2.0", id = requestId, error = new { code = -32602, message = "Inactive steering scope." } }, cancellation); }
        catch
        { foreach (var e in entries) e.Completion.TrySetException(new IOException("Native pull delivery is unconfirmed.")); }
    }
    private sealed record AcpInstruction(JsonElement[] Prompt, TaskCompletionSource<bool> Completion);
}
