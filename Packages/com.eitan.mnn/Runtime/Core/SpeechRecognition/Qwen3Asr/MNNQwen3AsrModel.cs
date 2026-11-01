using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;

namespace MNN.Unity
{
    internal static class MNNQwen3AsrModel
    {
        internal const string Repository = "hb-dev/Qwen3-ASR-0.6B-INT8-MNN";
        internal const string ModelScopeRepository = "huangzhengxiang/Qwen3-ASR-0.6B-INT8-MNN";
        internal const string Revision = "7426689dc24394df2ba693f384cb5b07426ddf70";
        // ModelScope assets are pinned individually: llm_config was updated after the weight commit.
        internal const string ModelScopeConfigRevision = "6f248645b7cb6749bc868215cf56ff61d772c449";
        internal const string ModelScopeWeightsRevision = "ab1528644543e774f8c3d795f8dcde3c22fe6668";
        internal static readonly string[] Paths = {"config.json", "llm_config.json", "llm.mnn", "llm.mnn.weight", "audio.mnn", "audio.mnn.weight", "embeddings_bf16.bin", "tokenizer.txt"};
        internal static readonly long[] Sizes = {634, 797, 505912, 633809314, 2988144, 373185600, 311164928, 3194205};
        internal static readonly string[] Hashes =
        {
            "1da7a5588a5bdc4c770f1d118376007840eaf0ccd51ae827b2d5fe46e3110f83",
            "4cc63a9fa5acc7cd150481efd5a2e9da673ad3722f3029906e3ad6f634eb45ee",
            "867d7b02119f50d031ab87a462287cc6f15bdf3a8bf6ec6b7211207ba5b36cba",
            "12dd51477bb1062f547817f6c7f230463a612f31f2161cd157103038d3a4a1c8",
            "a4b9ca649ee53c29e2099daef42ee79646f256aef39eec8a222f95853e24fbe9",
            "1456a76d7039681fbfdd94ffdac486545da62859f80a58b43a6f527f42a72382",
            "ab067cf4287b06781cd926874df9d0917e79a2360381e661b5010b99651ffc09",
            "6c01ed7a1726e8dcb8ce33a7efe3b02a17c9210e352b77318132b72156f88a49"
        };
        internal static bool IsRepository(string repository) => string.Equals(repository, Repository, StringComparison.OrdinalIgnoreCase);
        internal static bool IsModelScopeRepository(string repository) => string.Equals(repository, ModelScopeRepository, StringComparison.OrdinalIgnoreCase);
        internal static string ResolveRevision(string revision, string repository)
        {
            bool modelScope = IsModelScopeRepository(repository);
            if (!modelScope && !IsRepository(repository)) throw new ArgumentException("Unknown Qwen3-ASR profile.", nameof(repository));
            string pinned = modelScope ? ModelScopeConfigRevision : Revision;
            if (string.IsNullOrWhiteSpace(revision) || revision == "main" || modelScope && revision == "master" || revision == pinned) return pinned;
            throw new ArgumentException("This Qwen3-ASR profile requires its verified asset plan.", nameof(revision));
        }
        internal static string FileRevision(int index, bool modelScope) => !modelScope ? Revision : index == 1 ? ModelScopeConfigRevision : ModelScopeWeightsRevision;
        internal static void Verify(string directory)
        {
            for (int i = 0; i < Paths.Length; ++i)
                using (var stream = File.OpenRead(Path.Combine(directory, Paths[i])))
                using (var hash = SHA256.Create())
                    if (stream.Length != Sizes[i] || !string.Equals(BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", ""), Hashes[i], StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Qwen3-ASR profile file failed verification: " + Paths[i]);
        }
        internal static void Prepare(string directory, string repository = Repository)
        {
            ResolveRevision(null, repository);
            Verify(directory);
            File.WriteAllText(Path.Combine(directory, MNNQwen3AsrConfig.FileName), JsonUtility.ToJson(new MNNQwen3AsrConfig {repository = repository}, true));
        }
        internal static bool IsComplete(string directory)
        {
            try
            {
                ResolveRevision(null, MNNQwen3AsrConfig.Read(directory).repository);
                return Paths.Select((path, i) => new FileInfo(Path.Combine(directory, path)).Length == Sizes[i]).All(value => value);
            }
            catch (Exception error) when (error is IOException || error is ArgumentException || error is UnauthorizedAccessException) { return false; }
        }
    }
}
