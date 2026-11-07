using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace MNN.Unity
{
    // Official sherpa-onnx GigaSpeech 3.3M release converted with the official MNN 3.6.1 converter.
    internal static class MNNEnglishKwsModel
    {
        internal const string Repository = "k2-fsa/sherpa-onnx-kws-zipformer-gigaspeech-3.3M-2024-01-01";
        internal static readonly string[] Paths = { MNNKwsModel.Encoder, MNNKwsModel.Decoder, MNNKwsModel.Joiner, "tokens.txt", "bpe.model" };
        internal static readonly long[] Sizes = { 12391360, 1092352, 644660, 5006, 244837 };
        private static readonly string[] Hashes =
        {
            "2cff4ced4134e90a287ec6ae2fc6ff959e8b218a4d6bd5f9b0a43473c06a1ac0",
            "f8232799a4b89121a34a76f1212fd291d558e536ab8cb94690786be337f2c13c",
            "ea4a4984c292609835864a9a7197856dd618acfd2968381cd26ad2e4d1bd4dcd",
            "fd2ded4050a55d2b1578870ba8697d02371980217806b7558bd0a5cc60f3ba53",
            "c8a2a0129c4ab8e463164c142f82d25649661b122c8cd0b7aab5c9e80b90ad24"
        };
        internal static bool IsRepository(string repository) => string.Equals(repository, Repository, StringComparison.Ordinal);
        internal static bool IsComplete(string directory) => Paths.Select((path, index) =>
            File.Exists(Path.Combine(directory, path)) && new FileInfo(Path.Combine(directory, path)).Length == Sizes[index]).All(value => value);
        internal static void Verify(string directory)
        {
            for (int i = 0; i < Paths.Length; ++i)
                using (var stream = File.OpenRead(Path.Combine(directory, Paths[i])))
                using (var hash = SHA256.Create())
                    if (stream.Length != Sizes[i] || BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant() != Hashes[i])
                        throw new InvalidDataException("English KWS asset failed verification: " + Paths[i]);
        }
    }
}
