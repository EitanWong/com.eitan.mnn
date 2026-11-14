using DotCraft.Plugins;

namespace SiriAI.HarnessHost;

internal sealed class DotCraftPluginStartupException(string message) : IOException(message);

/// <summary>Administrator-selected installed bundles; official DotCraft owns discovery and trust.</summary>
internal static class DotCraftPluginConfiguration
{
    internal static void Resolve(HarnessConfiguration config, string baseDirectory, IEnumerable<string> workspaces)
    {
        if (config.DotCraftPluginRoots.Length > 32 || config.DotCraftDisabledPlugins.Length > 128 ||
            config.DotCraftDisabledPlugins.Any(id => string.IsNullOrWhiteSpace(id) || id.Length > 128 || id.Any(char.IsControl)))
            throw new ArgumentException("Invalid DotCraft plugin roots or disabled ids.");
        config.DotCraftDisabledPlugins = config.DotCraftDisabledPlugins.Select(PluginIds.Canonicalize)
            .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal).ToArray();
        config.DotCraftPluginRoots = config.DotCraftPluginRoots.Select(root => CanonicalDirectory(root, baseDirectory)).ToArray();
        if (config.DotCraftPluginRoots.Distinct(StringComparer.Ordinal).Count() != config.DotCraftPluginRoots.Length)
            throw new ArgumentException("DotCraft plugin roots must be unique installed bundle directories.");
        config.DotCraftPluginRootHashes = new(StringComparer.Ordinal);
        config.ResolvedDotCraftPlugins = new(StringComparer.OrdinalIgnoreCase);
        foreach (string root in config.DotCraftPluginRoots)
        {
            var parsed = PluginManifestParser.Load(root);
            var manifest = parsed.Manifest ?? throw new ArgumentException("Invalid DotCraft plugin manifest: " + root);
            if (!config.ResolvedDotCraftPlugins.TryAdd(manifest.Id, manifest.Dotnet != null))
                throw new ArgumentException("DotCraft plugin ids must be unique.");
            config.DotCraftPluginRootHashes.Add(root, BundleHash(root));
        }
        if (config.DotCraftPluginAuthorityConfig != null)
        {
            string path = Path.GetFullPath(config.DotCraftPluginAuthorityConfig, baseDirectory);
            string parent = CanonicalDirectory(Path.GetDirectoryName(path), baseDirectory);
            if (workspaces.Concat(config.DotCraftPluginRoots).Any(root => Within(CanonicalDirectory(root, baseDirectory), parent)))
                throw new ArgumentException("DotCraft plugin trust authority must be outside workspaces and plugin bundles.");
            config.DotCraftPluginAuthorityConfig = Path.Combine(parent, Path.GetFileName(path));
            foreach (string file in new[] { config.DotCraftPluginAuthorityConfig, Path.Combine(parent, "dotnet-plugin-trust.json") })
                if (File.Exists(file) && (File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                    throw new ArgumentException("DotCraft plugin authority files cannot be filesystem links.");
        }
        if (config.DotCraftPluginRoots.Length > 0 || config.DotCraftDisabledPlugins.Length > 0 || config.DotCraftPluginAuthorityConfig != null)
            config.DotCraftPluginFingerprint = MspWire.Hash(new
            {
                config.DotCraftPluginRootHashes, config.DotCraftDisabledPlugins, config.DotCraftPluginAuthorityConfig
            });
    }

    // This is only SiriAI's native-session identity, never a replacement for DotCraft's trust fingerprint.
    private static string BundleHash(string root) => PluginContentTree.Fingerprint(root, "SiriAI.PluginSession.v1\0"u8.ToArray());

    internal static void RequireUnchanged(HarnessConfiguration config)
    {
        foreach (var entry in config.DotCraftPluginRootHashes)
            if (BundleHash(entry.Key) != entry.Value)
                throw new DotCraftPluginStartupException("DotCraft plugin bundle changed after Host configuration loaded; restart and review its trust.");
    }

    internal static void RequireReady(IServiceProvider services, HarnessConfiguration config)
    {
        if (!config.ResolvedDotCraftPlugins.Any(entry => entry.Value)) return;
        var snapshot = services.GetRequiredService<IPluginDotnetRuntimeCoordinator>().Snapshot;
        foreach (var entry in config.ResolvedDotCraftPlugins.Where(entry => entry.Value &&
                     !config.DotCraftDisabledPlugins.Contains(entry.Key, StringComparer.OrdinalIgnoreCase)))
        {
            var plugin = snapshot.Plugins.FirstOrDefault(plugin => PluginIds.EqualsCanonical(plugin.PluginId, entry.Key));
            if (plugin?.State != PluginDotnetRuntimeState.Active)
                throw new DotCraftPluginStartupException("DotCraft plugin " + entry.Key + " is not active (" +
                    (plugin == null ? "not discovered" : string.Join(",", plugin.Blockers.Select(blocker => blocker.Code))) + "). No model input was submitted.");
        }
    }

    internal static bool Within(string root, string path)
    {
        string relative = Path.GetRelativePath(root, path);
        return !Path.IsPathRooted(relative) && relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    internal static string CanonicalDirectory(string path, string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("DotCraft plugin directory is empty.");
        string absolute = Path.GetFullPath(path, baseDirectory);
        string current = Path.GetPathRoot(absolute);
        foreach (string part in absolute[current.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            var directory = new DirectoryInfo(Path.Combine(current, part));
            if (!directory.Exists) throw new ArgumentException("DotCraft plugin directory does not exist: " + path);
            current = directory.ResolveLinkTarget(true)?.FullName ?? directory.FullName;
        }
        return Path.TrimEndingDirectorySeparator(current);
    }
}
