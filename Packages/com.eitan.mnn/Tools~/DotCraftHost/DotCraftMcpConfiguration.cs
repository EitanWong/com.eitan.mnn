using System.Text.Json;
using DotCraft.Mcp;

namespace SiriAI.HarnessHost;

/// <summary>Administrator MCP configuration projected into official DotCraft types per owner/workspace runtime.</summary>
internal static class DotCraftMcpConfiguration
{
    internal static void Resolve(HarnessConfiguration config, ISet<string> hostCredentials)
    {
        var servers = config.DotCraftMcpServers;
        if (servers.Length > 32 || servers.Any(s => s == null || s.Type is not ("stdio" or "http" or "sse")) ||
            servers.Select(s => s.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != servers.Length)
            throw new ArgumentException("DotCraft MCP requires unique named stdio or HTTP servers.");
        foreach (var server in servers.Where(s => s.Type != "stdio"))
            if (Uri.TryCreate(server.Url, UriKind.Absolute, out var url) && !url.IsLoopback && url.Scheme != "https")
                throw new ArgumentException("Remote DotCraft MCP requires HTTPS.");
        config.ResolvedDotCraftMcpServers = AcpMcpConfiguration.ResolveServers(servers, hostCredentials).Wire;
        foreach (var server in servers)
        {
            var names = (server.Environment ?? []).Select(v => v.Name).ToArray();
            if (names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Length)
                throw new ArgumentException("DotCraft MCP environment names must be unique ignoring case, matching the official client.");
        }
    }
    internal static List<McpServerConfig> Servers(HarnessConfiguration config, string workspace) =>
        config.ResolvedDotCraftMcpServers.EnumerateArray().Select(server => Server(server, workspace)).ToList();
    private static McpServerConfig Server(JsonElement server, string workspace)
    {
        var result = new McpServerConfig { Name = server.GetProperty("name").GetString() };
        if (server.TryGetProperty("command", out var command))
        {
            result.Transport = "stdio"; result.Command = command.GetString(); result.Cwd = workspace;
            result.Arguments = server.GetProperty("args").EnumerateArray().Select(a => a.GetString()).ToList();
            // The official manager inherits the environment. Its supported null overrides remove unspecified values.
            var system = BackendProcessEnvironment.AvailableSystemNames().ToHashSet(StringComparer.Ordinal);
            foreach (string name in Environment.GetEnvironmentVariables().Keys)
                if (!system.Contains(name)) result.EnvironmentVariables[name] = null;
            foreach (var value in server.GetProperty("env").EnumerateArray())
                result.EnvironmentVariables[value.GetProperty("name").GetString()] = value.GetProperty("value").GetString();
        }
        else
        {
            result.Transport = "http"; result.Url = server.GetProperty("url").GetString();
            foreach (var header in server.GetProperty("headers").EnumerateArray())
                result.Headers[header.GetProperty("name").GetString()] = header.GetProperty("value").GetString();
        }
        return result;
    }
    internal static async Task RequireReadyAsync(IServiceProvider services, IEnumerable<McpServerConfig> servers, CancellationToken cancellation)
    {
        var names = servers.Select(s => s.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (names.Count == 0) return;
        var manager = services.GetRequiredService<McpClientManager>();
        await manager.WaitForStartupCompletionAsync(cancellation);
        var ready = (await manager.ListStatusesAsync(cancellation)).Where(s => names.Contains(s.Name) && s.StartupState == "ready").ToArray();
        if (ready.Length != names.Count) throw new DotCraftMcpStartupException();
    }
}
internal sealed class DotCraftMcpStartupException : IOException
{
    internal DotCraftMcpStartupException() : base("DotCraft configured MCP startup failed; no task input was submitted.") { }
}
