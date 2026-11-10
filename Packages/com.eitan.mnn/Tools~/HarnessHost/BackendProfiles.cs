using System.Text.RegularExpressions;

namespace SiriAI.HarnessHost;

/// <summary>One source for launch defaults, discovery and backend environment policy; no installation side effects.</summary>
internal sealed record BackendProfile(string Id, string Name, string Command, string Adapter, string[] Arguments,
    string[] EnvironmentNames, string[] EnvironmentPrefixes, bool FullPermission = true, bool AlwaysFull = false,
    string Integration = "acp", bool Implemented = true, string[] ExecutableVariables = null, string AdapterVariable = null, string MinimumVersion = null);

internal static class BackendProfiles
{
#if DOTCRAFT
    private const bool DotCraftImplemented = true;
#else
    private const bool DotCraftImplemented = false;
#endif
    internal static readonly BackendProfile[] All =
    [
        new("qwen", "Qwen Code", "qwen", null, ["--acp"], ["DASHSCOPE_API_KEY", "OPENAI_API_KEY", "OPENAI_BASE_URL"], ["QWEN_CODE_"], ExecutableVariables: ["QWEN_CODE_BIN"], MinimumVersion: "0.21.6"),
        new("qoder", "Qoder", "qodercli", null, ["--acp"], [], ["QODER_", "QODERCLI_"], ExecutableVariables: ["QODERCLI_PATH", "QODER_CLI_PATH"]),
        new("opencode", "OpenCode", "opencode", null, ["acp"], ["DASHSCOPE_API_KEY"], ["OPENCODE_"], ExecutableVariables: ["OPENCODE_BIN"], MinimumVersion: "1.18.0"),
        new("minimax", "MiniMax Code", "mcode", null, ["acp"], [], ["MINIMAX_"], ExecutableVariables: ["MINIMAX_CODE_BIN"], MinimumVersion: "0.3.7"),
        new("kimi", "Kimi Code", "kimi", null, ["acp"], [], ["KIMI_"], ExecutableVariables: ["KIMI_CODE_BIN"], MinimumVersion: "0.31.0"),
        new("hermes", "Hermes", "hermes", null, ["acp", "--accept-hooks"], [], ["HERMES_"], ExecutableVariables: ["HERMES_BIN"]),
        new("codebuddy", "CodeBuddy", "codebuddy", null, ["--acp"], [], ["CODEBUDDY_"], ExecutableVariables: ["CODEBUDDY_BIN"]),
        new("deepseek", "DeepSeek Harness", "dsh", null, ["--profile", "acp"], ["DEEPSEEK_API_KEY"], ["DEEPSEEK_", "DSH_"], ExecutableVariables: ["DEEPSEEK_HARNESS_BIN"], MinimumVersion: "0.1.5"),
        new("codex", "Codex", "codex", "codex-acp", [], ["DASHSCOPE_API_KEY", "OPENAI_API_KEY", "OPENAI_BASE_URL"], ["CODEX_"], ExecutableVariables: ["CODEX_PATH"], AdapterVariable: "CODEX_ACP_BIN"),
        new("claude", "Claude Code", "claude", "claude-agent-acp", [], ["ANTHROPIC_API_KEY", "ANTHROPIC_BASE_URL", "CLAUDE_API_KEY"], ["CLAUDE_"], ExecutableVariables: ["CLAUDE_CODE_EXECUTABLE"], AdapterVariable: "CLAUDE_CODE_ACP_BIN"),
        new("pi", "Pi", "pi", "pi-acp", [], ["ANTHROPIC_API_KEY", "ANTHROPIC_BASE_URL", "OPENAI_API_KEY", "OPENAI_BASE_URL", "GEMINI_API_KEY", "GOOGLE_API_KEY"], ["PI_"], AlwaysFull: true, ExecutableVariables: ["PI_ACP_PI_COMMAND", "PI_BIN"], AdapterVariable: "PI_ACP_BIN", MinimumVersion: "0.80.4"),
        new("openclaw", "OpenClaw", "openclaw", null, ["acp"], ["DASHSCOPE_API_KEY", "AGENT_API_KEY"], ["OPENCLAW_"], FullPermission: false, ExecutableVariables: ["OPENCLAW_BIN"]),
        new("acp", "ACP Agent", null, null, [], [], ["ACP_"], FullPermission: false, ExecutableVariables: ["ACP_COMMAND"]),
        new("a2a", "A2A Agent", null, null, [], [], [], FullPermission: false, Integration: "a2a"),
        new("http", "SiriAI HTTP v1", null, null, [], [], [], FullPermission: false, Integration: "http"),
        new("muse", "Muse Code", "muse", null, ["serve"], ["META_API_KEY"], ["MUSE_"], Integration: "msp", ExecutableVariables: ["MUSE_CODE_BIN"]),
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
