using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using MNN.Samples.SiriAI.Harnesses;

namespace SiriAI.HarnessHost;

internal sealed partial class AcpSession
{
    private readonly ConcurrentDictionary<string, AcpInput> _inputs = new();
    private readonly HashSet<string> _seenElicitations = new(StringComparer.Ordinal);
    private int _inputAdmissions;
    internal Task RespondInputAsync(string id, string reply, HarnessInputAction action, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        lock (_activitySync)
        {
            if (!_inputs.TryGetValue(id, out var input) || input.Decision.Task.IsCompleted) throw new InvalidOperationException("ACP input request has expired.");
            object response = action switch {
                HarnessInputAction.Answer => input.Answer(reply),
                HarnessInputAction.Decline => new { action = "decline" },
                HarnessInputAction.Cancel => new { action = "cancel" },
                _ => throw new NotSupportedException("ACP input does not support clarification actions.") };
            if (!input.Decision.TrySetResult(response)) throw new InvalidOperationException("ACP input request was already answered.");
        }
        return Task.CompletedTask;
    }
    private async Task InputAsync(JsonElement requestId, JsonElement parameters, CancellationToken cancellation)
    {
        string id = Guid.NewGuid().ToString("N"); bool entered = false;
        int admissions = Interlocked.Increment(ref _inputAdmissions);
        try
        {
            if (admissions > 64) throw new ArgumentException("Pending ACP inputs exceed their budget.");
            // Permissions, forms and external prompts serialize under one public request identity.
            await _permissionGate.WaitAsync(cancellation); entered = true;
            cancellation.ThrowIfCancellationRequested();
            if (!_collect || MspWire.Text(parameters, "sessionId") != _sessionId || parameters.TryGetProperty("requestId", out _))
                throw new ArgumentException("Unrelated or inactive ACP input scope.");
            string message = MspWire.Required(parameters, "message");
            if (message.Length > 4096) throw new ArgumentException("ACP input message exceeds its budget.");
            var input = CreateInput(message, parameters);
            lock (_activitySync)
            {
                if (input.Prompt?.Mode == HarnessInputMode.Url &&
                    (_seenElicitations.Count >= 4096 || !_seenElicitations.Add(input.Prompt.NativeId)))
                    throw new ArgumentException("Reused or excessive native elicitation identity.");
                _inputs[id] = input; PublishActivity(message);
            }
            var result = await input.Decision.Task.WaitAsync(cancellation);
            await SendAsync(new { jsonrpc = "2.0", id = requestId, result }, _lifetime.Token);
        }
        catch (OperationCanceledException) when (!_lifetime.IsCancellationRequested)
        {
            try { await SendAsync(new { jsonrpc = "2.0", id = requestId, result = new { action = "cancel" } }, _lifetime.Token); }
            catch { _lifetime.Cancel(); }
        }
        catch (Exception error) when (!_lifetime.IsCancellationRequested)
        {
            try { await SendAsync(new { jsonrpc = "2.0", id = requestId, error = new { code = error is NotSupportedException ? -32601 : -32602, message = "Unsupported or invalid scoped ACP input." } }, _lifetime.Token); }
            catch { _lifetime.Cancel(); }
        }
        finally
        {
            Interlocked.Decrement(ref _inputAdmissions);
            lock (_activitySync) { _inputs.TryRemove(id, out _); if (entered) PublishActivity("Backend input resolved."); }
            if (entered) _permissionGate.Release();
        }
    }
    private static AcpInput CreateInput(string message, JsonElement parameters)
    {
        string mode = MspWire.Text(parameters, "mode");
        if (mode == "form") return new(message, new AcpInputForm(MspWire.Field(parameters, "requestedSchema")), null, null);
        if (mode == "url") return new(message, null, new HarnessInputPrompt(HarnessInputMode.Url,
            MspWire.Required(parameters, "url"), MspWire.Required(parameters, "elicitationId")), null);
        // Explicit text and omitted mode match the pinned Qwen InputBroker, not standard ACP form semantics.
        if (mode != null && mode != "text") throw new NotSupportedException("ACP input mode was not advertised.");
        var schema = MspWire.Field(parameters, "requestedSchema"); string key = null;
        if (schema.ValueKind is not (JsonValueKind.Undefined or JsonValueKind.Null))
        {
            if (schema.ValueKind != JsonValueKind.Object || Encoding.UTF8.GetByteCount(schema.GetRawText()) > 65536) throw new ArgumentException("Invalid text input schema.");
            var properties = MspWire.Field(schema, "properties");
            if (properties.ValueKind == JsonValueKind.Object && properties.EnumerateObject().Count() == 1)
            {
                key = properties.EnumerateObject().First().Name;
                HarnessText.Required(key, 160);
            }
        }
        return new(message, null, new HarnessInputPrompt(HarnessInputMode.Text), key);
    }
    private void CompleteInput(JsonElement parameters)
    {
        if (parameters.ValueKind != JsonValueKind.Object || !parameters.TryGetProperty("elicitationId", out var id) || id.ValueKind != JsonValueKind.String) return;
        lock (_activitySync)
        {
            if (!_collect) return;
            foreach (var input in _inputs.Values)
                if (!input.Decision.Task.IsCompleted && input.Prompt?.Mode == HarnessInputMode.Url && input.Prompt.NativeId == id.GetString())
                { input.Prompt = input.Prompt.WithCompletion(); PublishActivity(input.Message); return; }
        }
    }
    private sealed class AcpInput
    {
        internal string Message { get; }
        internal AcpInputForm Form { get; }
        internal HarnessInputPrompt Prompt { get; set; }
        private readonly string _textKey;
        internal TaskCompletionSource<object> Decision { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal AcpInput(string message, AcpInputForm form, HarnessInputPrompt prompt, string textKey)
        { Message = message; Form = form; Prompt = prompt; _textKey = textKey; }
        internal object Answer(string reply)
        {
            if (Form != null) return new { action = "accept", content = Form.Answer(reply) };
            HarnessInputPrompt.ReplyText(reply);
            if (Prompt.Mode == HarnessInputMode.Url) return new { action = "accept" };
            string text = reply.Trim();
            var content = new Dictionary<string, string>();
            if (text.Length != 0) content[_textKey ?? "response"] = text;
            return new { action = "accept", content };
        }
    }
}
