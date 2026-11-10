using System.Runtime.Serialization;
using System.Text.Json;
using MNN.Samples.SiriAI.Harnesses;

namespace SiriAI.HarnessHost;

[DataContract]
internal sealed class SkillStartupIntent
{
    [DataMember] public string HomeDirectory;
    [DataMember] public string LockHash;
    [DataMember] public string OwnerId;
}

internal sealed record SkillStartupSnapshot(string Hash, IReadOnlyDictionary<string, string> Sources);

internal static class SkillStartup
{
    private static readonly IReadOnlyDictionary<string, string> Directories = new Dictionary<string, string>
    {
        ["claude-code"] = ".claude/skills", ["codex"] = ".agents/skills", ["opencode"] = ".agents/skills",
        ["kimi-code-cli"] = ".agents/skills", ["openclaw"] = ".openclaw/skills", ["qoder"] = ".qoder/skills",
        ["qwen-code"] = ".qwen/skills", ["hermes-agent"] = ".hermes/skills", ["codebuddy"] = ".codebuddy/skills", ["pi"] = ".pi/agent/skills"
    };
    internal static string FilePath(string home, string installer, string name) => Path.Combine(home, Directories[installer], name, "SKILL.md");
    private static bool SafeName(string name) => !string.IsNullOrWhiteSpace(name) && name.Length <= 128 && name[0] != '-' &&
        name is not ("." or "..") && !name.Any(c => char.IsControl(c) || c is '/' or '\\' or ':');
    internal static SkillStartupSnapshot Read(string home, Action<string> report)
    {
        string path = Path.Combine(home, ".agents", ".skill-lock.json");
        if (!File.Exists(path)) return new(null, new Dictionary<string, string>());
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var bytes = new MemoryStream(); var buffer = new byte[8192]; int count;
        while ((count = file.Read(buffer, 0, buffer.Length)) != 0)
        { if (bytes.Length + count > 1024 * 1024) throw new IOException("Startup skill lock exceeds 1 MiB."); bytes.Write(buffer, 0, count); }
        byte[] data = bytes.ToArray();
        using var json = JsonDocument.Parse(data, new JsonDocumentOptions { MaxDepth = 32 });
        var sources = new Dictionary<string, string>(StringComparer.Ordinal);
        if (json.RootElement.ValueKind != JsonValueKind.Object || !json.RootElement.TryGetProperty("skills", out var skills) || skills.ValueKind != JsonValueKind.Object)
            return new(HostServiceRegistration.Hash(data), sources);
        foreach (var item in skills.EnumerateObject())
        {
            if (!SafeName(item.Name)) { report?.Invoke("Invalid startup skill name was skipped: " + JsonSerializer.Serialize(item.Name)); continue; }
            string source = Text(item.Value, "source") ?? Text(item.Value, "sourceUrl");
            if (source == null) { report?.Invoke("Startup skill has no source: " + JsonSerializer.Serialize(item.Name)); continue; }
            var request = new HarnessSkillRequest { SubmissionKey = "validate", Operation = "install", Source = source, Names = [item.Name] };
            try { request.Validate(); sources[item.Name] = source; }
            catch (ArgumentException) { report?.Invoke("Invalid startup skill source was skipped: " + JsonSerializer.Serialize(item.Name)); }
        }
        return new(HostServiceRegistration.Hash(data), sources);
    }
    private static string Text(JsonElement item, string field) => item.ValueKind == JsonValueKind.Object && item.TryGetProperty(field, out var value) &&
        value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()) ? value.GetString().Trim() : null;
    internal static bool Missing(string home, string installer, string name) => !File.Exists(FilePath(home, installer, name));
    internal static HarnessOutcome Prepare(SkillLibraryPlan plan, SkillsConfiguration configuration, string owner, out HarnessSkillRequest request)
    {
        request = plan.Request;
        if (plan.Startup == null) return null;
        var accepted = plan.Startup; var current = configuration.Startup;
        if (current == null || current.OwnerId != owner || accepted.OwnerId != owner || accepted.HomeDirectory != current.HomeDirectory)
            return HarnessOutcome.Interrupted("Startup skill configuration changed; no installer was started.");
        SkillStartupSnapshot snapshot;
        try { snapshot = Read(current.HomeDirectory, null); }
        catch (Exception error) when (error is IOException or JsonException or UnauthorizedAccessException)
        { return HarnessOutcome.Interrupted("Startup skill lock cannot be confirmed; no installer was started."); }
        if (snapshot.Hash != accepted.LockHash || request.Operation != "install" || request.Backends.Length != 1 || plan.Installers.Length != 1)
            return HarnessOutcome.Interrupted("Startup skill lock changed; no installer was started.");
        string acceptedSource = request.Source;
        var names = request.Names.Where(name => SafeName(name) && snapshot.Sources.TryGetValue(name, out var source) && source == acceptedSource &&
            Missing(current.HomeDirectory, plan.Installers[0], name)).ToArray();
        if (names.Length == 0) return HarnessOutcome.Completed("Startup skills are already present; no installer was started.");
        request = new HarnessSkillRequest { SubmissionKey = request.SubmissionKey, Operation = request.Operation, Source = request.Source, Names = names, Backends = request.Backends };
        return null;
    }
}
