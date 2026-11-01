using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;

namespace MNN.Unity
{
    internal static class MNNZipformerModel
    {
        internal const string English = "sherpa-mnn-streaming-zipformer-en-2023-02-21";
        internal const string Bilingual = "sherpa-mnn-streaming-zipformer-bilingual-zh-en-2023-02-20";
        internal sealed class File
        {
            internal readonly string Path, Sha256;
            internal readonly long Bytes;
            internal File(string path, long bytes, string sha256) {Path = path; Bytes = bytes; Sha256 = sha256;}
        }
        internal static bool IsRepository(string repository) => repository != null &&
            new[] {"MNN/", "taobao-mnn/"}.Any(owner => repository.Equals(owner + English, StringComparison.OrdinalIgnoreCase) ||
            repository.Equals(owner + Bilingual, StringComparison.OrdinalIgnoreCase));
        internal static string HuggingFaceRevision(string repository) => IsBilingual(repository)
            ? "0b6a216f74d3283d0f83eec2efb421a0f5ba2716" : "3a28149d750a3cde95ae5bfd46956bae790d2503";
        internal static bool IsBilingual(string repository) => repository != null && repository.EndsWith("/" + Bilingual, StringComparison.OrdinalIgnoreCase);
        internal static File[] Files(bool bilingual) => bilingual ? new[]
        {
            new File("encoder-epoch-99-avg-1.int8.mnn",269246668,"db2230d6e794c5d4ee29827fedd86dcf349eeb7194a47f5da1a7a5d837826bb5"),
            new File("decoder-epoch-99-avg-1.int8.mnn",13191808,"63ad2c99be2cacd39870c8b82975e6ae07ab9af61b6efe97a21900cd1c8a5d84"),
            new File("joiner-epoch-99-avg-1.int8.mnn",12835824,"053eecaad11f543269d83454d578485193b23d91f3e6105fcfc8a60345a3d6fb"),
            new File("tokens.txt",56317,"a8e0e4ec53810e433789b54a5c0134a7eaa2ffca595a6334d54c00da858841d3")
        } : new[]
        {
            new File("encoder-epoch-99-avg-1.mnn",293012912,"dff69851ffad8277011a2539c49ffecaa859de2869d720222ab944ead31779b2"),
            new File("decoder-epoch-99-avg-1.mnn",2137600,"9eaaa405369bc2afd1e03f2a7a4a736df6a262119bf4b01c6390fa5a72b5075c"),
            new File("joiner-epoch-99-avg-1.mnn",1028664,"67294833894c745f538eb974249ac9190bf9ee3f36bd990b9fec613803d12083"),
            new File("tokens.txt",5048,"49e3c2646595fd907228b3c6787069658f67b17377c60aeb8619c4551b2316fb")
        };

        internal static MNNZipformerConfig Config(bool bilingual)
        {
            var files = Files(bilingual);
            return new MNNZipformerConfig {encoder = files[0].Path, decoder = files[1].Path, joiner = files[2].Path, tokens = files[3].Path};
        }

        internal static void Verify(string directory, MNNZipformerConfig config, bool bilingual)
        {
            var files = Files(bilingual);
            var paths = new[] {config.encoder, config.decoder, config.joiner, config.tokens};
            for (int i = 0; i < files.Length; ++i)
            {
                using (var stream = System.IO.File.OpenRead(MNNZipformerConfig.Resolve(directory, paths[i])))
                using (var hash = SHA256.Create())
                    if (stream.Length != files[i].Bytes || !string.Equals(BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", ""), files[i].Sha256, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Zipformer profile file failed verification: " + paths[i]);
            }
        }

        internal static void Prepare(string directory, string repository)
        {
            if (!IsRepository(repository)) throw new ArgumentException("Unknown Zipformer repository.", nameof(repository));
            bool bilingual = IsBilingual(repository);
            var config = Config(bilingual);
            Verify(directory, config, bilingual);
            System.IO.File.WriteAllText(Path.Combine(directory, MNNZipformerConfig.FileName), JsonUtility.ToJson(config, true));
        }

        internal static bool IsComplete(string directory)
        {
            try
            {
                var config = MNNZipformerConfig.Read(directory);
                var files = Files(new FileInfo(MNNZipformerConfig.Resolve(directory, config.tokens)).Length == 56317);
                var paths = new[] {config.encoder, config.decoder, config.joiner, config.tokens};
                return paths.Select((path, i) => new FileInfo(MNNZipformerConfig.Resolve(directory, path)).Length == files[i].Bytes).All(match => match);
            }
            catch (Exception error) when (error is IOException || error is ArgumentException || error is UnauthorizedAccessException)
            {
                return false;
            }
        }
    }
}
