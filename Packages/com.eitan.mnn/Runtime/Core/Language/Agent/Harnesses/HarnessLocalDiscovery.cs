using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace MNN.Samples.SiriAI.Harnesses
{
    /// <summary>Read-only local inventory without a running host, Node.js or .NET CLI. Presence is not authentication or protocol proof.</summary>
    public static class HarnessLocalDiscovery
    {
        public static HarnessInstallation[] Scan(IEnumerable<string> searchDirectories = null)
        {
            string[] directories = (searchDirectories ?? DefaultDirectories()).Where(path => !string.IsNullOrWhiteSpace(path) && Path.IsPathRooted(path)).Distinct().ToArray();
            return HarnessBackendCatalog.All.Where(backend => backend.Command != null).Select(backend =>
            {
                string command = backend.ExecutableVariables.Select(Environment.GetEnvironmentVariable).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? backend.Command;
                string executable = Find(command, directories);
                string adapter = backend.Adapter == null ? executable : Find(string.IsNullOrWhiteSpace(backend.AdapterVariable) ? backend.Adapter : Environment.GetEnvironmentVariable(backend.AdapterVariable) ?? backend.Adapter, directories);
                return new HarnessInstallation
                {
                    Id = backend.Id, Backend = backend.Id, Name = backend.Name, Integration = backend.Integration,
                    Executable = executable, AdapterExecutable = adapter, Installed = executable != null,
                    ProtocolCandidate = executable != null && adapter != null,
                    RequiresExplicitConfiguration = true, InteroperabilityVerified = false
                };
            }).ToArray();
        }
        private static IEnumerable<string> DefaultDirectories()
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator).Concat(new[]
            { Path.Combine(home, ".local", "bin"), Path.Combine(home, ".npm-global", "bin"), Path.Combine(home, ".cargo", "bin"),
                "/opt/homebrew/bin", "/usr/local/bin", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm") });
        }
        private static string Find(string command, IEnumerable<string> directories)
        {
            if (string.IsNullOrWhiteSpace(command) || command.Any(char.IsControl)) return null;
            if (Path.IsPathRooted(command)) return File.Exists(command) ? Path.GetFullPath(command) : null;
            if (command.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || command.Contains("/") || command.Contains("\\")) return null;
            string[] extensions = Path.DirectorySeparatorChar == '\\'
                ? new[] { "", ".exe", ".cmd", ".bat" } : new[] { "" };
            return directories.SelectMany(directory => extensions.Select(extension => Path.Combine(directory, command + extension))).FirstOrDefault(File.Exists);
        }
    }
}
