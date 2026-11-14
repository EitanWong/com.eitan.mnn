using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MNN.Samples.SiriAI.Harnesses
{
    public static class HarnessDesktopPaths
    {
        /// <summary>Resolve saved, conversation and application project candidates in caller-defined order, without creating folders.</summary>
        public static string FindWorkspace(IEnumerable<string> candidates) =>
            (candidates ?? Array.Empty<string>()).Where(path => !string.IsNullOrWhiteSpace(path) && Path.IsPathRooted(path))
                .Where(Directory.Exists).Select(Path.GetFullPath).FirstOrDefault();
        public static string FindRuntime()
        {
            string filename = Path.DirectorySeparatorChar == '\\' ? "dotnet.exe" : "dotnet";
            var directories = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator).Concat(new[]
            {
                Environment.GetEnvironmentVariable("DOTNET_ROOT"), "/usr/local/share/dotnet", "/usr/share/dotnet",
                "/opt/homebrew/opt/dotnet@8/bin", "/opt/homebrew/bin",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet")
            });
            return directories.Where(path => !string.IsNullOrWhiteSpace(path) && Path.IsPathRooted(path))
                .Select(path => Path.Combine(path, filename)).FirstOrDefault(File.Exists);
        }
        public static string FindHost(IEnumerable<string> roots)
        {
            var suffixes = new[] { "SiriAI.HarnessHost.dll", "HarnessHost/SiriAI.HarnessHost.dll",
                "TestArtifacts~/SiriAIHarness/Host/bin/Debug/net8.0/SiriAI.HarnessHost.dll",
                "TestArtifacts~/SiriAIHarness/Host/bin/Release/net8.0/SiriAI.HarnessHost.dll" };
            return (roots ?? Array.Empty<string>()).Where(path => !string.IsNullOrWhiteSpace(path))
                .SelectMany(root => suffixes.Select(suffix => Path.GetFullPath(Path.Combine(root, suffix))))
                .FirstOrDefault(File.Exists);
        }
    }
}
