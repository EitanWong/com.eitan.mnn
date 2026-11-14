using System;
using System.Collections.Generic;
using System.Linq;

namespace MNN.Samples.SiriAI.Harnesses
{
    /// <summary>Reusable onboarding metadata, independent of Unity, process execution and product UI.</summary>
    public sealed class HarnessBackendDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public string Command { get; }
        public string Adapter { get; }
        public IReadOnlyList<string> ExecutableVariables { get; }
        public string AdapterVariable { get; }
        public string Integration { get; }
        public string ConfigurationHint { get; }
        public string DocumentationUrl { get; }
        public IReadOnlyList<HarnessInstallComponent> Components { get; }
        internal HarnessBackendDefinition(string id, string name, string command, string adapter, string integration,
            string hint, string documentation, params HarnessInstallComponent[] components)
        {
            Id = id; Name = name; Command = command; Adapter = adapter; Integration = integration;
            ConfigurationHint = hint; DocumentationUrl = documentation;
            Components = Array.AsReadOnly(components);
            ExecutableVariables = Array.AsReadOnly(CommandVariables(id));
            AdapterVariable = id == "codex" ? "CODEX_ACP_BIN" : id == "claude" ? "CLAUDE_CODE_ACP_BIN" : id == "pi" ? "PI_ACP_BIN" : null;
        }
        private static string[] CommandVariables(string id)
        {
            switch (id)
            {
                case "codex": return new[] { "CODEX_PATH" };
                case "claude": return new[] { "CLAUDE_CODE_EXECUTABLE" };
                case "deepseek": return new[] { "DEEPSEEK_HARNESS_BIN" };
                case "pi": return new[] { "PI_ACP_PI_COMMAND", "PI_BIN" };
                case "hermes": return new[] { "HERMES_BIN" };
                case "openclaw": return new[] { "OPENCLAW_BIN" };
                case "qwen": return new[] { "QWEN_CODE_BIN" };
                case "opencode": return new[] { "OPENCODE_BIN" };
                case "qoder": return new[] { "QODERCLI_PATH", "QODER_CLI_PATH" };
                case "minimax": return new[] { "MINIMAX_CODE_BIN" };
                case "kimi": return new[] { "KIMI_CODE_BIN" };
                case "codebuddy": return new[] { "CODEBUDDY_BIN" };
                case "muse": return new[] { "MUSE_CODE_BIN" };
                default: return Array.Empty<string>();
            }
        }
        public HarnessInstallComponent[] MissingComponents(HarnessInstallation installation) => Components.Where(component =>
            component.Adapter ? string.IsNullOrEmpty(installation?.AdapterExecutable) : installation?.Installed != true).ToArray();
    }

    public sealed class HarnessInstallComponent
    {
        public string Package { get; }
        public string ScriptUrl { get; }
        public bool Adapter { get; }
        internal HarnessInstallComponent(string package, bool adapter = false, string script = null)
        { Package = package; Adapter = adapter; ScriptUrl = script; }
    }
}
