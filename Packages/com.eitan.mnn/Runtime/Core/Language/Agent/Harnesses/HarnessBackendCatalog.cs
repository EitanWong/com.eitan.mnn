using System;
using System.Collections.Generic;
using System.Linq;

namespace MNN.Samples.SiriAI.Harnesses
{
    public static class HarnessBackendCatalog
    {
        public static IReadOnlyList<HarnessBackendDefinition> All { get; } = Array.AsReadOnly(new[]
        {
            new HarnessBackendDefinition("codex", "Codex", "codex", "codex-acp", "acp", "codex login", "https://github.com/openai/codex", new HarnessInstallComponent("@openai/codex@latest", false, null), new HarnessInstallComponent("@agentclientprotocol/codex-acp@latest", true, null)),
            new HarnessBackendDefinition("claude", "Claude Code", "claude", "claude-agent-acp", "acp", "claude", "https://code.claude.com/docs", new HarnessInstallComponent("@anthropic-ai/claude-code@latest", false, null), new HarnessInstallComponent("@agentclientprotocol/claude-agent-acp@latest", true, null)),
            new HarnessBackendDefinition("deepseek", "DeepSeek Harness", "dsh", null, "acp", "dsh web · DEEPSEEK_API_KEY", "https://github.com/deepseek-ai/DeepSeek-Harness", new HarnessInstallComponent("@deepseek-ai/dsh@latest", false, null)),
            new HarnessBackendDefinition("pi", "Pi", "pi", "pi-acp", "acp", "pi · native tools run without a permission approval mechanism", "https://github.com/badlogic/pi-mono", new HarnessInstallComponent("@earendil-works/pi-coding-agent@latest", false, null), new HarnessInstallComponent("pi-acp@latest", true, null)),
            new HarnessBackendDefinition("hermes", "Hermes", "hermes", null, "acp", "hermes setup --portal", "https://hermes-agent.nousresearch.com", new HarnessInstallComponent(null, false, "https://hermes-agent.nousresearch.com/install.sh")),
            new HarnessBackendDefinition("openclaw", "OpenClaw", "openclaw", null, "acp", "openclaw onboard · start your gateway before connecting", "https://docs.openclaw.ai", new HarnessInstallComponent("openclaw@latest", false, null)),
            new HarnessBackendDefinition("qwen", "Qwen Code", "qwen", null, "acp", "qwen", "https://github.com/QwenLM/qwen-code", new HarnessInstallComponent("@qwen-code/qwen-code@latest", false, null)),
            new HarnessBackendDefinition("opencode", "OpenCode", "opencode", null, "acp", "opencode auth login", "https://opencode.ai/docs", new HarnessInstallComponent("opencode-ai@latest", false, null)),
            new HarnessBackendDefinition("qoder", "Qoder", "qodercli", null, "acp", "qodercli login", "https://docs.qoder.com", new HarnessInstallComponent("@qoder-ai/qodercli@latest", false, null)),
            new HarnessBackendDefinition("minimax", "MiniMax Code", "mcode", null, "acp", "mcode login", "https://platform.minimax.io", new HarnessInstallComponent("@minimax-ai/code@latest", false, null)),
            new HarnessBackendDefinition("kimi", "Kimi Code", "kimi", null, "acp", "kimi login", "https://moonshotai.github.io/kimi-cli", new HarnessInstallComponent("@moonshot-ai/kimi-code@latest", false, null)),
            new HarnessBackendDefinition("codebuddy", "CodeBuddy", "codebuddy", null, "acp", "codebuddy", "https://www.codebuddy.ai", new HarnessInstallComponent("@tencent-ai/codebuddy-code@latest", false, null)),
            new HarnessBackendDefinition("muse", "Muse Code", "muse", null, "msp", "muse · optional MSP SDK; configure your account first", "https://dev.meta.ai", new HarnessInstallComponent(null, false, "https://dev.meta.ai/install.sh")),
            new HarnessBackendDefinition("llm", "Bare LLM", null, null, "llm", "OpenAI-compatible URL, API key and model · text only, experimental", "https://platform.openai.com/docs")
        });
        public static HarnessBackendDefinition Get(string id) => All.FirstOrDefault(item => item.Id == id)
            ?? throw new ArgumentException("Unknown backend: " + id, nameof(id));
        public static string ChooseLocal(IEnumerable<HarnessInstallation> installations, string preferred = null)
        {
            var candidates = (installations ?? Array.Empty<HarnessInstallation>()).Where(item => item.Installed && item.ProtocolCandidate).ToArray();
            if (candidates.Any(item => item.Id == preferred)) return preferred;
            return All.Select(item => item.Id).FirstOrDefault(id => candidates.Any(item => item.Id == id));
        }
        public static string DisplayName(string id) => All.FirstOrDefault(item => item.Id == id)?.Name ?? id;
    }
}
