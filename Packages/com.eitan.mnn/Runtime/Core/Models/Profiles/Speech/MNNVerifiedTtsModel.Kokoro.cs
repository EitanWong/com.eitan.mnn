using System;

namespace MNN.Unity
{
    internal static partial class MNNVerifiedTtsModel
    {
        internal const string KokoroSingleRepository = "Jeryuk/kokoro-v1_0-MNN";
        internal const string KokoroSingleRevision = "0a8c2982a6fad791eb39746f83ef9482252ceca6";
        internal const string KokoroSingleVoicesHash = "a0ee51e72fbbd0248be187d389908da361f9f230f86f8e32b43d041631980255";
        internal static bool IsKokoroSingleRepository(string repository) => string.Equals(repository, KokoroSingleRepository, StringComparison.OrdinalIgnoreCase);
        internal static FileEntry[] KokoroSingleFiles() => new[]
        {
            new FileEntry("config.json", 190L, "f9e1202bce96ebf3ac10f7e37626b8f4d3832e6498027a138898c0d528eb0f32", KokoroSingleRevision),
            new FileEntry("tokenizer.txt", 655L, "b6e80c7017603809ccd122f05ec49b35e47bc31da4a1468343699a666e368f02", KokoroSingleRevision),
            new FileEntry("tts_config.json", 20L, "fc02b12d79da13aeb8570c42e33b301a50562de3fed22ee29058981fd464d20e", KokoroSingleRevision),
            new FileEntry("tts.mnn", 100515704L, "fd9afbe867934fdccf548106ab10e4e5ade794740ea2607709a8a7d0eea4ae0e", KokoroSingleRevision),
            new FileEntry("voices.mnn", 522476L, "a0ee51e72fbbd0248be187d389908da361f9f230f86f8e32b43d041631980255", KokoroSingleRevision),
            new FileEntry("lexicon-zh.txt", 2365182L, "902bc2d20ac7c449c5ecbbbb23c89d12119c03ed5b8ed27f114f0260c9e35229", "f7b96bb6bef5c5da4d3aa4f4e0498fbbf62dc78b", "csukuangfj/kokoro-multi-lang-v1_0"),
            new FileEntry("lexicon-us-en.txt", 5956885L, "7daaab53a181be9885b853a8582bf1838186317e5dadacbcef9c426d6fa0da14", "f7b96bb6bef5c5da4d3aa4f4e0498fbbf62dc78b", "csukuangfj/kokoro-multi-lang-v1_0"),
            new FileEntry("LICENSE", 11358L, "cfc7749b96f63bd31c3c42b5c471bf756814053e847c10f3eb003417bc523d30", "f7b96bb6bef5c5da4d3aa4f4e0498fbbf62dc78b", "csukuangfj/kokoro-multi-lang-v1_0"),
        };

        internal const string KokoroExpandedRepository = "starsharp06sharp/kokoro-multi-lang-v1.1-MNN-Multilingual";
        internal const string KokoroExpandedRevision = "5270afffc6fc25ffe86a4fde31dc72f3cff45439";
        internal const string KokoroExpandedModelScopeRevision = "719842b796899dc2d2784c87e3859fcbe686245b";
        internal static bool IsKokoroExpandedRepository(string repository) => string.Equals(repository, KokoroExpandedRepository, StringComparison.OrdinalIgnoreCase);
        internal static FileEntry[] KokoroExpandedFiles(bool huggingFace) => new[]
        {
            new FileEntry("config.json", 287L, "fb6452a0c2b88b2eb9b3980c5084bb6e82ecadaa38bbf08221541ebd37b29cf4", huggingFace ? KokoroExpandedRevision : KokoroExpandedModelScopeRevision),
            new FileEntry("model.mnn", 1089920L, "0b826a32e280a87f4d061c6d1530080887b768224657c3c216764ad1326d89e6", huggingFace ? KokoroExpandedRevision : KokoroExpandedModelScopeRevision),
            new FileEntry("model.mnn.weight", 127867017L, "f44d8aba429e86eba368020193ba7267890c52a354cd7f1127bfe30c27e2058a", huggingFace ? KokoroExpandedRevision : KokoroExpandedModelScopeRevision),
            new FileEntry("voices.bin", 81469440L, "26fc7576ad4152c711b7fdf42d022e2ff90781f2744d39b04d077c29773596e9", huggingFace ? KokoroExpandedRevision : KokoroExpandedModelScopeRevision),
            new FileEntry("tokens.txt", 1111L, "931ab2df2400cd65d580a22402024c2347ced8ae9ea300e545144b1aacc48e14", huggingFace ? KokoroExpandedRevision : KokoroExpandedModelScopeRevision),
            new FileEntry("lexicon-zh.txt", 2119371L, "f7f5e982f7cdca50c5f918f83168e8bf3654bde9af563c53040ead9b13c19fd8", huggingFace ? KokoroExpandedRevision : KokoroExpandedModelScopeRevision),
            new FileEntry("lexicon-us-en.txt", 5911422L, "8b53f1a40fbbc93611ea7667ed088b3d22f44e25a533e6dcd625cf41e4395ea2", huggingFace ? KokoroExpandedRevision : KokoroExpandedModelScopeRevision),
            new FileEntry("LICENSE", 11358L, "cfc7749b96f63bd31c3c42b5c471bf756814053e847c10f3eb003417bc523d30", huggingFace ? KokoroExpandedRevision : KokoroExpandedModelScopeRevision),
        };
    }
}
