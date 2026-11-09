using System.IO.Compression;

namespace SiriAI.HarnessHost;

/// <summary>Bound archives before handing their bytes to the official SDK extractor.</summary>
internal static class DotCraftRegistryArchive
{
    internal const int MaximumCompressedBytes = 64 * 1024 * 1024;
    private const long MaximumExpandedBytes = 512L * 1024 * 1024;

    internal static void Validate(byte[] bytes, CancellationToken cancellation)
    {
        if (bytes.Length > MaximumCompressedBytes) throw new InvalidDataException("Plugin registry archive exceeds 64 MiB.");
        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        if (archive.Entries.Count > 20000) throw new InvalidDataException("Plugin registry archive has too many entries.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long expanded = 0;
        int files = 0;
        foreach (var entry in archive.Entries)
        {
            ValidateName(entry, names);
            if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\')) continue;
            if (++files > 10000 || entry.Length > MaximumExpandedBytes - expanded)
                throw new InvalidDataException("Plugin registry archive exceeds expanded content limits.");
            expanded += ReadExpanded(entry, MaximumExpandedBytes - expanded, cancellation);
        }
    }

    private static void ValidateName(ZipArchiveEntry entry, HashSet<string> names)
    {
        string path = entry.FullName.Replace('\\', '/');
        string[] parts = path.TrimEnd('/').Split('/');
        if (string.IsNullOrWhiteSpace(path) || path.StartsWith('/') || path.Contains(':') || path.Any(char.IsControl) ||
            parts.Length > 64 || parts.Any(part => part is "" or "." or "..") ||
            !names.Add(path.TrimEnd('/')) || ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000)
            throw new InvalidDataException("Plugin registry archive has an unsafe or duplicate entry path.");
    }

    private static long ReadExpanded(ZipArchiveEntry entry, long budget, CancellationToken cancellation)
    {
        using var stream = entry.Open();
        var buffer = new byte[81920];
        long count = 0;
        int read;
        while ((read = stream.Read(buffer)) > 0)
        {
            cancellation.ThrowIfCancellationRequested();
            if (read > budget - count) throw new InvalidDataException("Plugin registry archive exceeds expanded content limits.");
            count += read;
        }
        if (count != entry.Length) throw new InvalidDataException("Plugin registry archive length is inconsistent.");
        return count;
    }

    internal static async Task<byte[]> ReadAsync(Stream stream, CancellationToken cancellation)
    {
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellation)) > 0)
        {
            if (read > MaximumCompressedBytes - output.Length) throw new InvalidDataException("Plugin registry archive exceeds 64 MiB.");
            output.Write(buffer, 0, read);
        }
        var bytes = output.ToArray(); Validate(bytes, cancellation); return bytes;
    }
}

internal sealed class DotCraftRegistryTransport : DelegatingHandler
{
    internal DotCraftRegistryTransport() : base(new SocketsHttpHandler { AllowAutoRedirect = false }) { }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
    {
        var response = await base.SendAsync(request, cancellation);
        try
        {
            if (!response.IsSuccessStatusCode) return response;
            if (response.Content.Headers.ContentLength > DotCraftRegistryArchive.MaximumCompressedBytes)
                throw new InvalidDataException("Plugin registry archive exceeds 64 MiB.");
            using var input = await response.Content.ReadAsStreamAsync(cancellation);
            var bytes = await DotCraftRegistryArchive.ReadAsync(input, cancellation);
            response.Content.Dispose(); response.Content = new ByteArrayContent(bytes); return response;
        }
        catch { response.Dispose(); throw; }
    }
}
