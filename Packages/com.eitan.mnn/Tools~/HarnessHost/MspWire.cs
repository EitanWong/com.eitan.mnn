using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SiriAI.HarnessHost;

internal static class MspWire
{
    internal const string SchemaFingerprint = "sha256:c94c3d1f2f51e6424074b9ec43eac3eedcf532d3dcd77351ec1383a87d3049c6";
    internal static string Text(JsonElement value, string key) => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(key, out var field) && field.ValueKind == JsonValueKind.String ? field.GetString() : null;
    internal static JsonElement Field(JsonElement value, string key) => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(key, out var field) ? field : default;
    internal static string Required(JsonElement value, string key) => Text(value, key) is string text && !string.IsNullOrWhiteSpace(text) && text.Length <= 4096 ? text : throw new IOException("MSP response is missing a bounded " + key + ".");
    internal static IEnumerable<JsonElement> Array(JsonElement value) => value.ValueKind == JsonValueKind.Array ? value.EnumerateArray() : Enumerable.Empty<JsonElement>();
    internal static string Hash(object value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value))).ToLowerInvariant();
    internal static string Bounded(string value, int maximum = 4096) => string.IsNullOrWhiteSpace(value) ? "Muse activity" : value.Length > maximum ? value[..maximum] : value;
    internal static string CommandId()
    {
        // MSP commands use UUIDv7; RPC request IDs are a separate correlation namespace.
        byte[] bytes = RandomNumberGenerator.GetBytes(16);
        long milliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        for (int index = 5; index >= 0; index--) { bytes[index] = (byte)milliseconds; milliseconds >>= 8; }
        bytes[6] = (byte)((bytes[6] & 15) | 0x70);
        bytes[8] = (byte)((bytes[8] & 63) | 0x80);
        string hex = Convert.ToHexString(bytes).ToLowerInvariant();
        return $"{hex[..8]}-{hex[8..12]}-{hex[12..16]}-{hex[16..20]}-{hex[20..]}";
    }
}
