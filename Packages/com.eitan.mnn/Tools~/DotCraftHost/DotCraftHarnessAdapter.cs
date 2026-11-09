using System.Collections.Concurrent;
using System.Text.Json;
using DotCraft.Sessions;
using Microsoft.Extensions.AI;
using MNN.Samples.SiriAI.Harnesses;

namespace SiriAI.HarnessHost;

internal sealed class DotCraftCheckpoint
{
    public string Fingerprint { get; set; }
    public string Realm { get; set; }
    public string ThreadId { get; set; }
    public string TurnId { get; set; }
}

/// <summary>Official DotCraft.Harness APIs, compiled only into the separate net10 host.</summary>
internal sealed class DotCraftHarnessAdapter : IRecoverableHarnessAdapter, IHarnessSubmissionValidator,
    IHarnessCancellationOutcomeAdapter, IHarnessInputActionAdapter, IHarnessInstructionAdapter, IAsyncDisposable
{
    private readonly HarnessConfiguration _config;
    private readonly string _fingerprint, _key, _hostDirectory;
    private readonly ConcurrentDictionary<string, Lazy<Task<DotCraftRealm>>> _realms = new();
    private readonly ConcurrentDictionary<string, DotCraftExecution> _active = new();
    public string Id => _config.Id;
    public HarnessCapabilities Capabilities => HarnessCapabilities.Cancel | HarnessCapabilities.Permission |
        HarnessCapabilities.Input | HarnessCapabilities.Update | HarnessCapabilities.Attachments | HarnessCapabilities.UpdateAttachments;
    public string CancellationScope => "dotcraft-turn";

    internal static void Validate(HarnessConfiguration config)
    {
        if (!Uri.TryCreate(config.Endpoint, UriKind.Absolute, out var endpoint) ||
            endpoint.Scheme is not ("http" or "https") || !endpoint.IsLoopback && endpoint.Scheme != "https" ||
            endpoint.UserInfo.Length != 0 || endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0 ||
            string.IsNullOrWhiteSpace(config.Model) || config.Model.Length > 1024 || config.Model.Any(char.IsControl) ||
            config.TimeoutMilliseconds < 0 || config.DotCraftEnabledTools == null || config.DotCraftEnabledTools.Length > 256 ||
            config.DotCraftEnabledTools.Any(t => string.IsNullOrWhiteSpace(t) || t.Length > 128 || t.Any(char.IsControl)) ||
            config.DotCraftModelProtocol is not ("openai-chat-completions" or "openai-responses" or "anthropic"))
            throw new ArgumentException("Invalid DotCraft endpoint, model, protocol, tools or timeout.");
        if (string.IsNullOrWhiteSpace(config.TokenEnvironmentVariable) || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(config.TokenEnvironmentVariable)))
            throw new ArgumentException("DotCraft requires a separate configured model credential.");
        if (config.SystemPrompt != null) throw new NotSupportedException("DotCraft owns its native system instructions; use workspace instructions.");
    }

    internal DotCraftHarnessAdapter(HarnessConfiguration config, string hostDirectory)
    {
        Validate(config); _config = config; _hostDirectory = hostDirectory;
        _key = Environment.GetEnvironmentVariable(config.TokenEnvironmentVariable);
        _fingerprint = MspWire.Hash(new { config.Id, config.Endpoint, config.Model, config.DotCraftModelProtocol, config.DotCraftEnabledTools, config.TokenEnvironmentVariable, config.ResolvedDotCraftMcpServers });
        // Preserve existing session identities when the optional worker remains disabled.
        if (config.DotCraftCodeMode != "off")
            _fingerprint = MspWire.Hash(new { Previous = _fingerprint, config.DotCraftCodeMode });
        if (config.DotCraftPluginFingerprint != null)
            _fingerprint = MspWire.Hash(new { Previous = _fingerprint, config.DotCraftPluginFingerprint });
    }
    private string RealmKey(HarnessTask task) => MspWire.Hash(new { Host = _hostDirectory, _fingerprint, task.OwnerId, task.WorkspaceKey });
    private async Task<DotCraftRealm> RealmAsync(HarnessTask task, CancellationToken stopping)
    {
        string key = RealmKey(task);
        var value = _realms.GetOrAdd(key, _ => new Lazy<Task<DotCraftRealm>>(() =>
            DotCraftRealm.StartAsync(_config, task.WorkspaceKey, ".siriai-dotcraft-" + key[..24], _key, stopping))).Value;
        try { return await value; }
        catch { _realms.TryRemove(key, out _); throw; }
    }
    public void ValidateSubmission(TaskSubmission submission) => Input(submission.Objective, submission.Attachments);
    private static IList<AIContent> Input(string objective, HarnessContentPart[] attachments)
    {
        var result = new List<AIContent> { new TextContent(objective) };
        foreach (var part in attachments)
        {
            if (part.Kind == HarnessContentKind.Text) result.Add(new TextContent(part.Payload));
            else if (part.Kind == HarnessContentKind.Base64 && part.MediaType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true)
                result.Add(new DataContent(Convert.FromBase64String(part.Payload), part.MediaType));
            else throw new NotSupportedException("DotCraft accepts text and inline image inputs; remote content is not downloaded.");
        }
        return result;
    }
    public bool CanRecover(string checkpoint)
    {
        try { var state = JsonSerializer.Deserialize<DotCraftCheckpoint>(checkpoint); return state?.Fingerprint == _fingerprint && !string.IsNullOrWhiteSpace(state.ThreadId); }
        catch (JsonException) { return false; }
    }
    public Task<HarnessOutcome> RunAsync(HarnessTask task, Action<HarnessActivity> activity, CancellationToken stopping) => throw new NotSupportedException("DotCraft requires durable checkpoints.");
    public async Task<HarnessOutcome> RunDurableAsync(HarnessTask task, Action<HarnessActivity> activity, Action<string> checkpoint, CancellationToken stopping)
    {
        var execution = new DotCraftExecution(activity); _active[task.Id] = execution;
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(stopping);
        if (_config.TimeoutMilliseconds > 0) operation.CancelAfter(_config.TimeoutMilliseconds);
        try
        {
            var realm = await RealmAsync(task, operation.Token);
            var state = task.BackendState == null ? null : JsonSerializer.Deserialize<DotCraftCheckpoint>(task.BackendState);
            if (state != null)
            {
                if (state.Fingerprint != _fingerprint || state.Realm != RealmKey(task)) throw new IOException("DotCraft checkpoint configuration changed.");
                var resumed = await realm.Sessions.ResumeThreadAsync(state.ThreadId, operation.Token);
                execution.Bind(realm.Sessions, state.ThreadId);
                var prior = resumed.Turns.FirstOrDefault(t => t.Id == state.TurnId);
                // Official cold loading cancels non-terminal turns. Never re-submit an ambiguous operation.
                return prior?.Status is TurnStatus.Completed or TurnStatus.Failed ? DotCraftExecution.Result(prior) :
                    HarnessOutcome.Interrupted("DotCraft cold recovery stopped the prior turn; inspect its persisted history before submitting new work. No input was replayed.");
            }
            var identity = new SessionIdentity { ChannelName = "siriai", UserId = task.OwnerId, ChannelContext = task.ConversationId, WorkspacePath = task.WorkspaceKey };
            var threads = await realm.Sessions.FindThreadsAsync(identity, ct: operation.Token);
            var found = threads.OrderByDescending(t => t.LastActiveAt).FirstOrDefault();
            var thread = found == null ? await realm.Sessions.CreateThreadAsync(identity, displayName: "SiriAI tasks", ct: operation.Token) :
                await realm.Sessions.ResumeThreadAsync(found.Id, operation.Token);
            execution.Bind(realm.Sessions, thread.Id);
            state = new DotCraftCheckpoint { Fingerprint = _fingerprint, Realm = RealmKey(task), ThreadId = thread.Id };
            checkpoint(JsonSerializer.Serialize(state));
            await foreach (var item in realm.Sessions.SubmitInputAsync(thread.Id, Input(task.Objective, task.Attachments), ct: operation.Token))
            {
                if (item.EventType == SessionEventType.TurnStarted)
                {
                    state.TurnId = item.TurnId; checkpoint(JsonSerializer.Serialize(state)); execution.SetTurn(item.TurnId);
                }
                execution.Accept(item);
            }
            return await execution.NativeOutcomeAsync(operation.Token);
        }
        catch (DotCraftMcpStartupException error) { return HarnessOutcome.Failed(error.Message); }
        catch (DotCraftPluginStartupException error) { return HarnessOutcome.Failed(error.Message); }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) { return await execution.TimeoutAsync(); }
        catch (Exception) { return HarnessOutcome.Interrupted("DotCraft execution outcome is unconfirmed; inspect native history. No input was retried."); }
        finally { execution.Close(); _active.TryRemove(task.Id, out _); }
    }
    private DotCraftExecution Active(HarnessTask task) => _active.TryGetValue(task.Id, out var execution) ? execution : throw new InvalidOperationException("DotCraft task is not active.");
    public Task<HarnessOutcome> CancelWithOutcomeAsync(HarnessTask task, CancellationToken cancellation) => Active(task).CancelAsync(cancellation);
    public async Task<bool> CancelAsync(HarnessTask task, CancellationToken cancellation) => (await CancelWithOutcomeAsync(task, cancellation)).State == HarnessTaskState.Cancelled;
    public Task RespondAsync(HarnessTask task, string requestId, string reply, bool allow, CancellationToken cancellation) => Active(task).RespondAsync(requestId, reply, allow, HarnessInputAction.Answer, cancellation);
    public Task RespondInputAsync(HarnessTask task, string requestId, string reply, HarnessInputAction action, CancellationToken cancellation) => Active(task).RespondAsync(requestId, reply, false, action, cancellation);
    public void ValidateInstruction(HarnessTask task, HarnessInstruction instruction) => Input(instruction.Text, instruction.Attachments);
    public Task UpdateInstructionAsync(HarnessTask task, HarnessInstruction instruction, CancellationToken cancellation) =>
        Active(task).SteerAsync(Input(instruction.Text, instruction.Attachments), cancellation);
    public Task UpdateAsync(HarnessTask task, string instruction, CancellationToken cancellation) => Active(task).SteerAsync(Input(instruction, []), cancellation);
    public async ValueTask DisposeAsync()
    {
        foreach (var value in _realms.Values)
            if (value.IsValueCreated)
            {
                try { await (await value.Value).DisposeAsync(); }
                catch { /* A failed initialization has already disposed its host. */ }
            }
        _realms.Clear();
    }
}
