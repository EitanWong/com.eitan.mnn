using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;

namespace MNN.Unity
{
    internal static class MNNKwsModel
    {
        internal const string Repository = "Jeryuk/kws-zipformer-wenetspeech-3.3M-MNN";
        internal const string Revision = "df5d5e80a73b139d19217c46a9c72b6508c98b37";
        internal const string Prefix = "kws-zipformer-wenetspeech-3.3M-MNN/";
        internal const string Encoder = "encoder-epoch-12-avg-2-chunk-16-left-64.mnn";
        internal const string Decoder = "decoder-epoch-12-avg-2-chunk-16-left-64.mnn";
        internal const string Joiner = "joiner-epoch-12-avg-2-chunk-16-left-64.mnn";
        internal const string DefaultKeywords = "小爱同学\n你好问问\n小米小米";

        internal static readonly string[] Paths = {Encoder, Decoder, Joiner, "tokens.txt", "keywords.txt", "README.md", "test_wavs/3.wav", "test_wavs/test_keywords.txt"};
        internal static readonly long[] Sizes = {4394564, 420192, 68428, 1627, 286, 767, 257006, 222};
        internal static readonly string[] Hashes =
        {
            "a14878d14064a62823db811bff0f066bcc11103006e1a7d678e72f8473ed2976",
            "5825b79d848254561eaed2591613f5468a6d2056c6f0e4a3f370aae07dcbca74",
            "5996946e93e7c127cdc633c1c28f3c010b7777a20c25eb06ffac8f21bc2b8199",
            "72316508d9119696145abc6f1f8cdc46287535c34e5ce7e595f845cb1499cf2e",
            "46de8c6834da501b0a2e39a2691b17fd74ea707930dd30fbc7cc69b3d396b93f",
            "dd0ec68b5d9ee736f016fc982078bd3cdbccc69057e9910bdd45580992d13367",
            "710e79fb3450042be50bb2f6432a2a549b53577aea041a577e53a926fcabb614",
            "6af1addaeb80ddb6dc8576e11dcb5a4d17bc02629e6561ca6b2aadb5ec05e27d"
        };

        internal static bool IsRepository(string repository) => string.Equals(repository, Repository, StringComparison.OrdinalIgnoreCase);
        internal static string ResolveRevision(string revision)
        {
            if (string.IsNullOrWhiteSpace(revision) || revision == "main" || revision == Revision) return Revision;
            throw new ArgumentException("KWS requires the verified Wenetspeech export revision.", nameof(revision));
        }

        // The downloaded repository has one nested model folder; a manually prepared flat folder also works.
        internal static string AssetDirectory(string directory) => File.Exists(Path.Combine(directory, Encoder))
            ? directory : Path.Combine(directory, Prefix.TrimEnd('/'));

        internal static void Verify(string directory)
        {
            if (File.Exists(Path.Combine(directory, MNNKeywordSpotterConfig.FileName)) && MNNEnglishKwsModel.IsRepository(MNNKeywordSpotterConfig.Read(directory).repository))
            {
                MNNEnglishKwsModel.Verify(directory);
                return;
            }
            string assets = AssetDirectory(directory);
            for (int i = 0; i < 4; ++i)
                using (var stream = File.OpenRead(Path.Combine(assets, Paths[i])))
                using (var hash = SHA256.Create())
                    if (stream.Length != Sizes[i] || !string.Equals(BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", ""), Hashes[i], StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("KWS asset failed SHA-256 verification: " + Paths[i]);
        }

        internal static void Prepare(string directory)
        {
            Verify(directory);
            // Keep the language of an existing verified English export.
            if (File.Exists(Path.Combine(directory, MNNKeywordSpotterConfig.FileName)) &&
                MNNKeywordSpotterConfig.Read(directory).Language == MNNKeywordLanguage.English) return;
            File.WriteAllText(Path.Combine(directory, MNNKeywordSpotterConfig.FileName), JsonUtility.ToJson(new MNNKeywordSpotterConfig(), true));
        }

        internal static bool IsComplete(string directory)
        {
            try
            {
                if (MNNEnglishKwsModel.IsRepository(MNNKeywordSpotterConfig.Read(directory).repository)) return MNNEnglishKwsModel.IsComplete(directory);
                return IsRepository(MNNKeywordSpotterConfig.Read(directory).repository) &&
                    Paths.Take(4).Select((path, i) => new FileInfo(Path.Combine(AssetDirectory(directory), path)).Length == Sizes[i]).All(value => value);
            }
            catch (Exception error) when (error is IOException || error is ArgumentException || error is UnauthorizedAccessException) { return false; }
        }
    }
}
