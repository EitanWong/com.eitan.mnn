using MNN.Samples.SiriAI.Harnesses;

namespace SiriAI.HarnessHost;

/// <summary>Inventory only. Finding an executable does not imply authenticated or verified interoperability.</summary>
internal static class BackendDiscovery
{
    internal static HarnessInstallation[] Scan() => BackendProfiles.All.Where(profile => profile.Command != null).Select(profile =>
    {
        string executable = Find((profile.ExecutableVariables ?? []).Select(Environment.GetEnvironmentVariable).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? profile.Command);
        string adapter = profile.Adapter == null ? executable : Find(Environment.GetEnvironmentVariable(profile.AdapterVariable ?? "") ?? profile.Adapter);
        return new HarnessInstallation
        {
            Id = profile.Id, Name = profile.Name, Executable = executable, AdapterExecutable = adapter,
            Arguments = profile.Arguments, Installed = executable != null,
            ProtocolCandidate = profile.Integration != "dotcraft" && profile.Implemented && executable != null && adapter != null,
            Backend = profile.Id, Integration = profile.Integration,
            InteroperabilityVerified = false,
            RequiresExplicitConfiguration = true, MinimumVersion = profile.MinimumVersion
        };
    }).ToArray();

    internal static string Find(string command)
    {
        if (string.IsNullOrWhiteSpace(command) || command.Any(char.IsControl)) return null;
        if (Path.IsPathFullyQualified(command)) return Executable(command) ? Path.GetFullPath(command) : null;
        if (command.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return null;
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var candidates = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator)
            .Concat(new[] { Path.Combine(home, ".local", "bin"), Path.Combine(home, ".npm-global", "bin"),
                Path.Combine(home, ".cargo", "bin"), "/opt/homebrew/bin", "/usr/local/bin",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm") });
        string[] extensions = OperatingSystem.IsWindows()
            ? new[] { "" }.Concat((Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT").Split(';')).ToArray() : [""];
        foreach (string folder in candidates.Where(value => Path.IsPathFullyQualified(value)).Distinct())
        {
            foreach (string extension in extensions)
            {
                string path = Path.Combine(folder, command + extension);
                if (!Executable(path)) continue;
                return Path.GetFullPath(path);
            }
        }
        return null;
    }
    private static bool Executable(string path) => File.Exists(path) && (OperatingSystem.IsWindows() ||
        (File.GetUnixFileMode(path) & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0);
}
