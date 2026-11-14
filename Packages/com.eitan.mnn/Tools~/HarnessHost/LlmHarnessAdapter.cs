using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MNN.Samples.SiriAI.Harnesses;

namespace SiriAI.HarnessHost;

/// <summary>One stateless Chat Completions request per task. No tools, retries or resumable provider handle.</summary>
internal sealed class LlmHarnessAdapter : ILocalCancellationHarnessAdapter, IAsyncDisposable
{
    private const int MaximumBodyBytes = 1024 * 1024;
    private const int MaximumTextChars = 65536;
    private readonly HarnessConfiguration _configuration;
    private readonly Uri _endpoint;
    private readonly HttpClient _client;
    public string Id => _configuration.Id;
    public HarnessCapabilities Capabilities => HarnessCapabilities.Cancel;
    public string CancellationScope => "http-request";

    internal static (Uri Endpoint, string Token) Validate(HarnessConfiguration configuration)
    {
        var endpoint = new Uri(HarnessHostAddress.Parse(configuration.Endpoint), "chat/completions");
        if (string.IsNullOrWhiteSpace(configuration.Model) || configuration.Model.Length > 1024 || configuration.Model.Any(char.IsControl))
            throw new ArgumentException("LLM requires a nonempty model name of at most 1024 characters.");
        if (configuration.SystemPrompt?.Length > MaximumTextChars || configuration.TimeoutMilliseconds < 0)
            throw new ArgumentException("LLM system prompt exceeds 65536 characters or timeout is negative.");
        string token = Environment.GetEnvironmentVariable(configuration.TokenEnvironmentVariable ?? "");
        if (string.IsNullOrWhiteSpace(token) || token.Length > 4096 || token.Any(char.IsControl))
            throw new ArgumentException("Configure a separate LLM API key environment variable.");
        return (endpoint, token);
    }

    internal LlmHarnessAdapter(HarnessConfiguration configuration)
    {
        _configuration = configuration;
        var validated = Validate(configuration);
        _endpoint = validated.Endpoint;
        _client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }) { Timeout = Timeout.InfiniteTimeSpan };
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", validated.Token);
    }

    public Task<HarnessOutcome> RunAsync(HarnessTask task, Action<HarnessActivity> activity, CancellationToken hostStopping) =>
        RunCancellableAsync(task, activity, CancellationToken.None, hostStopping);

    public async Task<HarnessOutcome> RunCancellableAsync(HarnessTask task, Action<HarnessActivity> activity,
        CancellationToken cancellationRequested, CancellationToken hostStopping)
    {
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(hostStopping, cancellationRequested);
        if (_configuration.TimeoutMilliseconds > 0) operation.CancelAfter(_configuration.TimeoutMilliseconds);
        try
        {
            operation.Token.ThrowIfCancellationRequested();
            activity(new HarnessActivity(HarnessWaitingFor.None, null, "Submitting a text-only LLM request."));
            return await SendAsync(task.Objective, operation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (hostStopping.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) when (cancellationRequested.IsCancellationRequested)
        {
            return HarnessOutcome.Cancelled("Local HTTP request aborted; remote provider cancellation is unknown.");
        }
        catch (OperationCanceledException) { return HarnessOutcome.Failed("LLM request timed out; remote provider outcome is unknown."); }
        catch (JsonException) { return HarnessOutcome.Failed("LLM returned malformed JSON."); }
        catch (HttpRequestException) { return HarnessOutcome.Failed("LLM HTTP request failed; remote provider outcome is unknown."); }
        catch (IOException) { return HarnessOutcome.Failed("LLM response could not be read; remote provider outcome is unknown."); }
    }

    private async Task<HarnessOutcome> SendAsync(string objective, CancellationToken cancellation)
    {
        var messages = new List<object>();
        if (!string.IsNullOrWhiteSpace(_configuration.SystemPrompt)) messages.Add(new { role = "system", content = _configuration.SystemPrompt.Trim() });
        messages.Add(new { role = "user", content = objective });
        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(new { model = _configuration.Model.Trim(), messages, stream = false }), Encoding.UTF8, "application/json")
        };
        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation).ConfigureAwait(false);
        // Provider bodies may contain credentials. Report the numeric status without echoing their body or headers.
        if (!response.IsSuccessStatusCode) return HarnessOutcome.Failed("LLM HTTP status " + (int)response.StatusCode + "; no automatic retry was made.");
        using var body = await response.Content.ReadAsStreamAsync(cancellation).ConfigureAwait(false);
        using var bytes = new MemoryStream();
        var buffer = new byte[8192];
        int count;
        while ((count = await body.ReadAsync(buffer, cancellation).ConfigureAwait(false)) > 0)
        {
            if (bytes.Length + count > MaximumBodyBytes) return HarnessOutcome.Failed("LLM response exceeds the 1048576-byte host budget.");
            bytes.Write(buffer, 0, count);
        }
        using var document = JsonDocument.Parse(bytes.ToArray());
        string text = ResponseText(document.RootElement);
        if (string.IsNullOrWhiteSpace(text)) return HarnessOutcome.Failed("LLM returned no text result; tool calls are not supported by this backend.");
        if (text.Length > MaximumTextChars) return HarnessOutcome.Failed("LLM result exceeds the 65536-character host budget; it was not truncated.");
        return HarnessOutcome.Completed(text);
    }

    private static string ResponseText(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object) return null;
        if (payload.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0 &&
            choices[0].ValueKind == JsonValueKind.Object && choices[0].TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.Object &&
            message.TryGetProperty("content", out var content))
        {
            if (content.ValueKind == JsonValueKind.String) return content.GetString().Trim();
            if (content.ValueKind == JsonValueKind.Array)
                return string.Join("\n", content.EnumerateArray().Select(PartText).Where(text => !string.IsNullOrEmpty(text)));
        }
        return payload.TryGetProperty("output_text", out var fallback) && fallback.ValueKind == JsonValueKind.String ? fallback.GetString().Trim() : null;
    }

    private static string PartText(JsonElement part) => part.ValueKind == JsonValueKind.String ? part.GetString().Trim() :
        part.ValueKind == JsonValueKind.Object && part.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String ? text.GetString().Trim() : null;

    public Task<bool> CancelAsync(HarnessTask task, CancellationToken cancellation) =>
        throw new NotSupportedException("The task runtime must cancel the owned HTTP request token and await its outcome.");
    public Task RespondAsync(HarnessTask task, string requestId, string reply, bool allow, CancellationToken cancellation) =>
        throw new NotSupportedException("LLM requests do not accept input or authorization responses.");
    public Task UpdateAsync(HarnessTask task, string instruction, CancellationToken cancellation) =>
        throw new NotSupportedException("LLM requests do not support steering.");
    public ValueTask DisposeAsync() { _client.Dispose(); return ValueTask.CompletedTask; }
}
