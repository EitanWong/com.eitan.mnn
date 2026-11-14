using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;

namespace MNN.Unity
{
    // Official sherpa-onnx GigaSpeech 3.3M release converted with the official MNN 3.6.1 converter.
    internal static class MNNEnglishKwsModel
    {
        internal const string Repository = "k2-fsa/sherpa-onnx-kws-zipformer-gigaspeech-3.3M-2024-01-01";
        internal const string ModelScopeRepository = "EitanWang/English-KWS-GigaSpeech-MNN";
        internal const string Revision = "fa97f090a045bd56aebb788bec3db0248b3a537c";
        internal static bool IsModelScopeRepository(string repository) => string.Equals(repository, ModelScopeRepository, StringComparison.OrdinalIgnoreCase);
        internal static string ResolveRevision(string revision)
        {
            if (string.IsNullOrWhiteSpace(revision) || revision == "master" || revision == Revision) return Revision;
            throw new ArgumentException("English KWS requires the verified GigaSpeech export revision.", nameof(revision));
        }
        internal static MNNModelRepositoryFile[] Files() => Paths.Select((path, index) =>
            new MNNModelRepositoryFile { Path = path, Type = "blob", Size = Sizes[index], Sha256 = Hashes[index], Revision = Revision })
            .Concat(DeploymentMetadata()).ToArray();
        private static MNNModelRepositoryFile[] DeploymentMetadata() => new[]
        {
            new MNNModelRepositoryFile { Path = "LICENSE", Type = "blob", Size = 11358, Sha256 = "cfc7749b96f63bd31c3c42b5c471bf756814053e847c10f3eb003417bc523d30", Revision = Revision },
            new MNNModelRepositoryFile { Path = "NOTICE.md", Type = "blob", Size = 12074, Sha256 = "6a3245d34a1c1929203b1909ab45e97ca7f8a17454f7ba3ac05dbb79c763912a", Revision = Revision },
            new MNNModelRepositoryFile { Path = "README.md", Type = "blob", Size = 1690, Sha256 = "c286989d6e1dfae0424266be78a00e05acc7abb007a006c37fdd443e8900a253", Revision = Revision },
            new MNNModelRepositoryFile { Path = "release-manifest.json", Type = "blob", Size = 1711, Sha256 = "fa492e2ba8a303c043c90d9edbc6dfe435f64e01d5435de3644520d3c4dfe463", Revision = Revision }
        };
        internal static void Prepare(string directory)
        {
            Verify(directory);
            File.WriteAllText(Path.Combine(directory, MNNKeywordSpotterConfig.FileName),
                JsonUtility.ToJson(new MNNKeywordSpotterConfig { repository = Repository }, true));
        }
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
