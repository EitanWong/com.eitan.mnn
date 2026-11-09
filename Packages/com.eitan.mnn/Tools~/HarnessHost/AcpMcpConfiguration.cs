using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace SiriAI.HarnessHost;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class AcpMcpServer
{
    public string Name { get; set; }
    public string Type { get; set; } = "stdio";
    public string Command { get; set; }
    public string[] Arguments { get; set; }
    public AcpMcpValue[] Environment { get; set; }
    public string Url { get; set; }
    public AcpMcpValue[] Headers { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class AcpMcpValue
{
    public string Name { get; set; }
    public string Value { get; set; }
    public string ValueEnvironmentVariable { get; set; }
    public string Prefix { get; set; }
}

/// <summary>Parse administrator configuration once; resolved credentials never enter serialized configuration.</summary>
internal static class AcpMcpConfiguration
{
    internal static IEnumerable<string> EnvironmentNames(HarnessConfiguration configuration) =>
        configuration.AcpMcpServers.Concat(configuration.DotCraftMcpServers).SelectMany(s => (s.Environment ?? []).Concat(s.Headers ?? []))
            .Select(v => v.ValueEnvironmentVariable).Where(n => n != null).Distinct(StringComparer.Ordinal);

    internal static void Resolve(HarnessConfiguration configuration, ISet<string> hostCredentials)
    {
        var servers = configuration.AcpMcpServers;
        if (servers == null || servers.Length > 32 || servers.Any(s => s == null)) throw new ArgumentException("Invalid ACP MCP server list.");
        if (servers.Length > 0 && configuration.Protocol != "acp") throw new ArgumentException("AcpMcpServers requires an ACP backend.");
        var resolved = ResolveServers(servers, hostCredentials);
        configuration.ResolvedAcpMcpServers = resolved.Wire;
        configuration.ResolvedAcpMcpProcesses = resolved.Processes;
    }
    internal static (JsonElement Wire, IReadOnlyDictionary<string, AcpMcpProcess> Processes) ResolveServers(AcpMcpServer[] servers, ISet<string> hostCredentials)
    {
        if (servers == null || servers.Length > 32 || servers.Any(s => s == null)) throw new ArgumentException("Invalid MCP server list.");
        if (servers.Select(s => s.Name).Distinct(StringComparer.Ordinal).Count() != servers.Length) throw new ArgumentException("MCP server names must be unique.");
        var processes = new Dictionary<string, AcpMcpProcess>(StringComparer.Ordinal);
        var wire = servers.Select(server => Server(server, hostCredentials, processes)).ToArray();
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new { wire, local = processes });
        if (bytes.Length > 128 * 1024) throw new ArgumentException("ACP MCP configuration exceeds its escaped wire budget.");
        using var document = JsonDocument.Parse(bytes);
        var tokens = hostCredentials.Select(System.Environment.GetEnvironmentVariable).Where(v => !string.IsNullOrEmpty(v)).ToArray();
        RejectHostTokens(document.RootElement, tokens);
        return (document.RootElement.GetProperty("wire").Clone(), processes);
    }
    private static void RejectHostTokens(JsonElement value, string[] tokens)
    {
        if (value.ValueKind == JsonValueKind.Object)
            foreach (var property in value.EnumerateObject()) RejectHostTokens(property.Value, tokens);
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray()) RejectHostTokens(item, tokens);
        else if (value.ValueKind == JsonValueKind.String && tokens.Any(t => value.GetString().Contains(t, StringComparison.Ordinal)))
            throw new ArgumentException("ACP MCP configuration cannot forward host authentication credentials.");
    }
    private static object Server(AcpMcpServer server, ISet<string> hostCredentials, Dictionary<string, AcpMcpProcess> processes)
    {
        Text(server.Name, 128, "server name");
        if (server.Type is "stdio" or "acp")
        {
            if (server.Url != null || server.Headers != null) throw new ArgumentException("Stdio MCP cannot contain URL or headers.");
            Text(server.Command, 4096, "executable");
            if (!Path.IsPathFullyQualified(server.Command) || !File.Exists(server.Command)) throw new ArgumentException("Stdio MCP executable must be an existing absolute file.");
            if (server.Arguments == null || server.Arguments.Length > 128 || server.Arguments.Any(a => a == null || a.Length > 4096 || a.Any(char.IsControl)))
                throw new ArgumentException("Invalid stdio MCP arguments.");
            var environment = Values(server.Environment ?? [], false, hostCredentials);
            if (server.Type == "stdio") return new { name = server.Name, command = server.Command, args = server.Arguments, env = environment };
            string serverId = "siriai-mcp-" + MspWire.Hash(new { server.Name });
            processes.Add(serverId, new AcpMcpProcess(server.Command, server.Arguments, environment));
            return new { type = "acp", name = server.Name, serverId };
        }
        if (server.Type is not ("http" or "sse")) throw new ArgumentException("ACP MCP transport must be stdio, http, sse or acp.");
        if (server.Command != null || server.Arguments != null || server.Environment != null) throw new ArgumentException("Remote MCP cannot contain process options.");
        Text(server.Url, 8192, "URL");
        if (!Uri.TryCreate(server.Url, UriKind.Absolute, out var url) || url.Scheme is not ("http" or "https") || url.UserInfo.Length != 0 || url.Fragment.Length != 0)
            throw new ArgumentException("MCP URL requires HTTP or HTTPS without credentials or fragment.");
        return new { type = server.Type, name = server.Name, url = server.Url, headers = Values(server.Headers ?? [], true, hostCredentials) };
    }
    private static AcpMcpWireValue[] Values(AcpMcpValue[] values, bool headers, ISet<string> hostCredentials)
    {
        if (values.Length > 64 || values.Any(v => v == null) ||
            values.Select(v => v.Name).Distinct(headers ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal).Count() != values.Length)
            throw new ArgumentException("MCP values must have unique bounded names.");
        return values.Select(value =>
        {
            string pattern = headers ? @"\A[!#$%&'*+.^_`|~0-9A-Za-z-]{1,128}\z" : @"\A[A-Za-z_][A-Za-z0-9_]{0,127}\z";
            if (value.Name == null || !Regex.IsMatch(value.Name, pattern)) throw new ArgumentException("Invalid MCP value name.");
            if ((value.Value != null) == (value.ValueEnvironmentVariable != null)) throw new ArgumentException("MCP value needs exactly one literal or environment reference.");
            string text = value.Value;
            if (value.ValueEnvironmentVariable != null)
            {
                string name = value.ValueEnvironmentVariable;
                if (!Regex.IsMatch(name, @"\A[A-Za-z_][A-Za-z0-9_]{0,127}\z") || hostCredentials.Contains(name))
                    throw new ArgumentException("MCP credential reference cannot use host authentication variables.");
                text = System.Environment.GetEnvironmentVariable(name) ?? throw new ArgumentException("Missing MCP credential environment variable: " + name);
            }
            string prefix = value.Prefix ?? "";
            if (prefix.Length > 128) throw new ArgumentException("MCP value prefix exceeds its budget.");
            text = prefix + text;
            if (text.Length > 8192 || text.IndexOf('\0') >= 0 || headers && text.Any(c => c < 32 || c > 126))
                throw new ArgumentException("Invalid or oversized MCP value.");
            return new AcpMcpWireValue(value.Name, text);
        }).ToArray();
    }
    private static void Text(string value, int limit, string label)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > limit || value.Any(char.IsControl)) throw new ArgumentException("Invalid MCP " + label + ".");
    }
    internal static void RequireCapabilities(HarnessConfiguration configuration, JsonElement capabilities)
    {
        var advertised = MspWire.Field(capabilities, "mcpCapabilities");
        foreach (var server in configuration.AcpMcpServers)
            if (server.Type != "stdio" && MspWire.Field(advertised, server.Type).ValueKind != JsonValueKind.True)
                throw new AcpMcpCapabilityException("Agent did not advertise MCP " + server.Type + " transport. No session request or prompt was sent.");
    }
}

internal sealed record AcpMcpWireValue(string name, string value);
internal sealed record AcpMcpProcess(string Command, string[] Arguments, AcpMcpWireValue[] Environment);

internal sealed class AcpMcpCapabilityException : NotSupportedException
{
    internal AcpMcpCapabilityException(string message) : base(message) { }
}
