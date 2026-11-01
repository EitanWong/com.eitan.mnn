using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;

namespace MNN.Unity
{
    // Curated BERT graph protocols. Other exports can supply an explicit managed profile.
    internal static class MNNBertEmbeddingModel
    {
        internal const string BgeRepository = "darkmaniac7/bge-small-en-MNN";
        internal const string AlternateBgeRepository = "developerabu/bge-small-en-v1.5-mnn";
        internal const string MiniLmRepository = "darkmaniac7/minilm-l6-v2-mnn";
        internal static readonly string[] Repositories = {BgeRepository, AlternateBgeRepository, MiniLmRepository};

        internal sealed class File
        {
            internal readonly string Path, Sha256;
            internal readonly long Bytes;
            internal File(string path, long bytes, string sha256) { Path = path; Bytes = bytes; Sha256 = sha256; }
        }

        internal static bool IsRepository(string repository) => Repositories.Contains(repository, StringComparer.OrdinalIgnoreCase);
        internal static string Revision(string repository)
        {
            if (string.Equals(repository, BgeRepository, StringComparison.OrdinalIgnoreCase))
                return "dadd3051a85179088c00d0fab72fe4be62ea3267";
            if (string.Equals(repository, AlternateBgeRepository, StringComparison.OrdinalIgnoreCase))
                return "4be8bae1d712388c4362c2dce911d656eb1f45fc";
            if (string.Equals(repository, MiniLmRepository, StringComparison.OrdinalIgnoreCase))
                return "1f941721919a7516a094b9286f9ab3a678f7796e";
            throw new ArgumentException("Unknown BERT embedding profile.", nameof(repository));
        }

        internal static string ResolveRevision(string repository, string revision)
        {
            string pinned = Revision(repository);
            if (string.IsNullOrWhiteSpace(revision) || revision == "main" || revision == pinned)
                return pinned;
            throw new ArgumentException("This BERT embedding profile requires model revision " + pinned + ".", nameof(revision));
        }

        internal static MNNBertEmbeddingConfig Config(string repository)
        {
            Revision(repository);
            bool mini = string.Equals(repository, MiniLmRepository, StringComparison.OrdinalIgnoreCase);
            return new MNNBertEmbeddingConfig
            {
                graph = mini ? "minilm.mnn" : string.Equals(repository, BgeRepository, StringComparison.OrdinalIgnoreCase) ? "encoder.mnn" : "model.mnn",
                maximumTokens = mini ? 256 : 512,
                pooling = mini ? MNNBertPooling.Mean : MNNBertPooling.Cls,
                queryPrefix = mini ? null : "Represent this sentence for searching relevant passages: "
            };
        }

        internal static File[] Files(string repository)
        {
            var config = Config(repository);
            if (string.Equals(repository, BgeRepository, StringComparison.OrdinalIgnoreCase))
                return new[]
                {
                    new File(config.graph, 66845176, "80be6642bfda0e97b33b22f926ed569c8b0ab1a5b949ee8f1ce346b00169679f"),
                    new File("tokenizer.json", 711396, "d241a60d5e8f04cc1b2b3e9ef7a4921b27bf526d9f6050ab90f9267a1f9e5c66"),
                    new File("README.md", 5482, "fed4b9293c98cf68744fe47b743ce06a22d1cf55bb5f19aca452db219eb9fecc")
                };
            if (string.Equals(repository, AlternateBgeRepository, StringComparison.OrdinalIgnoreCase))
                return new[]
                {
                    new File(config.graph, 69798528, "379ce003a8fe35560501ffaad2ad61ae9754d74a007861d6ac56c0ec95e482c6"),
                    new File("tokenizer.json", 711396, "d241a60d5e8f04cc1b2b3e9ef7a4921b27bf526d9f6050ab90f9267a1f9e5c66"),
                    new File("README.md", 31, "4bcf87ecfbbb8e07a01b21415a970c8b53a5283bf6872b657040d3f45c9241f7")
                };
            return new[]
            {
                new File(config.graph, 45353480, "fe76ffa44922ae49ae4e5f74d2bd3fae5c5085a7c14fd4ad6f35b26b1bbb895f"),
                new File("vocab.txt", 231508, "07eced375cec144d27c900241f3e339478dec958f92fddbc551f295c992038a3"),
                new File("README.md", 1028, "31e8638abfa98f4c3b6108502d46c2d33bf2856597d6c71bd9df9c07420aa316")
            };
        }

        internal static void Prepare(string directory, string repository)
        {
            foreach (var file in Files(repository))
            {
                string path = MNNBertEmbeddingConfig.ResolveFile(directory, file.Path);
                using (var stream = System.IO.File.OpenRead(path))
                using (var hash = SHA256.Create())
                    if (stream.Length != file.Bytes || !string.Equals(BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", ""), file.Sha256, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("BERT embedding profile file failed verification: " + file.Path);
            }
            System.IO.File.WriteAllText(Path.Combine(directory, MNNBertEmbeddingConfig.FileName), JsonUtility.ToJson(Config(repository), true));
        }
    }
}
