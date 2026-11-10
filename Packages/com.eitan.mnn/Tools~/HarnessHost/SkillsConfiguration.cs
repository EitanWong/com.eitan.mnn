namespace SiriAI.HarnessHost;

/// <summary>Host administrator opts in. Request bodies cannot choose executables, environments or package versions.</summary>
internal sealed class SkillsConfiguration
{
    public string Command { get; set; } = "npx";
    public string[] Arguments { get; set; } = [];
    public string Package { get; set; }
    public string[] AllowedOwners { get; set; } = [];
    public string[] ForwardEnvironment { get; set; } = [];
    public string WorkingDirectory { get; set; }
    public int TimeoutMilliseconds { get; set; } = 600000;
    internal void Resolve(string baseDirectory, HostIdentity[] identities)
    {
        if (AllowedOwners == null || AllowedOwners.Length == 0 || AllowedOwners.Any(owner => !identities.Any(i => i.OwnerId == owner)))
            throw new ArgumentException("Skills requires explicitly authorized existing owners.");
        if (Arguments == null || Arguments.Length > 32 || Arguments.Any(v => v == null || v.Length > 4096 || v.Any(char.IsControl)) ||
            ForwardEnvironment == null || ForwardEnvironment.Any(name => string.IsNullOrWhiteSpace(name) || identities.Any(i => i.TokenEnvironmentVariable == name)) ||
            string.IsNullOrWhiteSpace(Command) || Command.Any(char.IsControl) || TimeoutMilliseconds < 100 || TimeoutMilliseconds > 600000)
            throw new ArgumentException("Invalid skills process configuration.");
        Package = string.IsNullOrWhiteSpace(Package) ? Environment.GetEnvironmentVariable("QWEN_AUDIO_AGENT_SKILLS_CLI_PACKAGE")?.Trim() : Package.Trim();
        if (string.IsNullOrEmpty(Package)) Package = "skills@1.5.22";
        if (Package.Length > 4096 || Package[0] == '-' || Package.Any(char.IsControl)) throw new ArgumentException("Invalid skills installer package.");
        WorkingDirectory = Path.GetFullPath(WorkingDirectory ?? baseDirectory, baseDirectory);
        if (!Directory.Exists(WorkingDirectory)) throw new ArgumentException("Skills working directory does not exist.");
        Command = BackendDiscovery.Find(Command) ?? Command;
        if (OperatingSystem.IsWindows() && Path.GetExtension(Command).ToLowerInvariant() is ".cmd" or ".bat")
        {
            string node = BackendDiscovery.Find("node");
            string cli = new[] { Path.GetDirectoryName(Command), node == null ? null : Path.GetDirectoryName(node) }
                .Where(p => p != null && Path.IsPathFullyQualified(p)).Select(p => Path.Combine(p, "node_modules", "npm", "bin", "npx-cli.js")).FirstOrDefault(File.Exists);
            if (node == null || cli == null) throw new ArgumentException("Configure Skills.Command as node.exe and Skills.Arguments with the absolute npm npx-cli.js path.");
            Command = node; Arguments = new[] { cli }.Concat(Arguments).ToArray();
        }
    }
}
