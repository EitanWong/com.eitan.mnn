using System.Text.RegularExpressions;
using MNN.Samples.SiriAI.Harnesses;

namespace SiriAI.HarnessHost;

/// <summary>One source for launch defaults, discovery and backend environment policy; no installation side effects.</summary>
internal sealed record BackendProfile(string Id, string Name, string Command, string Adapter, string[] Arguments,
    string[] EnvironmentNames, string[] EnvironmentPrefixes, bool FullPermission = true, bool AlwaysFull = false,
    string Integration = "acp", bool Implemented = true, string[] ExecutableVariables = null, string AdapterVariable = null, string MinimumVersion = null, string SkillsInstaller = null);

internal static class BackendProfiles
{
#if DOTCRAFT
    private const bool DotCraftImplemented = true;
#else
    private const bool DotCraftImplemented = false;
#endif
    internal static readonly BackendProfile[] All =
    [
        new("qwen", HarnessBackendCatalog.Get("qwen").Name, HarnessBackendCatalog.Get("qwen").Command, HarnessBackendCatalog.Get("qwen").Adapter, ["--acp"], ["DASHSCOPE_API_KEY", "OPENAI_API_KEY", "OPENAI_BASE_URL"], ["QWEN_CODE_"], ExecutableVariables: HarnessBackendCatalog.Get("qwen").ExecutableVariables.ToArray(), MinimumVersion: "0.21.6", SkillsInstaller: "qwen-code"),
        new("qoder", HarnessBackendCatalog.Get("qoder").Name, HarnessBackendCatalog.Get("qoder").Command, HarnessBackendCatalog.Get("qoder").Adapter, ["--acp"], [], ["QODER_", "QODERCLI_"], ExecutableVariables: HarnessBackendCatalog.Get("qoder").ExecutableVariables.ToArray(), SkillsInstaller: "qoder"),
        new("opencode", HarnessBackendCatalog.Get("opencode").Name, HarnessBackendCatalog.Get("opencode").Command, HarnessBackendCatalog.Get("opencode").Adapter, ["acp"], ["DASHSCOPE_API_KEY"], ["OPENCODE_"], ExecutableVariables: HarnessBackendCatalog.Get("opencode").ExecutableVariables.ToArray(), MinimumVersion: "1.18.0", SkillsInstaller: "opencode"),
        new("minimax", HarnessBackendCatalog.Get("minimax").Name, HarnessBackendCatalog.Get("minimax").Command, HarnessBackendCatalog.Get("minimax").Adapter, ["acp"], [], ["MINIMAX_"], ExecutableVariables: HarnessBackendCatalog.Get("minimax").ExecutableVariables.ToArray(), MinimumVersion: "0.3.7"),
        new("kimi", HarnessBackendCatalog.Get("kimi").Name, HarnessBackendCatalog.Get("kimi").Command, HarnessBackendCatalog.Get("kimi").Adapter, ["acp"], [], ["KIMI_"], ExecutableVariables: HarnessBackendCatalog.Get("kimi").ExecutableVariables.ToArray(), MinimumVersion: "0.31.0", SkillsInstaller: "kimi-code-cli"),
        new("hermes", HarnessBackendCatalog.Get("hermes").Name, HarnessBackendCatalog.Get("hermes").Command, HarnessBackendCatalog.Get("hermes").Adapter, ["acp", "--accept-hooks"], [], ["HERMES_"], ExecutableVariables: HarnessBackendCatalog.Get("hermes").ExecutableVariables.ToArray(), SkillsInstaller: "hermes-agent"),
        new("codebuddy", HarnessBackendCatalog.Get("codebuddy").Name, HarnessBackendCatalog.Get("codebuddy").Command, HarnessBackendCatalog.Get("codebuddy").Adapter, ["--acp"], [], ["CODEBUDDY_"], ExecutableVariables: HarnessBackendCatalog.Get("codebuddy").ExecutableVariables.ToArray(), SkillsInstaller: "codebuddy"),
        new("deepseek", HarnessBackendCatalog.Get("deepseek").Name, HarnessBackendCatalog.Get("deepseek").Command, HarnessBackendCatalog.Get("deepseek").Adapter, ["--profile", "acp"], ["DEEPSEEK_API_KEY"], ["DEEPSEEK_", "DSH_"], ExecutableVariables: HarnessBackendCatalog.Get("deepseek").ExecutableVariables.ToArray(), MinimumVersion: "0.1.5"),
        new("codex", HarnessBackendCatalog.Get("codex").Name, HarnessBackendCatalog.Get("codex").Command, HarnessBackendCatalog.Get("codex").Adapter, [], ["DASHSCOPE_API_KEY", "OPENAI_API_KEY", "OPENAI_BASE_URL"], ["CODEX_"], ExecutableVariables: HarnessBackendCatalog.Get("codex").ExecutableVariables.ToArray(), AdapterVariable: HarnessBackendCatalog.Get("codex").AdapterVariable, SkillsInstaller: "codex"),
        new("claude", HarnessBackendCatalog.Get("claude").Name, HarnessBackendCatalog.Get("claude").Command, HarnessBackendCatalog.Get("claude").Adapter, [], ["ANTHROPIC_API_KEY", "ANTHROPIC_BASE_URL", "CLAUDE_API_KEY"], ["CLAUDE_"], ExecutableVariables: HarnessBackendCatalog.Get("claude").ExecutableVariables.ToArray(), AdapterVariable: HarnessBackendCatalog.Get("claude").AdapterVariable, SkillsInstaller: "claude-code"),
        new("pi", HarnessBackendCatalog.Get("pi").Name, HarnessBackendCatalog.Get("pi").Command, HarnessBackendCatalog.Get("pi").Adapter, [], ["ANTHROPIC_API_KEY", "ANTHROPIC_BASE_URL", "OPENAI_API_KEY", "OPENAI_BASE_URL", "GEMINI_API_KEY", "GOOGLE_API_KEY"], ["PI_"], AlwaysFull: true, ExecutableVariables: HarnessBackendCatalog.Get("pi").ExecutableVariables.ToArray(), AdapterVariable: HarnessBackendCatalog.Get("pi").AdapterVariable, MinimumVersion: "0.80.4", SkillsInstaller: "pi"),
        new("openclaw", HarnessBackendCatalog.Get("openclaw").Name, HarnessBackendCatalog.Get("openclaw").Command, HarnessBackendCatalog.Get("openclaw").Adapter, ["acp"], ["DASHSCOPE_API_KEY", "AGENT_API_KEY"], ["OPENCLAW_"], FullPermission: false, ExecutableVariables: HarnessBackendCatalog.Get("openclaw").ExecutableVariables.ToArray(), SkillsInstaller: "openclaw"),
        new("acp", "ACP Agent", null, null, [], [], ["ACP_"], FullPermission: false, ExecutableVariables: ["ACP_COMMAND"]),
        new("a2a", "A2A Agent", null, null, [], [], [], FullPermission: false, Integration: "a2a"),
        new("http", "SiriAI HTTP v1", null, null, [], [], [], FullPermission: false, Integration: "http"),
        new("muse", HarnessBackendCatalog.Get("muse").Name, HarnessBackendCatalog.Get("muse").Command, HarnessBackendCatalog.Get("muse").Adapter, ["serve"], ["META_API_KEY"], ["MUSE_"], Integration: "msp", ExecutableVariables: HarnessBackendCatalog.Get("muse").ExecutableVariables.ToArray()),
        new("llm", "LLM", null, null, [], [], [], FullPermission: false, Integration: "llm"),
        new("dotcraft", "DotCraft", "dotcraft", null, [], [], [], FullPermission: false, Integration: "dotcraft", Implemented: DotCraftImplemented)
    ];
    internal static BackendProfile Get(string id) => All.FirstOrDefault(p => p.Id == id)
        ?? throw new ArgumentException("Unknown backend profile: " + id);

    internal static void Resolve(HarnessConfiguration configuration, ISet<string> hostCredentials)
    {
        if (string.IsNullOrWhiteSpace(configuration.Backend))
        {
            configuration.Arguments ??= [];
            configuration.ForwardEnvironment ??= [];
            if (configuration.PermissionMode != "native") throw new ArgumentException("Select a named backend for a permission override.");
            RejectHostCredentials(configuration, hostCredentials);
            return;
        }
        var profile = Get(configuration.Backend);
        if (!profile.Implemented) throw new NotSupportedException(profile.Name + " requires its " + profile.Integration + " adapter; it is not implemented by the ACP client.");
        if (configuration.PermissionMode != "native" && configuration.PermissionMode != "full") throw new ArgumentException("PermissionMode must be native or full.");
        if (configuration.PermissionMode == "full" && !profile.FullPermission) throw new ArgumentException(profile.Name + " does not expose a unified full permission mode.");
        configuration.Protocol = profile.Integration;
        if (profile.AlwaysFull) configuration.PermissionMode = "full";
        if (profile.Integration is not ("acp" or "msp")) { RejectHostCredentials(configuration, hostCredentials); return; }
        var explicitNames = configuration.ForwardEnvironment ?? [];
        if (explicitNames.Any(hostCredentials.Contains)) throw new ArgumentException("Host authentication variables cannot be forwarded to a backend.");
        configuration.ForwardEnvironment = Environment.GetEnvironmentVariables().Keys.Cast<string>()
            .Where(name => profile.EnvironmentNames.Contains(name) || profile.EnvironmentPrefixes.Any(name.StartsWith))
            .Concat(explicitNames).Where(name => !hostCredentials.Contains(name)).Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        if (configuration.ForwardEnvironment.Any(name => !Regex.IsMatch(name, "^[A-Za-z_][A-Za-z0-9_]{0,127}$"))) throw new ArgumentException("Invalid backend environment variable name.");
        ResolveLaunch(configuration, profile);
        RejectHostCredentials(configuration, hostCredentials);
    }
    private static void RejectHostCredentials(HarnessConfiguration configuration, ISet<string> hostCredentials)
    {
        if ((configuration.ForwardEnvironment ?? []).Any(hostCredentials.Contains) ||
            configuration.Protocol is "acp" or "msp" or "llm" or "dotcraft" && configuration.TokenEnvironmentVariable != null && hostCredentials.Contains(configuration.TokenEnvironmentVariable))
            throw new ArgumentException("Backend credentials must be separate from host authentication credentials.");
    }
    private static string SelectedExecutable(string explicitValue, string[] variables, string fallback)
    {
        string chosen = !string.IsNullOrWhiteSpace(explicitValue) ? explicitValue : (variables ?? []).Select(Environment.GetEnvironmentVariable).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? fallback;
        return BackendDiscovery.Find(chosen) ?? throw new FileNotFoundException("Required backend executable is not installed: " + (fallback ?? "ACP_COMMAND"));
    }
    private static void ResolveLaunch(HarnessConfiguration configuration, BackendProfile profile)
    {
        if (profile.Adapter == null)
            configuration.Command = SelectedExecutable(configuration.Command, profile.ExecutableVariables, profile.Command);
        else
        {
            configuration.NativeCommand = SelectedExecutable(configuration.NativeCommand, profile.ExecutableVariables, profile.Command);
            configuration.Command = SelectedExecutable(configuration.Command, [profile.AdapterVariable], profile.Adapter);
            if (configuration.Command == configuration.NativeCommand) throw new ArgumentException("The backend CLI and ACP adapter must be configured separately.");
            if (profile.Id == "codex") configuration.ProcessEnvironment["CODEX_PATH"] = configuration.NativeCommand;
            if (profile.Id == "claude") configuration.ProcessEnvironment["CLAUDE_CODE_EXECUTABLE"] = configuration.NativeCommand;
            if (profile.Id == "pi")
            {
                configuration.ProcessEnvironment["PI_BIN"] = configuration.NativeCommand;
                configuration.ProcessEnvironment["PI_ACP_PI_COMMAND"] = configuration.NativeCommand;
            }
        }
        string[] defaults = DefaultArguments(configuration, profile);
        configuration.Arguments ??= defaults;
        if (configuration.Arguments.Length < defaults.Length || !configuration.Arguments.Take(defaults.Length).SequenceEqual(defaults))
            throw new ArgumentException("Named backend arguments must retain its required protocol and permission options. Use an unnamed ACP configuration for custom argv.");
        if (configuration.Arguments.Any(value => value == null || value.Any(char.IsControl))) throw new ArgumentException("Backend arguments cannot contain null or control characters.");
        if (profile.Id == "codex" && configuration.PermissionMode == "full") configuration.ProcessEnvironment["INITIAL_AGENT_MODE"] = "agent-full-access";
        if (profile.Id == "claude" && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")) && Environment.GetEnvironmentVariable("CLAUDE_API_KEY") is string key)
            configuration.ProcessEnvironment["ANTHROPIC_API_KEY"] = key;
        if (profile.Id == "openclaw" && configuration.TokenEnvironmentVariable != null)
        {
            string secret = Environment.GetEnvironmentVariable(configuration.TokenEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(secret)) throw new ArgumentException("OpenClaw gateway credential is missing.");
            configuration.ProcessEnvironment["OPENCLAW_GATEWAY_TOKEN"] = secret;
            configuration.ForwardEnvironment = configuration.ForwardEnvironment.Append(configuration.TokenEnvironmentVariable).Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        }
    }
    private static string[] DefaultArguments(HarnessConfiguration configuration, BackendProfile profile)
    {
        if (profile.Id == "openclaw")
        {
            string endpoint = configuration.Endpoint ?? "http://127.0.0.1:18789";
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var address) || !new[] { "http", "https", "ws", "wss" }.Contains(address.Scheme) ||
                address.UserInfo.Length != 0 || address.Query.Length != 0 || address.Fragment.Length != 0 ||
                !address.IsLoopback && address.Scheme != "https" && address.Scheme != "wss") throw new ArgumentException("OpenClaw requires HTTPS/WSS remotely or a loopback gateway address.");
            var target = new UriBuilder(address) { Scheme = address.Scheme is "https" or "wss" ? "wss" : "ws", Port = address.IsDefaultPort ? -1 : address.Port };
            return ["acp", "--url", target.Uri.AbsoluteUri.TrimEnd('/'), "--verbose"];
        }
        return profile.Arguments.Concat(configuration.PermissionMode == "full" && profile.Id is "qoder" or "codebuddy" ? new[] { "--dangerously-skip-permissions" } : []).ToArray();
    }
}
