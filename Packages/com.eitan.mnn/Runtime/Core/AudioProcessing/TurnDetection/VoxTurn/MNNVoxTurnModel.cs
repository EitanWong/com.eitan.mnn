using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
namespace MNN.Unity
{
    internal static class MNNVoxTurnModel
    {
        internal const string ConfigFile = "mnn_voxturn.json";
        internal const string ManifestHash = "4216053d50e6876dcbf34f9e53f884eb7a18662f46f8322e5a28eec07d49a41a";
        internal static bool IsComplete(string directory)
        {
            try
            {
                Verify(directory, false);
                return true;
            }
            catch (Exception error) when (error is IOException || error is ArgumentException || error is UnauthorizedAccessException) { return false; }
        }
        internal static void Verify(string directory, bool content = true)
        {
            if (!string.Equals(Hash(Path.Combine(directory, "voxturn-runtime.json")), ManifestHash, StringComparison.Ordinal))
                throw new InvalidDataException("Unknown VoxTurn model profile.");
            var manifest = MNNModelData.Read(Path.Combine(directory, "voxturn-runtime.json"));
            foreach (object entry in MNNModelData.Array(MNNModelData.Get(manifest, "files")))
            {
                string name = (string)MNNModelData.Get(entry, "path"), expected = (string)MNNModelData.Get(entry, "sha256");
                if (Path.GetFileName(name) != name)
                    throw new InvalidDataException("Unsafe VoxTurn file name.");
                string path = Path.Combine(directory, name);
                long size = Convert.ToInt64(MNNModelData.Get(entry, "bytes"));
                if (!File.Exists(path) || new FileInfo(path).Length != size)
                    throw new InvalidDataException("Missing or incomplete VoxTurn file: " + name);
                if (content && !string.Equals(Hash(path), expected, StringComparison.Ordinal))
                    throw new InvalidDataException("VoxTurn checksum mismatch: " + name);
            }
        }
        private static string Hash(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
        internal static float[] Floats(string directory, string name, int count)
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(directory, name));
            if (bytes.Length != checked(count * 4))
                throw new InvalidDataException("Invalid VoxTurn constant: " + name);
            var result = new float[count];
            Buffer.BlockCopy(bytes, 0, result, 0, bytes.Length);
            if (result.Any(v => float.IsNaN(v) || float.IsInfinity(v)))
                throw new InvalidDataException("Nonfinite VoxTurn constant: " + name);
            return result;
        }
    }
}
