using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SiriAI.HarnessHost;

internal sealed class HostConfiguration
{
    public string Listen { get; set; } = "http://127.0.0.1:8765";
    public string DataDirectory { get; set; }
    public int MaximumConcurrent { get; set; } = 2;
    public HostIdentity[] Identities { get; set; } = [];
    public Dictionary<string, string> Workspaces { get; set; } = new();
    public HarnessConfiguration[] Harnesses { get; set; } = [];
    public SkillsConfiguration Skills { get; set; }
    public CoordinatorMcpScope[] CoordinatorMcpScopes { get; set; } = [];

    internal string HostId => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(DataDirectory))).ToLowerInvariant();
    internal static void ApplyEnvironment(HostServiceRegistration registration)
    {
        foreach (var pair in registration.ReadCredentials()) Environment.SetEnvironmentVariable(pair.Key, pair.Value);
    }
    internal static HostConfiguration LoadWithEnvironment(HostServiceRegistration registration)
    {
        var values = registration.ReadCredentials();
        var previous = values.Keys.ToDictionary(k => k, Environment.GetEnvironmentVariable);
        try
        {
            foreach (var pair in values) Environment.SetEnvironmentVariable(pair.Key, pair.Value);
            var config = Load(registration.ConfigPath);
            if (config.HostId != registration.HostId || config.Listen != registration.Listen) throw new InvalidDataException("Service configuration identity changed.");
            return config;
        }
        finally { foreach (var pair in previous) Environment.SetEnvironmentVariable(pair.Key, pair.Value); }
    }
    internal static HostConfiguration Load(string path)
    {
        string baseDirectory = Path.GetDirectoryName(Path.GetFullPath(path));
        var config = JsonSerializer.Deserialize<HostConfiguration>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Missing host configuration.");
        if (!Uri.TryCreate(config.Listen, UriKind.Absolute, out var address) ||
            (address.Scheme != "http" && address.Scheme != "https") || address.UserInfo.Length != 0 || address.Query.Length != 0 || address.Fragment.Length != 0 || address.AbsolutePath != "/") throw new ArgumentException("Invalid listen address.");
        if (config.Identities == null || config.Identities.Length == 0 || config.Identities.Any(i => i == null)) throw new ArgumentException("Configure at least one token identity.");
        if (config.Workspaces == null || config.Harnesses == null || config.MaximumConcurrent < 1 || config.MaximumConcurrent > 64)
            throw new ArgumentException("Invalid workspaces, harnesses or concurrency configuration.");
        foreach (var identity in config.Identities) identity.Resolve();
        if (config.Identities.Select(item => item.Secret).Distinct().Count() != config.Identities.Length)
            throw new ArgumentException("Each owner needs a distinct token.");
        foreach (var key in config.Workspaces.Keys.ToArray())
        {
            string directory = Path.GetFullPath(config.Workspaces[key], baseDirectory);
            if (!Directory.Exists(directory)) throw new ArgumentException("A configured workspace does not exist: " + key);
            // Canonicalize final directory symlinks; aliases of one workspace share a scheduling lane.
            directory = new DirectoryInfo(directory).ResolveLinkTarget(true)?.FullName ?? directory;
            config.Workspaces[key] = Path.TrimEndingDirectorySeparator(directory);
        }
        if (string.IsNullOrWhiteSpace(config.DataDirectory)) throw new ArgumentException("Configure a persistent DataDirectory.");
        config.DataDirectory = Path.GetFullPath(config.DataDirectory, baseDirectory);
        if (Directory.Exists(config.DataDirectory)) config.DataDirectory = new DirectoryInfo(config.DataDirectory).ResolveLinkTarget(true)?.FullName ?? config.DataDirectory;
        var hostCredentials = config.Identities.Select(identity => identity.TokenEnvironmentVariable).ToHashSet(StringComparer.Ordinal);
        if (config.Harnesses.Any(h => h == null || string.IsNullOrWhiteSpace(h.Id)) || config.Harnesses.Select(h => h.Id).Distinct(StringComparer.Ordinal).Count() != config.Harnesses.Length)
            throw new ArgumentException("Harness ids must be nonempty and unique.");
        foreach (var harness in config.Harnesses)
        {
            BackendProfiles.Resolve(harness, hostCredentials);
            AcpMcpConfiguration.Resolve(harness, hostCredentials);
            if (harness.DotCraftMcpServers == null || harness.DotCraftMcpServers.Length > 0 && harness.Protocol != "dotcraft")
                throw new ArgumentException("DotCraftMcpServers requires a DotCraft backend.");
            if (harness.DotCraftCodeMode is not ("off" or "on" or "only") ||
                harness.DotCraftCodeMode != "off" && harness.Protocol != "dotcraft")
                throw new ArgumentException("DotCraftCodeMode requires off, on or only and a DotCraft backend.");
            if (harness.DotCraftPluginRoots == null || harness.DotCraftDisabledPlugins == null ||
                harness.Protocol != "dotcraft" && (harness.DotCraftPluginRoots.Length > 0 ||
                    harness.DotCraftDisabledPlugins.Length > 0 || harness.DotCraftPluginAuthorityConfig != null || harness.DotCraftPluginRegistryConfig != null))
                throw new ArgumentException("Plugin configuration requires non-null lists and a DotCraft backend.");
            AcpHarnessAdapter.ValidateStartupWait(harness);
            if (harness.Protocol == "llm") LlmHarnessAdapter.Validate(harness);
            if (harness.Protocol == "msp") MspHarnessAdapter.Validate(harness);
#if DOTCRAFT
            if (harness.Protocol == "dotcraft")
            {
                DotCraftHarnessAdapter.Validate(harness);
                DotCraftMcpConfiguration.Resolve(harness, hostCredentials);
                DotCraftPluginConfiguration.Resolve(harness, baseDirectory, config.Workspaces.Values);
                DotCraftRegistryConfiguration.Resolve(harness, baseDirectory, config.Workspaces.Values);
            }
#endif
        }
        config.Skills?.Resolve(baseDirectory, config.Identities);
        CoordinatorMcpScope.Resolve(config);
        CoordinatorMcpRuns.Validate(config);
        return config;
    }

    internal string Authenticate(string header)
    {
        if (!header.StartsWith("Bearer ", StringComparison.Ordinal)) return null;
        byte[] actual = SHA256.HashData(Encoding.UTF8.GetBytes(header[7..]));
        foreach (var identity in Identities)
            if (CryptographicOperations.FixedTimeEquals(actual, identity.Digest)) return identity.OwnerId;
        return null;
    }
}

internal sealed class HostIdentity
{
    public string OwnerId { get; set; }
    public string TokenEnvironmentVariable { get; set; }
    internal string Secret { get; private set; }
    internal byte[] Digest { get; private set; }
    internal void Resolve()
    {
        if (string.IsNullOrWhiteSpace(OwnerId) || OwnerId.Length > 128 || string.IsNullOrWhiteSpace(TokenEnvironmentVariable))
            throw new ArgumentException("Invalid identity configuration.");
        Secret = Environment.GetEnvironmentVariable(TokenEnvironmentVariable);
        if (Secret == null || Secret.Length < 32 || Secret.Length > 4096) throw new ArgumentException("Identity token must contain 32–4096 characters: " + TokenEnvironmentVariable);
        Digest = SHA256.HashData(Encoding.UTF8.GetBytes(Secret));
    }
}

internal sealed class HarnessConfiguration
{
    public string Id { get; set; }
    public string Backend { get; set; }
    public string PermissionMode { get; set; } = "native";
    public string NativeCommand { get; set; }
    internal Dictionary<string, string> ProcessEnvironment { get; } = new(StringComparer.Ordinal);
    public string Protocol { get; set; } = "acp";
    public string Command { get; set; }
    public string[] Arguments { get; set; }
    public string[] ForwardEnvironment { get; set; } = [];
    public AcpMcpServer[] AcpMcpServers { get; set; } = [];
    public AcpMcpServer[] DotCraftMcpServers { get; set; } = [];
    internal JsonElement ResolvedDotCraftMcpServers { get; set; }
    public string CoordinatorScopeId { get; set; }
    internal JsonElement ResolvedAcpMcpServers { get; set; }
    internal IReadOnlyDictionary<string, AcpMcpProcess> ResolvedAcpMcpProcesses { get; set; } = new Dictionary<string, AcpMcpProcess>();
    // Explicitly supervised loopback Gateway warm-up; default external connections stay direct.
    public int OpenClawStartupWaitMilliseconds { get; set; }
    public string Endpoint { get; set; }
    public string Model { get; set; }
    public string SessionMode { get; set; }
    public Dictionary<string, JsonElement> SessionConfigOptions { get; set; } = new();
    public string SystemPrompt { get; set; }
    public string MspWorkspaceRoot { get; set; }
    public string DotCraftModelProtocol { get; set; } = "openai-chat-completions";
    public string[] DotCraftEnabledTools { get; set; } = [];
    public string DotCraftCodeMode { get; set; } = "off";
    public string[] DotCraftPluginRoots { get; set; } = [];
    public string[] DotCraftDisabledPlugins { get; set; } = [];
    public string DotCraftPluginAuthorityConfig { get; set; }
    public string DotCraftPluginRegistryConfig { get; set; }
    internal string DotCraftPluginFingerprint { get; set; }
    internal Dictionary<string, string> DotCraftPluginRootHashes { get; set; } = new();
    internal Dictionary<string, bool> ResolvedDotCraftPlugins { get; set; } = new();
    public int TimeoutMilliseconds { get; set; } = 300000;
    public string RemoteHarnessId { get; set; }
    public string RemoteWorkspace { get; set; }
    public string TokenEnvironmentVariable { get; set; }
    public int RemoteCapabilities { get; set; }
    public int PollMilliseconds { get; set; } = 1000;
    public bool ReuseContext { get; set; }
    public string AgentCardPath { get; set; } = ".well-known/agent-card.json";
    public string[] AllowedOrigins { get; set; } = [];
}
