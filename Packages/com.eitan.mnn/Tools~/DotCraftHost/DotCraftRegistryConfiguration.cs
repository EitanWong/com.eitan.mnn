using System.Security.Cryptography;
using System.Text.Json;
using DotCraft.Configuration;
using DotCraft.Plugins;
using DotCraft.Plugins.Marketplaces;

namespace SiriAI.HarnessHost;

internal static class DotCraftRegistryConfiguration
{
    internal static void Resolve(HarnessConfiguration config, string baseDirectory, IEnumerable<string> workspaces)
    {
        if (config.DotCraftPluginRegistryConfig == null) return;
        string path = Path.GetFullPath(config.DotCraftPluginRegistryConfig, baseDirectory);
        string parent = DotCraftPluginConfiguration.CanonicalDirectory(Path.GetDirectoryName(path), baseDirectory);
        path = Path.Combine(parent, Path.GetFileName(path));
        if (!File.Exists(path) || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0 ||
            workspaces.Concat(config.DotCraftPluginRoots).Any(root => DotCraftPluginConfiguration.Within(
                DotCraftPluginConfiguration.CanonicalDirectory(root, baseDirectory), parent)))
            throw new ArgumentException("DotCraft plugin registry config must be an existing administrator file outside workspaces and plugin bundles.");
        Read(path); config.DotCraftPluginRegistryConfig = path;
    }

    internal static AppConfig.PluginsConfig Read(string path)
    {
        if (new FileInfo(path).Length > 128 * 1024) throw new InvalidDataException("Plugin registry config exceeds 128 KiB.");
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        if (document.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Plugin registry config must be an object.");
        var plugins = RequiredProperty(document.RootElement, "Plugins", JsonValueKind.Object);
        var entries = RequiredProperty(plugins, "PluginRegistries", JsonValueKind.Array);
        if (entries.GetArrayLength() > 32 || entries.EnumerateArray().Any(entry => entry.ValueKind != JsonValueKind.Object))
            throw new InvalidDataException("Configure at most 32 plugin registry objects.");
        var registries = PluginsConfigPersistence.ReadPluginRegistries(path);
        // Official persistence tolerates and skips bad entries; administrator input must fail explicitly.
        if (registries.Count != entries.GetArrayLength()) throw new InvalidDataException("Malformed plugin registry entry.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in registries)
        {
            if (!PluginManifestParser.IsValidPluginId(entry.Name) || !names.Add(entry.Name) || entry.Url.Length > 4096)
                throw new InvalidDataException("Plugin registry names must be nonempty, unique identifiers and sources must be bounded.");
            var source = MarketplaceSourceParser.FromConfigured(entry.SourceType, entry.Url, entry.Ref, entry.SparsePaths);
            if (source.Kind == MarketplaceSourceKind.Git)
                throw new NotSupportedException("Repository marketplace management is not implemented by this Host; configure a local marketplace or archive.");
            if (Uri.TryCreate(source.Value, UriKind.Absolute, out var uri) && uri.Scheme == "https" && uri.UserInfo.Length == 0 && uri.Fragment.Length == 0) continue;
            if (!Path.IsPathFullyQualified(source.Value) || !File.Exists(source.Value) && !Directory.Exists(source.Value))
                throw new InvalidDataException("Plugin registry sources require HTTPS or an existing absolute local path.");
        }
        return new AppConfig.PluginsConfig { DisableDefaultPluginRegistry = true, PluginRegistries = [.. registries] };
    }

    private static JsonElement RequiredProperty(JsonElement parent, string name, JsonValueKind kind)
    {
        var properties = parent.EnumerateObject().Where(property => string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (properties.Length != 1 || properties[0].Value.ValueKind != kind)
            throw new InvalidDataException("Plugin registry config requires one " + name + " of type " + kind + ".");
        return properties[0].Value;
    }

    internal static async Task<AppConfig.PluginsConfig> PrepareAsync(string configPath, string home, CancellationToken cancellation)
    {
        var config = Read(configPath);
        foreach (var entry in config.PluginRegistries)
        {
            if (Directory.Exists(entry.Url))
            {
                PluginContentTree.Fingerprint(entry.Url, "SiriAI.RegistrySource.v1\0"u8.ToArray());
                continue;
            }
            if (!File.Exists(entry.Url)) continue; // HTTPS reads cached data until explicit sync.
            var file = new FileInfo(entry.Url);
            if (file.Length > DotCraftRegistryArchive.MaximumCompressedBytes) throw new InvalidDataException("Plugin registry archive exceeds 64 MiB.");
            using var input = file.OpenRead();
            byte[] bytes = await DotCraftRegistryArchive.ReadAsync(input, cancellation);
            string destination = Path.Combine(home, "inputs", MspWire.Hash(entry.Url) + ".zip");
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            if (!File.Exists(destination) || !SHA256.HashData(File.ReadAllBytes(destination)).SequenceEqual(SHA256.HashData(bytes)))
            {
                string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try { await File.WriteAllBytesAsync(temporary, bytes, cancellation); File.Move(temporary, destination, true); }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
            // Official catalog materializes local ZIPs through the local snapshot source kind.
            entry.Url = destination; entry.SourceType = "local";
        }
        string activePath = Path.Combine(home, "config.json");
        var current = PluginsConfigPersistence.ReadPluginRegistries(activePath);
        foreach (var entry in config.PluginRegistries)
        {
            var previous = current.SingleOrDefault(existing =>
                string.Equals(existing.Name, entry.Name, StringComparison.OrdinalIgnoreCase) && SameSource(existing, entry));
            // Refresh metadata belongs to the SDK. Keep it only for the same configured source.
            entry.LastUpdated = previous?.LastUpdated;
            entry.LastRevision = previous?.LastRevision;
        }
        if (JsonSerializer.Serialize(current) != JsonSerializer.Serialize(config.PluginRegistries))
            PluginsConfigPersistence.WritePluginRegistries(activePath, config.PluginRegistries);
        return config;
    }

    private static bool SameSource(AppConfig.PluginRegistryConfig left, AppConfig.PluginRegistryConfig right)
    {
        var first = MarketplaceSourceParser.FromConfigured(left.SourceType, left.Url, left.Ref, left.SparsePaths);
        var second = MarketplaceSourceParser.FromConfigured(right.SourceType, right.Url, right.Ref, right.SparsePaths);
        // Refresh records a local ZIP as archive; catalog needs the local snapshot kind to extract it.
        if (File.Exists(first.Value)) first = first with { Kind = MarketplaceSourceKind.Archive };
        if (File.Exists(second.Value)) second = second with { Kind = MarketplaceSourceKind.Archive };
        string DocumentPath(string path) => string.IsNullOrWhiteSpace(path)
            ? ".craft/plugins/marketplace.json" : path.Trim().Replace('\\', '/');
        return first.Matches(second) && DocumentPath(left.MarketplacePath) == DocumentPath(right.MarketplacePath);
    }

    internal static string FutureDirectory(string path)
    {
        path = Path.GetFullPath(path);
        if (Directory.Exists(path)) return DotCraftPluginConfiguration.CanonicalDirectory(path, Path.GetPathRoot(path));
        var directory = new DirectoryInfo(path);
        return Path.Combine(FutureDirectory(directory.Parent.FullName), directory.Name);
    }
}
