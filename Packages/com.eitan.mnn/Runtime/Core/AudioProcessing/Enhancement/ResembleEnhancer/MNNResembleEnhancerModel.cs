using System;
using System.IO;
using System.Security.Cryptography;

namespace MNN.Unity
{
    internal static class MNNResembleEnhancerModel
    {
        internal const string ManifestFile = "resemble-enhancer-runtime.json";
        internal const string ManifestHash = "a045f6f50f6df1cd7abb0a270324e0efdfa274429615abdaa87b30d00fb571ac";

        internal static bool IsComplete(string directory)
        {
            try { Verify(directory, false); return true; }
            catch (Exception error) when (error is IOException || error is ArgumentException || error is UnauthorizedAccessException)
            { return false; }
        }

        internal static void Verify(string directory, bool content = true)
        {
            if (Hash(Path.Combine(directory, ManifestFile)) != ManifestHash)
                throw new InvalidDataException("Unknown Resemble Enhance runtime manifest.");
            var manifest = MNNModelData.Read(Path.Combine(directory, ManifestFile));
            foreach (object row in MNNModelData.Array(MNNModelData.Get(manifest, "files")))
            {
                string name = (string)MNNModelData.Get(row, "path");
                if (Path.GetFileName(name) != name)
                    throw new InvalidDataException("Unsafe Resemble Enhance file name.");
                string path = Path.Combine(directory, name);
                if (!File.Exists(path) || new FileInfo(path).Length != Convert.ToInt64(MNNModelData.Get(row, "bytes")))
                    throw new InvalidDataException("Missing or incomplete Resemble Enhance file: " + name);
                if ((content || name == MNNResembleEnhancerConfig.FileName) &&
                    Hash(path) != (string)MNNModelData.Get(row, "sha256"))
                    throw new InvalidDataException("Resemble Enhance checksum mismatch: " + name);
            }
        }

        private static string Hash(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
    }
}
