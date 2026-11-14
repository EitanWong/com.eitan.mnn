using System.Text.Json;
using DotCraft.Configuration;
using DotCraft.Plugins;
using DotCraft.Plugins.Marketplaces;
using MNN.Samples.SiriAI.Harnesses;

namespace SiriAI.HarnessHost;

/// <summary>Local administrator process only. No task/model/HTTP route can install plugin code.</summary>
internal static class DotCraftRegistryCli
{
    internal static async Task<int> ExecuteAsync(string[] args)
    {
        try
        {
            var options = Parse(args); string action = args[1];
            var config = options.TryGetValue("--directory", out var directory)
                ? HostConfiguration.LoadWithEnvironment(HostServiceRegistration.Load(directory))
                : HostConfiguration.Load(Required(options, "--config"));
            var harness = config.Harnesses.SingleOrDefault(h => h.Id == Required(options, "--harness"));
            if (harness?.Protocol != "dotcraft" || harness.DotCraftPluginRegistryConfig == null)
                throw new ArgumentException("Select a DotCraft Harness with DotCraftPluginRegistryConfig.");
            string home = DotCraftRegistryConfiguration.FutureDirectory(Path.Combine(config.DataDirectory,
                "dotcraft-marketplaces", MspWire.Hash(new { harness.Id, harness.DotCraftPluginRegistryConfig })[..24]));
            if (config.Workspaces.Values.Any(root => DotCraftPluginConfiguration.Within(
                    DotCraftPluginConfiguration.CanonicalDirectory(root, Path.GetPathRoot(root)), home)))
                throw new ArgumentException("Plugin management storage must be outside task workspaces.");
            using var writer = new FileTaskStore(config.DataDirectory); // Excludes an active Host and other admin writers.
            Environment.SetEnvironmentVariable("DOTCRAFT_DEFAULT_PLUGIN_REGISTRY_URL", null);
            Environment.SetEnvironmentVariable("DOTCRAFT_PLUGIN_REGISTRIES", null);
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            var plugins = await DotCraftRegistryConfiguration.PrepareAsync(harness.DotCraftPluginRegistryConfig, home, cancellation.Token);
            using var transport = new DotCraftRegistryTransport();
            if (action == "sync")
            {
                var sync = new PluginRegistrySyncService(home, handler: transport);
                var manager = new MarketplaceManager(home, archiveSync: sync);
                var refreshed = await manager.RefreshAsync(null, cancellation.Token, plugins);
                if (refreshed.Errors.Count > 0) throw new IOException(string.Join(",", refreshed.Errors.Select(error => error.Code)));
            }
            var catalog = new BuiltInPluginCatalog([], plugins, home).Discover();
            if (catalog.Diagnostics.Any(d => d.Severity == PluginDiagnosticSeverity.Error))
                throw new InvalidDataException(string.Join(",", catalog.Diagnostics.Where(d => d.Severity == PluginDiagnosticSeverity.Error).Select(d => d.Code)));
            if (action is "catalog" or "sync")
                Console.WriteLine(JsonSerializer.Serialize(new { Action = action, Home = home, Plugins = catalog.Plugins.Select(Info), catalog.Diagnostics }));
            else
                Console.WriteLine(JsonSerializer.Serialize(Install(action, home, plugins, catalog, options)));
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine("Plugin management error: " + error.Message); return 2; }
    }

    private static object Install(string action, string home, AppConfig.PluginsConfig config, PluginDiscoveryResult catalog, Dictionary<string, string> options)
    {
        string id = Required(options, "--plugin");
        if (!PluginManifestParser.IsValidPluginId(id)) throw new ArgumentException("Invalid plugin id.");
        var candidate = catalog.Plugins.SingleOrDefault(p => PluginIds.EqualsCanonical(p.Manifest.Id, id))
            ?? throw new InvalidOperationException("Plugin is not available in the configured catalog.");
        string hash = ContentHash(candidate.Manifest.RootPath);
        if (hash != Required(options, "--expected-hash")) throw new InvalidOperationException("Plugin content changed; inspect catalog and confirm its current ContentHash.");
        string root = Path.Combine(home, "plugins", candidate.Manifest.Id);
        if (Directory.Exists(root))
        {
            if (!BuiltInPluginDeployer.IsManagedBuiltInPluginRoot(root)) throw new IOException("Installed plugin is user owned; it will not be replaced.");
            if (action != "update" && ContentHash(root) != hash) throw new InvalidOperationException("Installed content differs; use explicit update.");
        }
        var diagnostics = new BuiltInPluginDeployer(Path.Combine(home, "plugins"), [], config, home).DeployPlugin(candidate.Manifest.Id);
        if (diagnostics.Any(d => d.Severity == PluginDiagnosticSeverity.Error) || !Directory.Exists(root) || ContentHash(root) != hash)
            throw new IOException("Official plugin deployment did not produce the confirmed package bytes.");
        return new { Action = action, Home = home, Id = candidate.Manifest.Id, Root = root, ContentHash = hash, Diagnostics = diagnostics };
    }

    private static object Info(DiscoveredPlugin plugin) => new
    {
        Id = plugin.Manifest.Id, plugin.Manifest.Version, Marketplace = plugin.MarketplaceName,
        Root = plugin.Manifest.RootPath, ContentHash = ContentHash(plugin.Manifest.RootPath)
    };
    // Package review identity only. The official .NET trust algorithm remains SDK-owned.
    private static string ContentHash(string root) => PluginContentTree.Fingerprint(root,
        "SiriAI.PluginCatalog.v1\0"u8.ToArray(), path => path != BuiltInPluginDeployer.MarkerFile);

    private static Dictionary<string, string> Parse(string[] args)
    {
        if (args.Length < 2 || args[1] is not ("catalog" or "sync" or "install" or "update"))
            throw new ArgumentException("Usage: --plugins catalog|sync|install|update --config PATH|--directory SERVICE --harness ID [--plugin ID --expected-hash HASH]");
        string[] allowed = args[1] is "install" or "update" ? ["--config", "--directory", "--harness", "--plugin", "--expected-hash"] : ["--config", "--directory", "--harness"];
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 2; i < args.Length; i += 2)
            if (i + 1 >= args.Length || !allowed.Contains(args[i]) || !result.TryAdd(args[i], args[i + 1]))
                throw new ArgumentException("Invalid or duplicate plugin management option.");
        if (result.ContainsKey("--config") == result.ContainsKey("--directory")) throw new ArgumentException("Select one configuration source.");
        return result;
    }
    private static string Required(Dictionary<string, string> options, string key) =>
        options.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("Missing plugin management option: " + key);
}
