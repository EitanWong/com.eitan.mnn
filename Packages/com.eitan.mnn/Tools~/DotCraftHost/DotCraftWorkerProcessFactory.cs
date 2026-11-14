using System.Diagnostics;
using DotCraft.Processes;

namespace SiriAI.HarnessHost;

/// <summary>Uses the current runtime for official script workers and excludes service/model credentials.</summary>
internal sealed class DotCraftWorkerProcessFactory : IManagedChildProcessFactory
{
    public ManagedChildProcess Start(ProcessStartInfo startInfo)
    {
        if (startInfo.ArgumentList.Count >= 2 &&
            startInfo.ArgumentList[^2] == "script-worker" && startInfo.ArgumentList[^1] == "code-mode")
        {
            // The official launcher uses bare dotnet for DLL hosts; PATH may select a different SDK.
            if (string.Equals(startInfo.FileName, "dotnet", StringComparison.OrdinalIgnoreCase))
                startInfo.FileName = Environment.ProcessPath ?? throw new InvalidOperationException("Current DotCraft runtime path is unavailable.");
            var systemNames = BackendProcessEnvironment.AvailableSystemNames().ToHashSet(StringComparer.Ordinal);
            foreach (var name in startInfo.Environment.Keys.ToArray())
                if (!systemNames.Contains(name)) startInfo.Environment.Remove(name);
        }
        return ManagedChildProcess.Start(startInfo);
    }
}
