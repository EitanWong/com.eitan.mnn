using System.Text.Json;
using MNN.Samples.SiriAI.Harnesses;

namespace SiriAI.HarnessHost;

internal sealed partial class SkillLibraryService
{
    private HarnessTask[] ManagementTasks() => _configuration.Identities.Select(i => i.OwnerId).Distinct(StringComparer.Ordinal)
        .SelectMany(owner => _runtime.List(owner)).ToArray();
    private async Task DrainAsync(CancellationToken cancellation)
    {
        while (ManagementTasks().Any(t => !t.IsTerminal)) await Task.Delay(25, cancellation);
    }
    internal async Task SynchronizeStartupAsync(Action<string> report, CancellationToken cancellation)
    {
        var startup = _configuration.Skills?.Startup;
        if (startup == null) return;
        try
        {
            // Recovery may contain a queued removal. Observe its final lock before planning any installs.
            await DrainAsync(cancellation);
            foreach (string backend in _configuration.Harnesses.Where(h => h.Protocol == "acp" && h.Backend != null)
                .Select(h => h.Backend).Distinct(StringComparer.Ordinal))
            {
                string installer = BackendProfiles.Get(backend).SkillsInstaller;
                if (installer == null) continue;
                try { await SynchronizeBackendAsync(startup, backend, installer, report, cancellation); }
                catch (Exception error) when (error is IOException or JsonException or UnauthorizedAccessException)
                { report(JsonSerializer.Serialize(new { Event = "skills.startup.failed", Backend = backend, Message = error.Message })); }
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            _processStopping.Cancel();
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await DrainAsync(cleanup.Token);
            throw;
        }
    }
    private async Task SynchronizeBackendAsync(SkillStartupConfiguration startup, string backend, string installer, Action<string> report, CancellationToken cancellation)
    {
        var snapshot = SkillStartup.Read(startup.HomeDirectory, report);
        var pending = snapshot.Sources.Keys.Where(name => SkillStartup.Missing(startup.HomeDirectory, installer, name)).ToHashSet(StringComparer.Ordinal);
        while (pending.Count > 0)
        {
            cancellation.ThrowIfCancellationRequested();
            snapshot = SkillStartup.Read(startup.HomeDirectory, null);
            var group = snapshot.Sources.Where(p => pending.Contains(p.Key) && SkillStartup.Missing(startup.HomeDirectory, installer, p.Key))
                .GroupBy(p => p.Value, StringComparer.Ordinal).FirstOrDefault();
            if (group == null) break;
            string[] names = group.Take(64).Select(p => p.Key).ToArray();
            pending.ExceptWith(names);
            var task = SubmitCore(startup.OwnerId, new HarnessSkillRequest { SubmissionKey = "startup-" + Guid.NewGuid().ToString("N"),
                Operation = "install", Source = group.Key, Names = names, Backends = [backend] },
                new SkillStartupIntent { HomeDirectory = startup.HomeDirectory, LockHash = snapshot.Hash, OwnerId = startup.OwnerId });
            while (!task.IsTerminal)
            { await Task.Delay(25, cancellation); task = _runtime.Get(task.Id, startup.OwnerId); }
            report(JsonSerializer.Serialize(new { Event = task.State == HarnessTaskState.Completed ? "skills.startup.completed" : "skills.startup.failed",
                Backend = backend, Source = group.Key, Names = names, TaskId = task.Id, State = task.State.ToString(), Message = task.Result,
                Hint = task.State == HarnessTaskState.Completed ? null : "Inspect the source; use skills remove NAME -g -y to remove stale lock entries." }));
        }
    }
}
