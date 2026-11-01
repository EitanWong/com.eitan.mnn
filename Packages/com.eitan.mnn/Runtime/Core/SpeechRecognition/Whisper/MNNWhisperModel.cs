using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;

namespace MNN.Unity
{
    internal static class MNNWhisperModel
    {
        internal const string Repository = "developerabu/whisper-tiny-en-mnn";
        internal const string Revision = "d28322eb9c7dec03d776897267f1ab056832d9fd";
        internal static readonly string[] Paths = {"encode.mnn", "decode.mnn", "tiny.en-tokens.txt"};
        internal static readonly long[] Sizes = {12365220, 90881376, 835554};
        internal static readonly string[] Hashes =
        {
            "b83833c37d2b0169c1593ade5f41df6dd4fd74957ec6817eb99672b1bbdb0ce7",
            "66376630647fc35bcad2361d2801ffcacc8caa2b5327e9326b3ee9c7a8e37ac4",
            "306cd27f03c1a714eca7108e03d66b7dc042abe8c258b44c199a7ed9838dd930"
        };
        internal sealed class Profile
        {
            internal readonly string Id, Repository, Revision;
            internal readonly bool ModelScope, Multilingual;
            internal readonly int Layers, Width;
            internal readonly string[] Paths, Hashes;
            internal readonly long[] Sizes;
            internal bool ChineseAvailable => Id == "developerabu-small";
            internal int VocabularySize => Multilingual ? 51865 : 51864;
            internal string Name => Layers == 12 ? "small" : Layers == 6 ? "base" : Multilingual ? "tiny" : "tiny.en";
            internal int EndToken => Multilingual ? 50257 : 50256;
            internal Profile(string id, string repository, string revision, bool modelScope, bool multilingual,
                int layers, int width, string[] paths, long[] sizes, string[] hashes)
            {
                Id = id; Repository = repository; Revision = revision; ModelScope = modelScope;
                Multilingual = multilingual; Layers = layers; Width = width; Paths = paths; Sizes = sizes; Hashes = hashes;
            }
            internal MNNWhisperConfig Config() => new MNNWhisperConfig {profile = Id, encoder = Paths[0], decoder = Paths[1], tokens = Paths[2]};
        }

        internal static readonly Profile[] Profiles =
        {
            new Profile("developerabu-tiny-en", "developerabu/whisper-tiny-en-mnn", "d28322eb9c7dec03d776897267f1ab056832d9fd", false, false, 4, 384,
                new[] {"encode.mnn", "decode.mnn", "tiny.en-tokens.txt"}, new long[] {12365220, 90881376, 835554},
                new[] {"b83833c37d2b0169c1593ade5f41df6dd4fd74957ec6817eb99672b1bbdb0ce7", "66376630647fc35bcad2361d2801ffcacc8caa2b5327e9326b3ee9c7a8e37ac4", "306cd27f03c1a714eca7108e03d66b7dc042abe8c258b44c199a7ed9838dd930"}),
            new Profile("modelscope-tiny-en", "rap20171201/sherpa-mnn-whisper-tiny.en", "d0f4c8fdefd4df80e87285245f6fce9276dc5fa9", true, false, 4, 384,
                new[] {"tiny.en-encoder.mnn", "tiny.en-decoder.mnn", "tiny.en-tokens.txt"}, new long[] {12365220, 90881696, 835554},
                new[] {"c8772de31f74fd76d426128bde2c870a102b92b96d541ba52242512ab96e2bc6", "cd3c73f2e4ffeca8500e8ed3bff42e252fd25eebae47ecc628ee8e35bbfbd46a", "306cd27f03c1a714eca7108e03d66b7dc042abe8c258b44c199a7ed9838dd930"}),
            new Profile("developerabu-tiny", "developerabu/whisper-tiny-mnn", "4fbb7fae85ee8992210ef324f2bf79d3298452ca", false, true, 4, 384,
                new[] {"tiny-encoder.mnn", "tiny-decoder.mnn", "tiny-tokens.txt"}, new long[] {11452836, 89985888, 816730},
                new[] {"bda0adca7edef6a32c2e9c860688126ff686988cdbf9a845ce387436ca603c08", "9141791098cdcb53aeb2c51b1583e771434529fe75bef1b8b0567b6ba74dee47", "b34b360dbb493e781e479794586d661700670d65564001f23024971d1f2fa126"}),
            new Profile("developerabu-base", "developerabu/whisper-base-mnn", "4c877365c38c5d6897ba7591c4c53a130f3d3143", false, true, 6, 512,
                new[] {"encode.mnn", "decode.mnn", "base-tokens.txt"}, new long[] {29154436, 133417824, 816730},
                new[] {"0cd181eeac62f8571404176dbcf6a42450d9b6538527ea26d29a2c24732d6724", "0d88ef6cf9f60cbc34368fb211df29ee75583fc0813036127d845c2c38a2bf7e", "b34b360dbb493e781e479794586d661700670d65564001f23024971d1f2fa126"}),
            new Profile("developerabu-small", "developerabu/whisper-small-mnn", "bc469b60476aee692389c540e599047f461c9f05", false, true, 12, 768,
                new[] {"small-encoder.mnn", "small-decoder.mnn", "small-tokens.txt"}, new long[] {107374700, 263024872, 816730},
                new[] {"8e63730727e6e7e2b5fda9560a4bd808d5e652c27771fcc7c8d34eebb751475b", "56463e9f9514a6201dc52bd3b1f65e0d51d4ecba99ded648f550a5aaa261886e", "b34b360dbb493e781e479794586d661700670d65564001f23024971d1f2fa126"}),
        };
        internal static bool IsRepository(string repository) => Profiles.Any(p => !p.ModelScope && string.Equals(p.Repository, repository, StringComparison.OrdinalIgnoreCase));
        internal static bool IsModelScopeRepository(string repository) => Profiles.Any(p => p.ModelScope && string.Equals(p.Repository, repository, StringComparison.OrdinalIgnoreCase));
        internal static Profile ForRepository(string repository) => Profiles.FirstOrDefault(p => string.Equals(p.Repository, repository, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException("Unknown verified Whisper repository.", nameof(repository));
        internal static Profile ForConfig(MNNWhisperConfig config) => Profiles.FirstOrDefault(p => p.Id == config?.profile)
            ?? throw new ArgumentException("Unknown verified Whisper profile.", nameof(config));
        internal static string ResolveRevision(string revision, string repository = Repository)
        {
            var profile = ForRepository(repository);
            string pinned = profile.Revision;
            if (string.IsNullOrWhiteSpace(revision) || revision == "main" || profile.ModelScope && revision == "master" || revision == pinned) return pinned;
            throw new ArgumentException("This Whisper profile requires model revision " + pinned + ".", nameof(revision));
        }
        internal static string[] Assets(MNNWhisperConfig config)
        {
            ForConfig(config);
            return new[] {config.encoder, config.decoder, config.tokens};
        }
        internal static void Verify(string directory, MNNWhisperConfig config)
        {
            var profile = ForConfig(config);
            var paths = Assets(config);
            for (int i = 0; i < paths.Length; ++i)
                using (var stream = File.OpenRead(MNNZipformerConfig.Resolve(directory, paths[i])))
                using (var hash = SHA256.Create())
                    if (stream.Length != profile.Sizes[i] || !string.Equals(BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", ""), profile.Hashes[i], StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Whisper profile file failed verification: " + paths[i]);
        }
        internal static void Prepare(string directory, string repository = Repository)
        {
            var config = ForRepository(repository).Config();
            Verify(directory, config);
            File.WriteAllText(Path.Combine(directory, MNNWhisperConfig.FileName), JsonUtility.ToJson(config, true));
        }
        internal static bool IsComplete(string directory)
        {
            try
            {
                var config = MNNWhisperConfig.Read(directory);
                var sizes = ForConfig(config).Sizes;
                return Assets(config).Select((path, i) =>
                    new FileInfo(MNNZipformerConfig.Resolve(directory, path)).Length == sizes[i]).All(match => match);
            }
            catch (Exception error) when (error is IOException || error is ArgumentException || error is UnauthorizedAccessException)
            {
                return false;
            }
        }
    }
}
