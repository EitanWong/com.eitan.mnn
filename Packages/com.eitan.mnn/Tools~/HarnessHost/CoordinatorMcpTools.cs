using System.Text.Json;
using MNN.Samples.SiriAI.Harnesses;

namespace SiriAI.HarnessHost;

/// <summary>Delegations live in the existing task store; no second mutable session registry.</summary>
internal sealed partial class CoordinatorMcpTools
{
    private readonly PersistentTaskRuntime _runtime;
    private readonly CoordinatorMcpRuns _runs;
    internal CoordinatorMcpTools(PersistentTaskRuntime runtime, CoordinatorMcpRuns runs) { _runtime = runtime; _runs = runs; }
    private HarnessTask[] Tasks(CoordinatorMcpScope scope, CoordinatorTurn turn) => _runtime.List(scope.OwnerId).Where(task =>
        task.SubmissionKey.StartsWith(scope.PrefixFor(turn?.Run.Parent.DeliveryConversationId ?? scope.ConversationId), StringComparison.Ordinal) && task.WorkspaceKey == scope.Workspace &&
        task.HarnessId == scope.HarnessId && (task.ResultConversationId == scope.ConversationId || task.ResultConversationId?.StartsWith("coordination:", StringComparison.Ordinal) == true)).ToArray();
    internal async Task<object> CallAsync(CoordinatorMcpScope scope, string name, JsonElement arguments)
    {
        if (arguments.ValueKind != JsonValueKind.Object) throw new ArgumentException("Tool arguments must be an object.");
        await scope.Tools.WaitAsync();
        try
        {
            var turn = _runs.Resolve(scope, arguments);
            return name switch
            {
                "siriai_sessions_list" => List(scope, arguments, turn),
                "siriai_session_start" => Start(scope, arguments, turn),
                "siriai_session_send" => await SendAsync(scope, arguments, turn),
                "siriai_session_status" => View(scope, turn, Select(scope, arguments, turn)),
                "siriai_session_cancel" => await CancelAsync(scope, arguments, turn),
                _ => throw new ArgumentException("Unknown coordinator tool.")
            };
        }
        finally { scope.Tools.Release(); }
    }
    private object List(CoordinatorMcpScope scope, JsonElement arguments, CoordinatorTurn turn)
    {
        Fields(arguments, "query", "limit");
        string query = Text(arguments, "query", 256, false);
        int limit = 25;
        if (arguments.TryGetProperty("limit", out var requested) && (!requested.TryGetInt32(out limit) || limit < 1 || limit > 100)) throw new ArgumentException("List limit must be 1..100.");
        var tasks = Tasks(scope, turn);
        var latest = tasks.GroupBy(t => t.ConversationId).Select(g => g.OrderByDescending(t => t.CreatedUtcTicks).ThenByDescending(t => t.Id).First());
        if (query != null) latest = latest.Where(t => (t.Title ?? t.Objective).Contains(query, StringComparison.OrdinalIgnoreCase));
        return new { sessions = latest.OrderByDescending(t => t.UpdatedUtcTicks).Take(limit).Select(t => View(scope, turn, t, true, tasks)).ToArray() };
    }
    private object Start(CoordinatorMcpScope scope, JsonElement arguments, CoordinatorTurn turn)
    {
        Fields(arguments, "request_id", "prompt", "title");
        string intent = Text(arguments, "request_id", 128);
        if (turn != null && turn.Phase != "initial") throw new InvalidOperationException("This coordinator phase cannot start independent work.");
        string session = "delegate-" + MspWire.Hash(new { Prefix = turn?.Run.Prefix ?? scope.Prefix, intent });
        return View(scope, turn, Submit(scope, arguments, session, Text(arguments, "title", 256, false), "start", turn));
    }
    private async Task<object> SendAsync(CoordinatorMcpScope scope, JsonElement arguments, CoordinatorTurn turn)
    {
        Fields(arguments, "request_id", "session_id", "prompt");
        string session = ResolveSession(scope, turn, Text(arguments, "session_id", 4096));
        var original = Tasks(scope, turn).Where(t => t.ConversationId == session).OrderBy(t => t.CreatedUtcTicks).FirstOrDefault()
            ?? throw new UnauthorizedAccessException("Delegated session is unavailable in this scope.");
        if (turn?.Phase == "result") throw new InvalidOperationException("Result collection cannot execute work.");
        if (turn?.Phase == "update") return await SteerAsync(turn, arguments, session);
        string key = SubmissionKey(scope, arguments, "send", turn);
        if (!Tasks(scope, turn).Any(t => t.SubmissionKey == key) && Tasks(scope, turn).Any(t => t.ConversationId == session && !t.IsTerminal))
            throw new InvalidOperationException("Session is busy; update the original work or wait for completion.");
        return View(scope, turn, Submit(scope, arguments, session, original.Title, "send", turn));
    }
    private static string SubmissionKey(CoordinatorMcpScope scope, JsonElement arguments, string operation, CoordinatorTurn turn) =>
        (turn?.Run.Prefix ?? scope.Prefix) + MspWire.Hash(new { operation, request = Text(arguments, "request_id", 128) });
    private HarnessTask Submit(CoordinatorMcpScope scope, JsonElement arguments, string session, string title, string operation, CoordinatorTurn turn)
    {
        string key = SubmissionKey(scope, arguments, operation, turn);
        turn?.RequireActive();
        if (turn != null && turn.Run.Children().Length >= 32 && !turn.Run.Children().Any(t => t.SubmissionKey == key))
            throw new InvalidOperationException("Coordinator child capacity reached.");
        return _runtime.Submit(new TaskSubmission(scope.OwnerId, session, key, scope.HarnessId, scope.Workspace,
            Text(arguments, "prompt", 32768), attachments: turn?.Inputs, resultConversationId: turn?.Run.ResultConversation ?? scope.ConversationId, title: title));
    }
    private async Task<object> SteerAsync(CoordinatorTurn turn, JsonElement arguments, string session)
    {
        var target = turn.Targets.SingleOrDefault(t => t.ConversationId == session)
            ?? throw new UnauthorizedAccessException("Update target is not part of the original run.");
        string prompt = Text(arguments, "prompt", 32768);
        string id = MspWire.Hash(new { turn.Run.Prefix, request = Text(arguments, "request_id", 128), operation = "steer" });
        var previous = turn.Run.Children().SingleOrDefault(t => t.Instructions?.Any(i => i.Id == id) == true);
        if (previous != null && previous.ConversationId != session) throw new InvalidOperationException("Instruction identity belongs to another target.");
        target = previous ?? _runtime.Get(target.Id, target.OwnerId);
        if (previous == null && target.IsTerminal)
        {
            var next = Submit(turn.Run.Scope, arguments, session, target.Title, "send", turn);
            turn.UpdatedTargets++; return View(turn.Run.Scope, turn, next);
        }
        turn.RequireActive();
        if (previous == null && target.State != HarnessTaskState.Running) throw new InvalidOperationException("Target is not accepting active-turn updates.");
        if (target.Instructions?.Any(i => i.Id == id && (i.Text != prompt || !HarnessContentPart.SameInputs(i.Attachments, turn.Inputs))) == true) throw new InvalidOperationException("Instruction identity belongs to different text.");
        HarnessInstruction instruction;
        try
        {
            await _runtime.UpdateContentAsync(target.Id, target.OwnerId, prompt, turn.Inputs, CancellationToken.None, id);
            instruction = _runtime.Get(target.Id, target.OwnerId).Instructions.Single(i => i.Id == id);
        }
        catch (Exception) { turn.Uncertain = true; throw new InvalidOperationException("Child delivery outcome is unconfirmed."); }
        if (instruction.State != HarnessInstructionState.Delivered)
        {
            turn.Uncertain |= instruction.State != HarnessInstructionState.Rejected;
            turn.Rejected = true;
            throw new InvalidOperationException("Child instruction delivery was not confirmed.");
        }
        try { turn.RequireActive(); }
        catch (InvalidOperationException) { turn.Uncertain = true; throw; }
        turn.UpdatedTargets++;
        return new { status = "updated", session_id = session, delegation_id = target.Id, native_session_id = NativeSessionId(Tasks(turn.Run.Scope, turn), session) };
    }
    private HarnessTask Select(CoordinatorMcpScope scope, JsonElement arguments, CoordinatorTurn turn)
    {
        Fields(arguments, "delegation_id", "session_id");
        string task = Text(arguments, "delegation_id", 128, false), session = Text(arguments, "session_id", 4096, false);
        if (task == null && session == null) throw new ArgumentException("Provide an exact delegation_id or session_id.");
        if (session != null) session = ResolveSession(scope, turn, session);
        return Tasks(scope, turn).Where(t => (task == null || t.Id == task) && (session == null || t.ConversationId == session))
            .OrderByDescending(t => t.CreatedUtcTicks).ThenByDescending(t => t.Id).FirstOrDefault()
            ?? throw new UnauthorizedAccessException("Delegated task is unavailable in this scope.");
    }
    private async Task<object> CancelAsync(CoordinatorMcpScope scope, JsonElement arguments, CoordinatorTurn turn)
    {
        var task = Select(scope, arguments, turn);
        if (turn != null && (turn.Phase == "result" || !turn.Targets.Any(t => t.Id == task.Id)))
            throw new InvalidOperationException("This turn cannot cancel that work.");
        await _runtime.CancelAsync(task.Id, scope.OwnerId, CancellationToken.None);
        return View(scope, turn, _runtime.Get(task.Id, scope.OwnerId));
    }
    private object View(CoordinatorMcpScope scope, CoordinatorTurn turn, HarnessTask task, bool preview = false, HarnessTask[] knownTasks = null) => new
    {
        session_id = task.ConversationId, native_session_id = NativeSessionId(knownTasks ?? Tasks(scope, turn), task.ConversationId),
        delegation_id = task.Id, title = task.Title, directory = task.WorkspaceKey,
        status = task.State.ToString().ToLowerInvariant(), harness_id = task.HarnessId,
        activity = task.Activity, waiting_for = task.WaitingFor.ToString().ToLowerInvariant(), request_id = task.RequestId,
        result = preview && task.Result?.Length > 512 ? task.Result[..512] : task.Result,
        result_is_preview = preview && task.Result?.Length > 512
    };
    internal static void Fields(JsonElement arguments, params string[] allowed)
    {
        if (arguments.EnumerateObject().Any(p => p.Name != "turn_id" && !allowed.Contains(p.Name, StringComparer.Ordinal))) throw new ArgumentException("Unknown coordinator tool argument.");
    }
    internal static string Text(JsonElement arguments, string name, int limit, bool required = true)
    {
        if (!arguments.TryGetProperty(name, out var value)) return required ? throw new ArgumentException("Missing coordinator argument: " + name) : null;
        if (value.ValueKind != JsonValueKind.String) throw new ArgumentException("Coordinator argument must be a string: " + name);
        string text = value.GetString();
        if (string.IsNullOrWhiteSpace(text) || text.Length > limit || name != "prompt" && text.Any(char.IsControl)) throw new ArgumentException("Invalid coordinator argument: " + name);
        return text;
    }
}
