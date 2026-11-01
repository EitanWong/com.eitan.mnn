using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;

namespace MNN.Unity
{
    internal static class MNNParaformerModel
    {
        internal const string Repository = "KindBrave/speech_paraformer-large_asr_nat-zh-cn-16k-common-vocab8404-online-mnn";
        internal const string DuplicateRepository = "guoera/speech_paraformer-large_asr_nat-zh-cn-16k-common-vocab8404-online-mnn";
        internal const string Revision = "b6b9dd4471cffd6b59f283b2998284d38f4d31e0";
        internal const string DuplicateRevision = "a0d72b0dddeb36ec1d6c4bf8fe29e87d8832b39d";
        internal const string ModelScopeRevision = "c6d2ad13a5133ad7f212aa434c5333a9b477c70c";
        internal static readonly string[] Paths = {"encoder.mnn", "decoder.mnn", "asr_config.json", "config.json", "tokenizer.txt"};
        internal static readonly long[] Sizes = {636409864, 228552132, 32594, 167, 45238};
        internal static readonly string[] Hashes =
        {
            "df0f51f7647c47001e0149956f6144642fb2dde409ddeb968db9287e6ca0f799",
            "d05ffae895f729ad1840c56a5246b7073a5d7eac39f0dcaab965ba8fa641bab9",
            "0174a65f8683f61f61abc7563888dce50c2a0973c655a0f65d02283872f8ae14",
            "b870f6b8c917d9cd05239cdc92dead33350b0cbf201d6b1f8d9c3ec0e364d14e",
            "75f4bff544de034561a9cf86750bbbd80bef6cd1bc6e3d710fd856d52ff188cf"
        };
        internal static bool IsHuggingFaceRepository(string repository) =>
            string.Equals(repository, Repository, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(repository, DuplicateRepository, StringComparison.OrdinalIgnoreCase);
        internal static bool IsModelScopeRepository(string repository) => string.Equals(repository, Repository, StringComparison.OrdinalIgnoreCase);
        internal static string ResolveRevision(string revision, string repository, bool modelScope)
        {
            if (!(modelScope ? IsModelScopeRepository(repository) : IsHuggingFaceRepository(repository)))
                throw new ArgumentException("Unknown Paraformer profile.", nameof(repository));
            string pinned = modelScope ? ModelScopeRevision : string.Equals(repository, DuplicateRepository, StringComparison.OrdinalIgnoreCase) ? DuplicateRevision : Revision;
            if (string.IsNullOrWhiteSpace(revision) || revision == "main" || modelScope && revision == "master" || revision == pinned) return pinned;
            throw new ArgumentException("This Paraformer profile requires its verified asset plan.", nameof(revision));
        }
        internal static void Verify(string directory)
        {
            for (int i = 0; i < Paths.Length; ++i)
                using (var stream = File.OpenRead(Path.Combine(directory, Paths[i])))
                using (var hash = SHA256.Create())
                    if (stream.Length != Sizes[i] || !string.Equals(BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", ""), Hashes[i], StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Paraformer profile file failed verification: " + Paths[i]);
        }
        internal static void Prepare(string directory, string repository = Repository)
        {
            ResolveRevision(null, repository, false);
            Verify(directory);
            File.WriteAllText(Path.Combine(directory, MNNParaformerConfig.FileName), JsonUtility.ToJson(new MNNParaformerConfig {repository = repository}, true));
        }
        internal static bool IsComplete(string directory)
        {
            try
            {
                ResolveRevision(null, MNNParaformerConfig.Read(directory).repository, false);
                return Paths.Select((path, i) => new FileInfo(Path.Combine(directory, path)).Length == Sizes[i]).All(value => value);
            }
            catch (Exception error) when (error is IOException || error is ArgumentException || error is UnauthorizedAccessException) { return false; }
        }
    }
}
