using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>Versioned Player snapshot. Paths are relative to MNN/Models, never Editor paths.</summary>
    [Serializable]
    public sealed class MNNModelDeliveryManifest
    {
        public const string StreamingPath = "MNN/model-delivery.json";
        public const string ModelsPath = "MNN/Models";
        public int schemaVersion = 1;
        public bool allowDownloads = true;
        public MNNBundledModelStorage bundledStorage;
        public int requestTimeout = 300;
        public MNNModelDeliveryFile[] files = Array.Empty<MNNModelDeliveryFile>();
        public void Validate()
        {
            if (schemaVersion != 1)
                throw new InvalidDataException("Unsupported MNN model delivery schema: " + schemaVersion);
            if (!Enum.IsDefined(typeof(MNNBundledModelStorage), bundledStorage) || requestTimeout < 1)
                throw new InvalidDataException("Invalid MNN runtime delivery settings.");
            if (files == null)
                throw new InvalidDataException("Model delivery files are missing.");
            var paths = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in files)
            {
                if (file == null)
                    throw new InvalidDataException("Null model file entry.");
                MNNModelAssetDownloader.ResolvePath(".", file.path);
                if (!paths.Add(file.path))
                    throw new InvalidDataException("Duplicate model file: " + file.path);
                if (file.bytes < 0 || file.sha256 == null || file.sha256.Length != 64)
                    throw new InvalidDataException("Model files require byte count and SHA-256: " + file.path);
                foreach (var c in file.sha256)
                    if (!Uri.IsHexDigit(c))
                        throw new InvalidDataException("Invalid SHA-256: " + file.path);
                if (!file.bundled || !string.IsNullOrEmpty(file.url))
                    if (!Uri.TryCreate(file.url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !string.IsNullOrEmpty(uri.UserInfo) || file.bytes < 1)
                        throw new InvalidDataException("Remote model files require HTTPS and a positive byte count: " + file.path);
            }
        }

        public static string ComputeSha256(string path, CancellationToken token = default)
        {
            using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            using (var input = File.OpenRead(path))
            {
                var buffer = new byte[128 * 1024];
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    token.ThrowIfCancellationRequested();
                    hash.AppendData(buffer, 0, read);
                }

                return BitConverter.ToString(hash.GetHashAndReset()).Replace("-", "").ToLowerInvariant();
            }
        }
    }

    public enum MNNBundledModelStorage
    {
        Automatic,
        PersistentData
    }
}
