using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using MNN.Samples.SiriAI.Harnesses;

namespace SiriAI.HarnessHost;

/// <summary>Only live turn capabilities are mutable here. Child ownership lives in durable submission identities.</summary>
internal sealed class CoordinatorMcpRuns
{
    private readonly ConcurrentDictionary<string, CoordinatorTurn> _turns = new();
    private readonly TaskCompletionSource<PersistentTaskRuntime> _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal Task<PersistentTaskRuntime> Ready => _ready.Task;
    internal CoordinatorMcpRuns(HostConfiguration configuration) { }
    internal void Attach(PersistentTaskRuntime runtime) => _ready.SetResult(runtime);
    internal static void Validate(HostConfiguration configuration)
    {
        foreach (var harness in configuration.Harnesses.Where(h => h.CoordinatorScopeId != null))
        {
            if (harness.Backend == "openclaw")
                throw new ArgumentException("OpenClaw uses native delegation and does not support the HTTP MCP CoordinatorScopeId contract. Remove CoordinatorScopeId and use its native coordinator.");
            var scope = configuration.CoordinatorMcpScopes.SingleOrDefault(s => s.Id == harness.CoordinatorScopeId);
            if (harness.Protocol != "acp" || scope == null || scope.CoordinatorHarnessId != null ||
                configuration.Harnesses.Single(h => h.Id == scope.HarnessId).CoordinatorScopeId != null)
                throw new ArgumentException("A coordinator requires one distinct scope and a non-coordinator child backend.");
            var endpoint = new Uri(new Uri(configuration.Listen), "/mcp/coordinator");
            bool attached = harness.AcpMcpServers.Any(s => s.Type == "http" && Uri.TryCreate(s.Url, UriKind.Absolute, out var url) && url == endpoint &&
                (s.Headers ?? []).Any(h => string.Equals(h.Name, "Authorization", StringComparison.OrdinalIgnoreCase) &&
                    h.ValueEnvironmentVariable == scope.TokenEnvironmentVariable && h.Prefix == "Bearer "));
            if (!attached) throw new ArgumentException("CoordinatorScopeId requires its own local HTTP MCP grant.");
            scope.CoordinatorHarnessId = harness.Id;
        }
        if (configuration.CoordinatorMcpScopes.Any(s => s.BindsParent && s.CoordinatorHarnessId == null))
            throw new ArgumentException("ParentTask conversation binding requires an explicit coordinator harness.");
    }
    internal CoordinatorTurn Begin(CoordinatorRun run, string phase, HarnessContentPart[] inputs = null)
    {
        var turn = new CoordinatorTurn(run, phase, inputs);
        if (!_turns.TryAdd(turn.Id, turn)) throw new InvalidOperationException("Duplicate coordinator turn identity.");
        return turn;
    }
    internal async Task EndAsync(CoordinatorTurn turn, CancellationToken stopping)
    {
        turn.Close();
        if (!_turns.TryRemove(turn.Id, out _)) return;
        await turn.Run.Scope.Tools.WaitAsync(stopping);
        turn.Run.Scope.Tools.Release();
    }
    internal CoordinatorTurn Resolve(CoordinatorMcpScope scope, JsonElement arguments)
    {
        string id = CoordinatorMcpTools.Text(arguments, "turn_id", 64, false);
        if (scope.CoordinatorHarnessId == null)
        {
            if (id != null) throw new UnauthorizedAccessException("Turn is not available in this grant.");
            return null;
        }
        if (id == null || !_turns.TryGetValue(id, out var turn) || turn.Run.Scope != scope)
            throw new UnauthorizedAccessException("Coordinator turn is unavailable.");
        turn.RequireActive(); return turn;
    }
}

internal sealed class CoordinatorRun
{
    internal readonly HarnessTask Parent;
    internal readonly CoordinatorMcpScope Scope;
    internal readonly PersistentTaskRuntime Runtime;
    internal readonly Action<HarnessActivity> Activity;
    internal readonly SemaphoreSlim Controls = new(1, 1);
    internal readonly object Sync = new();
    internal HarnessTask NativeTask;
    internal Task<HarnessOutcome> NativeCompletion;
    internal bool Cancelling, Finalizing, Closed;
    internal Task<HarnessOutcome> Cancellation;
    internal CoordinatorRun(HarnessTask parent, CoordinatorMcpScope scope, PersistentTaskRuntime runtime, Action<HarnessActivity> activity)
    { Parent = parent; Scope = scope; Runtime = runtime; Activity = activity; }
    internal string Prefix => Scope.PrefixFor(Parent.DeliveryConversationId) + Parent.Id + ":";
    internal string ResultConversation => HarnessCollectedResult.ConversationFor(Parent.Id);
    internal HarnessTask[] Children() => Runtime.List(Parent.OwnerId).Where(t => t.SubmissionKey.StartsWith(Prefix, StringComparison.Ordinal) &&
        t.HarnessId == Scope.HarnessId && t.WorkspaceKey == Scope.Workspace && t.ResultConversationId == ResultConversation).ToArray();
    internal HarnessTask[] Targets() => Children().GroupBy(t => t.ConversationId).Select(g => g.OrderByDescending(t => t.CreatedUtcTicks).ThenByDescending(t => t.Id).First()).ToArray();
}

internal sealed class CoordinatorTurn
{
    internal readonly string Id = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
    internal readonly CoordinatorRun Run;
    internal readonly string Phase;
    internal readonly HarnessTask[] Targets;
    internal int UpdatedTargets;
    internal bool Uncertain, Rejected;
    private volatile bool _closed;
    internal readonly HarnessContentPart[] Inputs;
    internal CoordinatorTurn(CoordinatorRun run, string phase, HarnessContentPart[] inputs = null)
    {
        Run = run; Phase = phase; Targets = run.Targets();
        Inputs = (phase == "initial" ? run.Parent.Attachments : phase == "update" ? inputs : null)?.ToArray() ?? [];
    }
    internal void Close() => _closed = true;
    internal void RequireActive()
    {
        if (_closed || Run.Closed || Run.Cancelling || Run.Runtime.Get(Run.Parent.Id, Run.Parent.OwnerId).State != HarnessTaskState.Running)
            throw new InvalidOperationException("Original coordinator turn has ended.");
    }
    internal object[] Views()
    {
        int perTarget = Math.Min(16384, 32768 / Math.Max(1, Targets.Length));
        return Targets.Select(t => Run.Runtime.Get(t.Id, t.OwnerId)).Select(t =>
        {
            int length = Math.Min(t.Result?.Length ?? 0, perTarget);
            return (object)new { session_id = t.ConversationId, delegation_id = t.Id, status = t.State.ToString().ToLowerInvariant(),
                result = t.Result == null ? null : t.Result[..length], result_is_preview = t.Result?.Length > length,
                artifact_count = t.Artifacts?.Length ?? 0 };
        }).ToArray();
    }
}
