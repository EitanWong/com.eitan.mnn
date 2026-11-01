using System;
using System.IO;
using System.Security.Cryptography;

namespace MNN.Unity
{
    // One exact two-encoder export; other CLIP/Qwen-VL exports need their own protocol.
    internal static class MNNImageEmbeddingModel
    {
        internal const string Repository = "budaoshou/MobileCLIP-MNN";
        internal const string Marker = "mnn_image_embedding.json";
        internal const string Revision = "9059bc3438a22e9bb1281a4e29ffdeb464e80597";
        internal static readonly string[] Files = {"vision_model.mnn", "text_model.mnn", "tokenizer.json", "merges.txt", "preprocessor_config.json", "tokenizer_config.json"};
        internal static readonly long[] Sizes = {45513028, 169819220, 2224081, 524605, 382, 763};
        internal static readonly string[] Hashes = {
            "7f80d3541b9acfb2fec2b1d6d0adddc1a58799f6a1c290be22fcd34d00e09dcb",
            "ca6a86db74f9f8bbe41a238910bfd9764198bba4064419d3fa421f2bd3c244c3",
            "72ed5c96db5729294468543e4bc75fce14ca63f58e37300290189ba1c1e52b85",
            "d308b7377a8ceaa9707a21614fe8c831b9196e197b7aeb69833359362907af02",
            "b031f09fbd69e22a605b6cc7433993249ee893b7fc1b79321f669cd015493dd4",
            "a7d9d24f248071b792e4a3b56ab0539c2f40eec8da56d6fd91fb3a50058acebd"};
        internal static bool IsRepository(string repository) => string.Equals(repository, Repository, StringComparison.OrdinalIgnoreCase);
        internal static bool IsComplete(string directory)
        {
            if (!File.Exists(Path.Combine(directory, Marker))) return false;
            for (int i = 0; i < Files.Length; ++i)
            {
                var file = new FileInfo(Path.Combine(directory, Files[i]));
                if (!file.Exists || file.Length != Sizes[i]) return false;
            }
            return true;
        }
        internal static void Verify(string directory)
        {
            for (int i = 0; i < Files.Length; ++i)
                using (var stream = File.OpenRead(Path.Combine(directory, Files[i])))
                using (var sha = SHA256.Create())
                    if (BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant() != Hashes[i])
                        throw new InvalidDataException("Image embedding requires the pinned publisher asset: " + Files[i]);
        }
        internal static void Prepare(string directory)
        {
            Verify(directory);
            File.WriteAllText(Path.Combine(directory, Marker), "{\"profile\":\"MobileCLIP\"}\n");
        }
    }
}
