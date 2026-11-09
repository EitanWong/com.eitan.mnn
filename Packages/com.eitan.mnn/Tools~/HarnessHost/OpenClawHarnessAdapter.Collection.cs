using System.Text.Json;
using MNN.Samples.SiriAI.Harnesses;

namespace SiriAI.HarnessHost;

internal sealed partial class OpenClawHarnessAdapter
{
    private sealed record ChildResult(OpenClawChild Child, HarnessTaskState State, string Text);
    private async Task<HarnessOutcome> CollectAsync(Run run)
    {
        run.Activity(new HarnessActivity(HarnessWaitingFor.None, null, "Waiting for exact OpenClaw native child results.", run.Artifacts));
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(run.Stopping);
        if (run.State.DeadlineUtcTicks != 0)
        {
            double remaining = (new DateTime(run.State.DeadlineUtcTicks, DateTimeKind.Utc) - DateTime.UtcNow).TotalMilliseconds;
            if (remaining <= 0) throw new IOException("OpenClaw native result deadline elapsed.");
            limit.CancelAfter(TimeSpan.FromMilliseconds(remaining));
        }
        using var concurrency = new SemaphoreSlim(4, 4);
        var results = await Task.WhenAll(run.State.Children.Select(async child =>
        {
            await concurrency.WaitAsync(limit.Token);
            try { return await WaitChildAsync(run, child, limit.Token); }
            finally { concurrency.Release(); }
        }));
        var artifacts = run.Artifacts.ToList();
        foreach (var result in results)
        {
            string identity = "openclaw-child-" + MspWire.Hash(new { run.Task.Id, result.Child.RunId })[..24];
            string metadata = JsonSerializer.Serialize(new { result.Child.RunId, result.Child.SessionKey, result.Child.Title, result.Child.Directory,
                ParentTaskId = run.Task.Id, State = result.State.ToString(), Result = result.Text });
            artifacts.Add(new HarnessArtifact(identity, "OpenClaw child result", null,
                [new HarnessContentPart(HarnessContentKind.Json, metadata, "application/json", identity + ".json")], run.Task.Id, identity));
        }
        string content = string.Join("\n\n", results.Select(r => r.Child.Title + "\n" + r.Text));
        if (content.Length > 65536) return HarnessOutcome.Failed("OpenClaw child text exceeds the foreground result budget; inspect native sessions.");
        HarnessOutcome outcome = results.Any(r => r.State == HarnessTaskState.Interrupted) ? HarnessOutcome.Interrupted(content)
            : results.Any(r => r.State == HarnessTaskState.Failed) ? HarnessOutcome.Failed(content)
            : results.Any(r => r.State == HarnessTaskState.Cancelled) ? HarnessOutcome.Cancelled(content)
            : HarnessOutcome.Completed(content);
        // Public artifact and full task wire budgets are verified before claiming a completed result.
        var snapshot = run.Task.Copy(); snapshot.Result = content; snapshot.Artifacts = artifacts.ToArray();
        outcome = outcome.WithArtifacts(snapshot.Artifacts);
        if (TaskHostJson.Write(snapshot).Length > 900000) return HarnessOutcome.Failed("OpenClaw result exceeds the foreground wire budget.");
        run.Artifacts = snapshot.Artifacts;
        if (run.Cancelling || results.All(r => r.State != HarnessTaskState.Completed)) return outcome;
        return await SummarizeAsync(run, results, outcome, limit.Token);
    }
    private async Task<ChildResult> WaitChildAsync(Run run, OpenClawChild child, CancellationToken cancellation)
    {
        while (true)
        {
            cancellation.ThrowIfCancellationRequested();
            if (run.Cancelling && !child.AbortRequested)
            {
                lock (run.Sync) { child.AbortRequested = true; run.Save(); }
                // This side effect is journalled before sending and is never replayed after an ambiguous response.
                var aborted = await _gateway.CallAsync("chat.abort", new { runId = child.RunId, sessionKey = child.SessionKey }, 10000, cancellation);
                bool abortConfirmed = MspWire.Field(aborted, "aborted").ValueKind == JsonValueKind.True &&
                    MspWire.Array(MspWire.Field(aborted, "runIds")).Any(i => i.ValueKind == JsonValueKind.String && i.GetString() == child.RunId);
                bool alreadyEnded = MspWire.Field(aborted, "aborted").ValueKind == JsonValueKind.False &&
                    MspWire.Field(aborted, "runIds").ValueKind == JsonValueKind.Array && !MspWire.Array(MspWire.Field(aborted, "runIds")).Any();
                if (!abortConfirmed && !alreadyEnded) return new(child, HarnessTaskState.Interrupted, "Native abort response did not confirm this exact run.");
                if (abortConfirmed) lock (run.Sync) { child.AbortConfirmed = true; run.Save(); }
            }
            JsonElement completion;
            using var polling = CancellationTokenSource.CreateLinkedTokenSource(cancellation,
                run.Cancelling ? CancellationToken.None : run.PollCancellation.Token);
            try { completion = await _gateway.CallAsync("agent.wait", new { runId = child.RunId, timeoutMs = 30000 }, 35000, polling.Token); }
            catch (OperationCanceledException) when (!cancellation.IsCancellationRequested && run.Cancelling && !child.AbortRequested)
            { continue; }
            string observedRun = MspWire.Text(completion, "runId");
            if (observedRun != null && observedRun != child.RunId) throw new IOException("Native completion returned a different run identity.");
            string status = MspWire.Text(completion, "status");
            if (status is "timeout" or "pending")
            {
                if (child.AbortRequested) return new(child, HarnessTaskState.Interrupted, "Native child termination after abort remains unconfirmed; abort was not replayed.");
                if (status == "pending") await Task.Delay(250, cancellation);
                continue;
            }
            if (status == "error" && (child.AbortConfirmed || MspWire.Text(completion, "error") == "aborted"))
                return new(child, HarnessTaskState.Cancelled, "OpenClaw confirmed the exact native run was aborted.");
            if (status is not ("ok" or "completed")) return new(child, child.AbortRequested ? HarnessTaskState.Interrupted : HarnessTaskState.Failed,
                "OpenClaw native run ended without successful completion or a confirmed cancellation outcome.");
            var history = await _gateway.CallAsync("chat.history", new { sessionKey = child.SessionKey, limit = 20 }, 15000, cancellation);
            string observedKey = MspWire.Text(history, "sessionKey");
            if (observedKey != null && observedKey != child.SessionKey) throw new IOException("Native history returned a different session identity.");
            var assistant = MspWire.Array(MspWire.Field(history, "messages")).Reverse().FirstOrDefault(m => MspWire.Text(m, "role") == "assistant");
            var content = MspWire.Field(assistant, "content");
            string text = content.ValueKind == JsonValueKind.Array
                ? string.Join("\n", content.EnumerateArray().Where(p => MspWire.Text(p, "type") == "text").Select(p => MspWire.Text(p, "text") ?? "")).Trim()
                : content.ValueKind == JsonValueKind.String ? content.GetString().Trim() : "";
            if (string.IsNullOrWhiteSpace(text) || text.Length > 65536) return new(child, HarnessTaskState.Failed, "OpenClaw native run completed without bounded assistant text.");
            return new(child, HarnessTaskState.Completed, text);
        }
    }
}
