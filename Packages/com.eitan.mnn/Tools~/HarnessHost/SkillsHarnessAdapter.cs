using System.Diagnostics;
using System.Text;
using MNN.Samples.SiriAI.Harnesses;

namespace SiriAI.HarnessHost;

internal sealed class SkillsHarnessAdapter : ILocalCancellationHarnessAdapter
{
    private readonly HostConfiguration _host;
    private SkillsConfiguration Configuration => _host.Skills;
    internal SkillsHarnessAdapter(HostConfiguration host) { _host = host; }
    public string Id => "skills";
    public HarnessCapabilities Capabilities => HarnessCapabilities.Cancel;
    public string CancellationScope => "local-installer-process";
    public Task<HarnessOutcome> RunAsync(HarnessTask task, Action<HarnessActivity> activity, CancellationToken stopping) =>
        RunCancellableAsync(task, activity, CancellationToken.None, stopping);
    public async Task<HarnessOutcome> RunCancellableAsync(HarnessTask task, Action<HarnessActivity> activity, CancellationToken cancellation, CancellationToken stopping)
    {
        if (!Configuration.AllowedOwners.Contains(task.OwnerId, StringComparer.Ordinal))
            return HarnessOutcome.Interrupted("Skill owner is no longer authorized; no installer was started.");
        var plan = TaskHostJson.Read<SkillLibraryPlan>(Encoding.UTF8.GetBytes(task.Objective));
        if (plan.Fingerprint != SkillLibraryPlan.Identity(Configuration))
            return HarnessOutcome.Interrupted("Skill installer configuration changed after admission; no process was started.");
        var request = plan.Request;
        string[] arguments = Arguments(request, plan.Installers);
        activity(new HarnessActivity(HarnessWaitingFor.None, null, "Running explicit skill operation: " + request.Operation));
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellation, stopping);
        budget.CancelAfter(Configuration.TimeoutMilliseconds);
        Process process = null;
        try
        {
            var start = StartInfo(arguments);
            process = Process.Start(start) ?? throw new IOException("Installer did not start.");
            process.StandardInput.Close();
            var output = ReadAsync(process.StandardOutput.BaseStream, budget.Token);
            var error = ReadAsync(process.StandardError.BaseStream, budget.Token);
            await Task.WhenAll(output, error, process.WaitForExitAsync(budget.Token));
            string result = "skills CLI exit " + process.ExitCode + "\n" + output.Result + "\n[stderr]\n" + error.Result;
            return process.ExitCode == 0 ? HarnessOutcome.Completed(result) : HarnessOutcome.Failed(result);
        }
        catch (OperationCanceledException)
        {
            if (stopping.IsCancellationRequested) return HarnessOutcome.Interrupted("Host stopped during skill operation; installer side effects are unconfirmed. No automatic replay.");
            return cancellation.IsCancellationRequested ? HarnessOutcome.Cancelled("Stopped the local skill installer; completed filesystem changes may remain.") :
                HarnessOutcome.Failed("Skill installer timed out; filesystem changes may remain. Inspect the installed list before retrying.");
        }
        catch (Exception error) when (error is IOException or DecoderFallbackException or System.ComponentModel.Win32Exception)
        { return HarnessOutcome.Failed("Skill installer could not start or return bounded UTF-8 output; filesystem changes are unconfirmed."); }
        finally
        {
            if (process != null)
            {
                try
                {
                    if (!process.HasExited) process.Kill(true);
                    using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                    await process.WaitForExitAsync(cleanup.Token);
                }
                catch (Exception error) when (error is InvalidOperationException or OperationCanceledException or System.ComponentModel.Win32Exception) { }
                process.Dispose();
            }
        }
    }
    private string[] Arguments(HarnessSkillRequest request, string[] installers)
    {
        var args = new List<string>(Configuration.Arguments) { "-y", Configuration.Package };
        if (request.Operation == "source-list") args.AddRange(["add", request.Source, "--list"]);
        else if (request.Operation == "install")
        {
            args.AddRange(["add", request.Source]);
            foreach (var name in request.Names) args.AddRange(["--skill", name]);
            args.AddRange(["-g", "--copy", "-y"]);
            foreach (var target in installers) args.AddRange(["-a", target]);
        }
        else if (request.Operation == "remove") args.AddRange(["remove", request.Names[0], "-g", "-y"]);
        else args.AddRange([request.Operation, "-g"]);
        return args.ToArray();
    }
    private ProcessStartInfo StartInfo(string[] arguments)
    {
        var start = new ProcessStartInfo(Configuration.Command) { WorkingDirectory = Configuration.WorkingDirectory,
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string value in arguments) start.ArgumentList.Add(value);
        start.Environment.Clear();
        var credentials = _host.Identities.Select(i => i.TokenEnvironmentVariable).ToHashSet(StringComparer.Ordinal);
        foreach (string name in BackendProcessEnvironment.AvailableSystemNames().Concat(Configuration.ForwardEnvironment).Distinct(StringComparer.Ordinal))
            if (!credentials.Contains(name) && Environment.GetEnvironmentVariable(name) is string value) start.Environment[name] = value;
        start.Environment["PWD"] = Configuration.WorkingDirectory;
        return start;
    }
    private static async Task<string> ReadAsync(Stream input, CancellationToken cancellation)
    {
        using var data = new MemoryStream(); var buffer = new byte[4096]; int count;
        while ((count = await input.ReadAsync(buffer, cancellation)) != 0)
        { if (data.Length + count > 16384) throw new IOException("Skill installer output exceeds 16 KiB per stream."); data.Write(buffer, 0, count); }
        return new UTF8Encoding(false, true).GetString(data.ToArray());
    }
    public Task<bool> CancelAsync(HarnessTask task, CancellationToken cancellation) => throw new NotSupportedException("Runtime owns installer cancellation.");
    public Task RespondAsync(HarnessTask task, string id, string text, bool allow, CancellationToken cancellation) => throw new NotSupportedException();
    public Task UpdateAsync(HarnessTask task, string instruction, CancellationToken cancellation) => throw new NotSupportedException();
}
