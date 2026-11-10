using System;
using System.IO;

namespace MNN.Unity
{
    // Fixed published resources; checksums match the real-inference test fixtures.
    internal static partial class MNNVerifiedTtsModel
    {
        internal sealed class FileEntry
        {
            internal readonly string Path, Hash, Revision, SourceRepository;
            internal readonly long Bytes;
            internal FileEntry(string path, long bytes, string hash, string revision, string sourceRepository = null)
            { Path = path; Bytes = bytes; Hash = hash; Revision = revision; SourceRepository = sourceRepository; }
        }
        internal static bool IsRepository(string repository) => IsCosyVoice300MRepository(repository) || string.Equals(repository, "dr3334/Kokoro-82M-v1.0-MNN", StringComparison.OrdinalIgnoreCase)
            || string.Equals(repository, "dr3334/Supertonic-3-INT8-MNN", StringComparison.OrdinalIgnoreCase) || IsKokoro11Repository(repository) || IsSherpaVitsRepository(repository) || IsMossRepository(repository) || IsKokoroExpandedRepository(repository) || string.Equals(repository, BertVits2CustomRepository, StringComparison.OrdinalIgnoreCase);

        internal const string DhVaaniRepository = "Bbkblo/DhVaani-0.5-MNN";
        internal const string DhVaaniRevision = "9ecccbbba2d6269dc083ab1975800de8b0dbe1c1";
        internal static bool IsDhVaaniRepository(string repository) => string.Equals(repository, DhVaaniRepository, StringComparison.OrdinalIgnoreCase);
        internal const string MossRepository = "kiven2008/moss-tts-nano-100m-mnn";
        internal const string MossCachedRepository = "ZhangZes/MOSS-TTS-Nano-MNN";
        internal static bool IsMossRepository(string repository) => string.Equals(repository, MossRepository, StringComparison.OrdinalIgnoreCase)
            || string.Equals(repository, MossCachedRepository, StringComparison.OrdinalIgnoreCase);
        internal const string SherpaVitsRepository = "KindBrave/sherpa-vits-zh-ll-MNN-Custom";
        internal const string SherpaVitsHuggingFaceRevision = "debc94f4d18e3783ec0d054a8c7401b4c064c511";
        internal const string SherpaVitsModelScopeRevision = "07b16a90eac0b7803692ffc7173b16e3adb40048";
        internal static bool IsSherpaVitsRepository(string repository) => string.Equals(repository, SherpaVitsRepository, StringComparison.OrdinalIgnoreCase);
        internal const string MmsTamilRepository = "developerabu/mms-tts-tam-mnn";
        internal const string MmsTamilRevision = "fb2efd8800e1f7992712aceca63aaa59bc041cd9";
        internal static bool IsMmsTamilRepository(string repository) => string.Equals(repository, MmsTamilRepository, StringComparison.OrdinalIgnoreCase);
        internal const string Kokoro11Repository = "KindBrave/kokoro-multi-lang-v1_1-MNN-Custom";
        internal const string Kokoro11HuggingFaceRevision = "8902386456c12c2c325d14eeb122e257be627966";
        internal const string Kokoro11ModelScopeRevision = "2a80a70abf25777810d89099ae1451f0af253318";
        internal static bool IsKokoro11Repository(string repository) => string.Equals(repository, Kokoro11Repository, StringComparison.OrdinalIgnoreCase);
        internal const string KokoroRevision = "c161c9db569b944c35e06f5c3042e4c575aee4a9";
        internal const string IndicVitsRepository = "developerabu/vits-tts-mnn";
        internal const string IndicVitsRevision = "3487b22ed8492df6218ecd67a1a7af00fed11569";
        internal static bool IsIndicVitsRepository(string repository) => string.Equals(repository, IndicVitsRepository, StringComparison.OrdinalIgnoreCase);
        internal static bool IsHuggingFaceRepository(string repository) => string.Equals(repository, "wangjazz/kokoro-82m-mnn", StringComparison.OrdinalIgnoreCase) || IsIndicVitsRepository(repository) || IsKokoro11Repository(repository) || IsMmsTamilRepository(repository) || IsSherpaVitsRepository(repository) || IsDhVaaniRepository(repository) || IsQwen3ExplicitRepository(repository) || IsPiperLessacRepository(repository) || IsKokoroSingleRepository(repository) || IsBertVits2Repository(repository) || IsKokoroExpandedRepository(repository) || IsPocketRepository(repository) || IsIndicF5Repository(repository);
        internal static string ResolveHuggingFaceRevision(string repository, string revision)
        {
            if (!IsHuggingFaceRepository(repository))
                throw new ArgumentException("Unknown verified Hugging Face TTS repository.", nameof(repository));
            string expected = IsIndicF5Repository(repository) ? IndicF5Revision : IsPocketRepository(repository) ? PocketRevision : IsKokoroExpandedRepository(repository) ? KokoroExpandedRevision : IsBertVits2Repository(repository) ? BertVits2Revision(repository) : IsKokoroSingleRepository(repository) ? KokoroSingleRevision : IsPiperLessacRepository(repository) ? PiperLessacRevision : IsQwen3ExplicitRepository(repository) ? Qwen3ExplicitRevision : IsDhVaaniRepository(repository) ? DhVaaniRevision : IsSherpaVitsRepository(repository) ? SherpaVitsHuggingFaceRevision : IsMmsTamilRepository(repository) ? MmsTamilRevision : IsIndicVitsRepository(repository) ? IndicVitsRevision : IsKokoro11Repository(repository) ? Kokoro11HuggingFaceRevision : KokoroRevision;
            if (string.IsNullOrWhiteSpace(revision) || revision == "main" || revision == "master" || revision == expected)
                return expected;
            throw new ArgumentException("This TTS frontend is verified with revision " + expected + ".", nameof(revision));
        }
        internal static FileEntry[] HuggingFaceFiles(string repository)
        {
            if (!IsHuggingFaceRepository(repository))
                throw new ArgumentException("Unknown verified Hugging Face TTS repository.", nameof(repository));
            if (IsIndicF5Repository(repository)) return IndicF5Files();
            if (IsPocketRepository(repository)) return PocketFiles();
            if (IsKokoroExpandedRepository(repository)) return KokoroExpandedFiles(true);
            if (IsBertVits2Repository(repository)) return BertVits2Files(true, BertVits2Revision(repository));
            if (IsKokoroSingleRepository(repository)) return KokoroSingleFiles();
            if (IsPiperLessacRepository(repository)) return PiperLessacFiles();
            if (IsQwen3ExplicitRepository(repository)) return Qwen3ExplicitFiles();
            if (IsDhVaaniRepository(repository))
                return new[]
                {
                    new FileEntry("README.md", 5767L, "44cf24c205d8bfdb9656fbebb83ffdf6d60b200ca515cfaf41137138ce506277", DhVaaniRevision),
                    new FileEntry("model.json", 697L, "6aa1712ffa270a6f01885bdb51b2f466cf758e9489f62146faeb4113fc27dfaf", DhVaaniRevision),
                    new FileEntry("tokens.txt", 8065L, "5ab10f795d0231bcc800defc1213cec4d0bf59b840711daa67e51ab318b38cf8", DhVaaniRevision),
                    new FileEntry("mel_fb.bin", 209324L, "11ac0227ea00a0b7e8722cd73711b5beac95971362e26404e8d9b3ea62235ddd", DhVaaniRevision),
                    new FileEntry("vocos_head.bin", 2109476L, "dee985ae540e38f47f4cf6d5b4d6dc0916244e3b47ea72aabb6aa9cef31533d5", DhVaaniRevision),
                    new FileEntry("text_encoder_int8.mnn", 6059280L, "8c0fbd7879696a9d684e3cbe692ac2b0c270f3e1602bc28408bfef68a0444087", DhVaaniRevision),
                    new FileEntry("fm_decoder_int8.mnn", 123923996L, "82db32d59c9bb704f3960db3601660d5526ddc3c2aec7e5c61f923c56ce6144d", DhVaaniRevision),
                    new FileEntry("vocoder_backbone.mnn", 13333808L, "3334fd7a07b61b794fa540d1b1e8b738ac79b062128768c6229eaf9dffabfd8c", DhVaaniRevision),
                };
            if (IsSherpaVitsRepository(repository))
                return SherpaVitsFiles(SherpaVitsHuggingFaceRevision);
            if (IsMmsTamilRepository(repository))
                return new[]
                {
                    new FileEntry("model_int8.mnn", 29605316L, "435d49e8861e38fd4c633e12e36d87116f5cd90a5ae8d5e1b8b81ce0e3d389ec", MmsTamilRevision),
                    new FileEntry("model_fp16_int8.mnn", 57404572L, "98f86c685357d918b47364080135e980d7feed40125d809acdd24a445248b479", MmsTamilRevision),
                    new FileEntry("model_fp32.mnn", 113845016L, "e9d249803863368121974f1063ab751a2b56f0271305151f88ebd78092b0c272", MmsTamilRevision),
                    new FileEntry("tokens.txt", 375L, "0b3f692319bb5fae8658e2f84bf252bca92450d0207bbba7273caa1a182d81b8", MmsTamilRevision),
                    new FileEntry("README.md", 31L, "4bcf87ecfbbb8e07a01b21415a970c8b53a5283bf6872b657040d3f45c9241f7", MmsTamilRevision),
                };
            if (IsKokoro11Repository(repository))
                return Kokoro11Files(Kokoro11HuggingFaceRevision);
            if (IsIndicVitsRepository(repository))
                return new[]
                {
                    new FileEntry("config.json", 1918L, "1dd3f583ef2fbf8c19a1bd5a88c8d47e37c239a05c5efd15a8bb1aefcc9132c6", IndicVitsRevision),
                    new FileEntry("tokenizer_config.json", 795L, "eddd1a1b2d50f071689f523a34ce669a3038adddf18a4d570cb5998bfc055184", IndicVitsRevision),
                    new FileEntry("special_tokens_map.json", 275L, "8564e43c4c8c156b8fbe53c330bba8cef4f79b8e60550825421d8979abe19e64", IndicVitsRevision),
                    new FileEntry("vocab.json", 17178L, "f99ef079d2f8bed7658d51184146356727d950672b1221d7bcb11c4c5d5ac79a", IndicVitsRevision),
                    new FileEntry("vits_tts_q8.mnn", 33582668L, "6ec50252ed4ba056426ca2f0156a94528bdb78e8ea28b9df0c9c96096b522995", IndicVitsRevision),
                    new FileEntry("README.md", 4130L, "a109a7f040cece79316b0e0249b2f8d68f1be55e1a4242fabbccab44b1790c5e", IndicVitsRevision),
                };
            return new[]
            {
                new FileEntry("config.json", 190L, "f9e1202bce96ebf3ac10f7e37626b8f4d3832e6498027a138898c0d528eb0f32", KokoroRevision),
                new FileEntry("tts_config.json", 1156L, "6dda828eca8b414202846bab1edbb3b53a892146706b413a2c4f32a362dc821c", KokoroRevision),
                new FileEntry("tokenizer.txt", 655L, "b6e80c7017603809ccd122f05ec49b35e47bc31da4a1468343699a666e368f02", KokoroRevision),
                new FileEntry("tts.mnn", 100515712L, "763b51ae426cc5020fb55072d415591100dcc057ed4acb2d8efcba9ae3b5b6c6", KokoroRevision),
                new FileEntry("voices.mnn", 28731100L, "0a965c346f81b48e10d454f8280c42c5fcf304d0879ef1a53fb80715c7254534", KokoroRevision),
                new FileEntry("lexicon-zh.txt", 2365182L, "902bc2d20ac7c449c5ecbbbb23c89d12119c03ed5b8ed27f114f0260c9e35229", "f7b96bb6bef5c5da4d3aa4f4e0498fbbf62dc78b", "csukuangfj/kokoro-multi-lang-v1_0"),
                new FileEntry("lexicon-us-en.txt", 5956885L, "7daaab53a181be9885b853a8582bf1838186317e5dadacbcef9c426d6fa0da14", "f7b96bb6bef5c5da4d3aa4f4e0498fbbf62dc78b", "csukuangfj/kokoro-multi-lang-v1_0"),
                new FileEntry("LICENSE", 11358L, "cfc7749b96f63bd31c3c42b5c471bf756814053e847c10f3eb003417bc523d30", "f7b96bb6bef5c5da4d3aa4f4e0498fbbf62dc78b", "csukuangfj/kokoro-multi-lang-v1_0"),
            };
        }

        internal static FileEntry[] Files(string repository)
        {
            if (IsCosyVoice300MRepository(repository)) return CosyVoice300MFiles();
            if (IsKokoroExpandedRepository(repository)) return KokoroExpandedFiles(false);
            if (string.Equals(repository, BertVits2CustomRepository, StringComparison.OrdinalIgnoreCase)) return BertVits2Files(false);
            if (IsMossRepository(repository)) return string.Equals(repository, MossCachedRepository, StringComparison.OrdinalIgnoreCase) ? MossCachedFiles() : MossFiles();
            if (IsSherpaVitsRepository(repository))
                return SherpaVitsFiles(SherpaVitsModelScopeRevision);
            if (IsKokoro11Repository(repository))
                return Kokoro11Files(Kokoro11ModelScopeRevision);
            if (string.Equals(repository, "dr3334/Kokoro-82M-v1.0-MNN", StringComparison.OrdinalIgnoreCase))
                return new[]
                {
                    new FileEntry("FRONTEND-LICENSE", 11358L, "cfc7749b96f63bd31c3c42b5c471bf756814053e847c10f3eb003417bc523d30", "128dd7387392d33c82e63b51f898750dd48d554b"),
                    new FileEntry("MODEL-LICENSE", 11358L, "cfc7749b96f63bd31c3c42b5c471bf756814053e847c10f3eb003417bc523d30", "128dd7387392d33c82e63b51f898750dd48d554b"),
                    new FileEntry("NOTICE.txt", 3262L, "55f1d9d279d5192d659d19fda42b03b4231266f7af1da1e0904a7afc83cce845", "e7f9c985f3118427cb6646b9962ee4b91c536b0f"),
                    new FileEntry("README.md", 1845L, "44792ed8e235e1a741e6a6d9ae1adc30194469bdfc5911c8e92cc82f85545f62", "128dd7387392d33c82e63b51f898750dd48d554b"),
                    new FileEntry("lexicon-us-en.txt", 5956885L, "7daaab53a181be9885b853a8582bf1838186317e5dadacbcef9c426d6fa0da14", "128dd7387392d33c82e63b51f898750dd48d554b"),
                    new FileEntry("lexicon-zh.txt", 2365182L, "902bc2d20ac7c449c5ecbbbb23c89d12119c03ed5b8ed27f114f0260c9e35229", "128dd7387392d33c82e63b51f898750dd48d554b"),
                    new FileEntry("manifest.json", 2002L, "d05ca6a2d5cb5665a28adbb1e3bd322f49aabe3fb03d68aa5f103317dcc8b9ff", "128dd7387392d33c82e63b51f898750dd48d554b"),
                    new FileEntry("tokenizer.txt", 655L, "b6e80c7017603809ccd122f05ec49b35e47bc31da4a1468343699a666e368f02", "128dd7387392d33c82e63b51f898750dd48d554b"),
                    new FileEntry("tts.mnn", 100515712L, "763b51ae426cc5020fb55072d415591100dcc057ed4acb2d8efcba9ae3b5b6c6", "128dd7387392d33c82e63b51f898750dd48d554b"),
                    new FileEntry("upstream-tts-config.json", 1156L, "6dda828eca8b414202846bab1edbb3b53a892146706b413a2c4f32a362dc821c", "128dd7387392d33c82e63b51f898750dd48d554b"),
                    new FileEntry("voices/zf_xiaoxiao.bin", 522240L, "03adb5d5e3ddd88b047954e974e651cb0a4b524c985057e5d872e962c7be1169", "128dd7387392d33c82e63b51f898750dd48d554b"),
                    new FileEntry("voices/zm_yunxi.bin", 522240L, "7243892fb4e560d47014090ddf010f8b8b790f3c6b029ff82b2ac06aa4e27c8b", "128dd7387392d33c82e63b51f898750dd48d554b"),
                };
            else if (string.Equals(repository, "dr3334/Supertonic-3-INT8-MNN", StringComparison.OrdinalIgnoreCase))
                return new[]
                {
                    new FileEntry("LICENSE", 15007L, "0d944a9110fed9a9602d60e0423a272903e7bd21ab060490774efc77c2275e9f", "f01461d731741d3fa4d97678e9ed8ae8b8cdba9d"),
                    new FileEntry("NOTICE.txt", 3262L, "55f1d9d279d5192d659d19fda42b03b4231266f7af1da1e0904a7afc83cce845", "1dfd5693db1b62b2c49331b7674f9dee5c69385a"),
                    new FileEntry("README.md", 1353L, "54c8a6c686d03fc2a3efdbac47c4a602b39914355da76c8b803aab09e7898e28", "f01461d731741d3fa4d97678e9ed8ae8b8cdba9d"),
                    new FileEntry("manifest.json", 2893L, "2d37d384ff252bd430bf5a2c698eac5ae96fe03fea597a191f8b6a1b4dac21cf", "f01461d731741d3fa4d97678e9ed8ae8b8cdba9d"),
                    new FileEntry("mnn_models/int8/duration_predictor.mnn", 2682596L, "a3178a72cdc95e1f5584fedb5e4f8507d0ad65036237f4082a7073636625f130", "f01461d731741d3fa4d97678e9ed8ae8b8cdba9d"),
                    new FileEntry("mnn_models/int8/text_encoder.mnn", 15907604L, "2b57b1f902821b73d28168771786c01f0df164b9c137b87da5c55299d2164975", "f01461d731741d3fa4d97678e9ed8ae8b8cdba9d"),
                    new FileEntry("mnn_models/int8/vector_estimator.mnn", 65770972L, "61fdfab87e394532f384d171d3fb4c4ea5ecb5fadce6a146922a6b8472efe9eb", "f01461d731741d3fa4d97678e9ed8ae8b8cdba9d"),
                    new FileEntry("mnn_models/int8/vocoder.mnn", 25805752L, "acfbc34038f8f9776e5a7efc83cd502e6764cc133177ee2618198c9c469a445d", "f01461d731741d3fa4d97678e9ed8ae8b8cdba9d"),
                    new FileEntry("mnn_models/tts.json", 8253L, "42078d3aef1cd43ab43021f3c54f47d2d75ceb4e75f627f118890128b06a0d09", "f01461d731741d3fa4d97678e9ed8ae8b8cdba9d"),
                    new FileEntry("mnn_models/unicode_indexer.json", 277676L, "9bf7346e43883a81f8645c81224f786d43c5b57f3641f6e7671a7d6c493cb24f", "f01461d731741d3fa4d97678e9ed8ae8b8cdba9d"),
                    new FileEntry("voice_styles/F1.json", 292046L, "bbdec6ee00231c2c742ad05483df5334cab3b52fda3ba38e6a07059c4563dbc2", "f01461d731741d3fa4d97678e9ed8ae8b8cdba9d"),
                    new FileEntry("voice_styles/F2.json", 292423L, "7c722c6a72707b1a77f035d67f0d1351ba187738e06f7683e8c72b1df3477fc6", "f01461d731741d3fa4d97678e9ed8ae8b8cdba9d"),
                    new FileEntry("voice_styles/F3.json", 290794L, "12f6ef2573baa2defa1128069cb59f203e3ab67c92af77b42df8a0e3a2f7c6ab", "f01461d731741d3fa4d97678e9ed8ae8b8cdba9d"),
                    new FileEntry("voice_styles/F4.json", 291808L, "c2fa764c1225a76dfc3e2c73e8aa4f70d9ee48793860eb34c295fff01c2e032b", "f01461d731741d3fa4d97678e9ed8ae8b8cdba9d"),
                    new FileEntry("voice_styles/F5.json", 291479L, "45966e73316415626cf41a7d1c6f3b4c70dbc1ba2bee5c1978ef0ce33244fc8d", "f01461d731741d3fa4d97678e9ed8ae8b8cdba9d"),
                    new FileEntry("voice_styles/M1.json", 291748L, "e35604687f5d23694b8e91593a93eec0e4eca6c0b02bb8ed69139ab2ea6b0a5b", "f01461d731741d3fa4d97678e9ed8ae8b8cdba9d"),
                    new FileEntry("voice_styles/M2.json", 292055L, "b76cbf62bac707c710cf0ae5aba5e31eea1a6339a9734bfae33ab98499534a50", "f01461d731741d3fa4d97678e9ed8ae8b8cdba9d"),
                    new FileEntry("voice_styles/M3.json", 290198L, "ea1ac35ccb91b0d7ecad533a2fbd0eec10c91513d8951e3b25fbba99954e159b", "f01461d731741d3fa4d97678e9ed8ae8b8cdba9d"),
                    new FileEntry("voice_styles/M4.json", 291522L, "ca8eefad4fcd989c9379032ff3e50738adc547eeb5e221b82593a6d7b3bac303", "f01461d731741d3fa4d97678e9ed8ae8b8cdba9d"),
                    new FileEntry("voice_styles/M5.json", 291469L, "dd22b92740314321f8ae11c5e87f8dd60d060f15dd3a632b5adf77f471f77af2", "f01461d731741d3fa4d97678e9ed8ae8b8cdba9d"),
                };
            throw new ArgumentException("Unknown verified TTS repository.", nameof(repository));
        }

        private static FileEntry[] MossCachedFiles() => new[]
        {
            new FileEntry("README.md", 1377L, "0d435c3b0c6d496aa7a790751de61b108c85a2ebb940781e633c55f8338c4783", "159c7a751a527d0f3bc23024f9ff1a48e87c77df"),
            new FileEntry("audio_tokenizer/codec_browser_onnx_meta.json", 16461L, "c40eb144a4f50453ac0437944cbdeee1467845b886e90197ff1eada1353303db", "32feeaa86b2be7a0fa3c743b866a51be702a9570"),
            new FileEntry("audio_tokenizer/moss_audio_tokenizer_decode_full.mnn", 22696992L, "b03ef4bedcc15296ad23de8de3a931c1bec148e947441801c11b45f58c7bb88b", "32feeaa86b2be7a0fa3c743b866a51be702a9570"),
            new FileEntry("tts/browser_poc_manifest.json", 470720L, "803716e58b71fe2a770af1495ca74797d38c181b41cfeeaf9e524adc15910a20", "32feeaa86b2be7a0fa3c743b866a51be702a9570"),
            new FileEntry("tts/moss_tts_decode_step.mnn", 220868604L, "668a76e746f51035286ccab8a0008ac0470187b7ed7d31d136bc0b8d2000217e", "32feeaa86b2be7a0fa3c743b866a51be702a9570"),
            new FileEntry("tts/moss_tts_local_cached_step.mnn", 114914088L, "cee3a10ff98b62680e7172658f2dcddebcfe3cc72eec2594997d1deeb69253da", "32feeaa86b2be7a0fa3c743b866a51be702a9570"),
            new FileEntry("tts/moss_tts_prefill_nocumsum.mnn", 220855400L, "849553c9b7963471112bb7519dba3a969ad46f417993098c31319a42304465c4", "32feeaa86b2be7a0fa3c743b866a51be702a9570"),
            new FileEntry("tts/tokenizer.model", 470897L, "c353ee1479b536bf414c1b247f5542b6607fb8ae91320e5af1781fee200fddff", "32feeaa86b2be7a0fa3c743b866a51be702a9570"),
            new FileEntry("tts/tts_browser_onnx_meta.json", 4302L, "1dacbf4d59732ae1fb1253139b70a4ca07415246fb6473919c9947d489a59e04", "80051dd85b8e1ddd1f5121b1842b0ec6a40506c0"),
        };

        private static FileEntry[] MossFiles() => new[]
        {
            new FileEntry("README.md", 1381L, "a6bd4cb968deebd50c57f2406f21f643aa26876893b0d02d34496975895e9020", "b2c536bca763fb4f311ae82616f68148d3d08181"),
            new FileEntry("SHA256SUMS.txt", 991L, "eb44a7689ae68bded68c7d17065d3534e92b718a962b39043283d7de9d166676", "333f53df1ae6da764d3f7575d32023b60eecaee7"),
            new FileEntry("browser_poc_manifest.json", 503354L, "097d80e993dc29f0bae427590b4f77084a161cb578b50d82c29f455d5faa9eee", "8439c3521b301f2ad57a3ffe5a0fb3fcbef3881e"),
            new FileEntry("codec_browser_onnx_meta.json", 16461L, "c40eb144a4f50453ac0437944cbdeee1467845b886e90197ff1eada1353303db", "9a5c7c51d533496b0ced0c13596cae76adbd6152"),
            new FileEntry("moss_audio_tokenizer_decode_full.mnn", 44967304L, "59958809bca8ee8217ed1df6e5b63c4425868e20897e411d2e08363e9631a180", "c98cdb0ea7d5fc4b2061839ba75a979978c81679"),
            new FileEntry("moss_tts_decode_step.mnn", 441297272L, "da5a9c21d4af2ecf641134c3defe100df1ef9c88782f28d9f0c6d7df01e9b71b", "da3da388295b3044200b4cde1a9e1729359d0c0f"),
            new FileEntry("moss_tts_local_fixed_sampled_frame.mnn", 255898648L, "744c301b38e58788c1c783beea38cd5eebd3154ad9c97247a202cc3bfbed49bc", "1eeefc9c7fbc822f389c6d6a32c4303611f25b4f"),
            new FileEntry("moss_tts_prefill.mnn", 441284956L, "c4381e27092d1c2c11071ef7b36e701d07a3776170e8593924dcf757221cd58e", "7172ef28b9ad46e9dfaafd2fcd542a12270f5aee"),
            new FileEntry("tokenizer_mnn.txt", 273014L, "7bcd543054175ab42886e604c0183db756725cf00b0e104590f9e3171d7f8840", "76b6746ae7837a04f73088b70e5bb845ccefb249"),
            new FileEntry("tts_browser_onnx_meta.json", 4302L, "1dacbf4d59732ae1fb1253139b70a4ca07415246fb6473919c9947d489a59e04", "a3d2b628a418b7a9e0fb4e9d9ac80b4fe5c956f5"),
        };

        private static FileEntry[] SherpaVitsFiles(string revision) => new[]
        {
            new FileEntry("G_multisperaker_latest.json", 2352L, "f31e4bf23827c3528fdf090fd7b6fb8e63333709b80670d40fa864f1fa9fadf3", revision),
            new FileEntry("config.json", 190L, "74ecd62019edefab418a0f34e7b6ff8adf094ca0ea2f0e0c50881c878d651261", revision),
            new FileEntry("lexicon.txt", 376868L, "b3a82f16b286c424953dea3686039e7ab465fa8e15d87ef8abd0ec69175beb21", revision),
            new FileEntry("model.mnn", 800144L, "b44fb2bfd609135cefc69e7be27da96f840f461d220d19b2f0fa558316e3e62b", revision),
            new FileEntry("model.mnn.weight", 31461366L, "2204aca875fc68706633b0e575ac23c935a40ea2c4067ec7d8c4a14d914b8939", revision),
            new FileEntry("tokens.txt", 331L, "34b035b9aeb070df6188b022f29c00e0e142c7ade9f25611ced65db5e9cc8402", revision),
        };

        private static FileEntry[] Kokoro11Files(string revision) => new[]
        {
            new FileEntry("config.json", 286L, "c1950f3680c3d674ce33129f1b566c41f953da6f587a31fbd9b5cf1e6feb9d9c", revision),
            new FileEntry("model.mnn", 1031152L, "8b03d24715947721dc980d4bd32c9cf3426867293915503822d79f119bf4258b", revision),
            new FileEntry("model.mnn.weight", 139303601L, "40dd783a8c21a1f83aba1adedf917078f1a3984ad703faa3a07304fcaec83e89", revision),
            new FileEntry("voices.bin", 53790720L, "e64a5a581d8c2a350d848f51c3121657cd83aa07ed6109172177345874a7244c", revision),
            new FileEntry("tokens.txt", 1111L, "931ab2df2400cd65d580a22402024c2347ced8ae9ea300e545144b1aacc48e14", revision),
            new FileEntry("lexicon-us-en.txt", 5911422L, "8b53f1a40fbbc93611ea7667ed088b3d22f44e25a533e6dcd625cf41e4395ea2", revision),
            new FileEntry("lexicon-zh.txt", 2119371L, "f7f5e982f7cdca50c5f918f83168e8bf3654bde9af563c53040ead9b13c19fd8", revision),
            new FileEntry("LICENSE", 11358L, "cfc7749b96f63bd31c3c42b5c471bf756814053e847c10f3eb003417bc523d30", revision),
        };

        internal static void Prepare(string directory, string repository)
        {
            Validate(directory, repository);
            if (IsIndicF5Repository(repository))
                File.WriteAllText(Path.Combine(directory, "INDICF5-SOURCE-NOTICE.txt"),
                    "MNN export: https://huggingface.co/" + IndicF5Repository + "/tree/" + IndicF5Revision + "\n" +
                    "Base: ai4bharat/IndicF5, MIT; managed frontend/sampler follows AI4Bharat IndicF5 and SWivid F5-TTS.\n" +
                    "Vocabulary/license/config: https://huggingface.co/" + IndicF5FrontendRepository + "/tree/" + IndicF5FrontendRevision + "\n" +
                    "Vocabulary Git blob matches official checkpoints/vocab.txt: 65db6313e8830d8a31b2ae46f15d8487456c1a8d.\n" +
                    "24 kHz mono, reference WAV plus exact transcript required. At most 12 seconds reference and 2048 total mel frames.\n" +
                    "Bundled reference samples/hi_short_mnn_vocos_fp32.wav transcript, checked by independent ASR: नमस्ते, आप कैसे हैं?\n" +
                    "Original graph files preserved; only public serialized position tables/input dimensions adapted in memory. CPU / macOS Mono ABI.\n");
            if (IsPocketRepository(repository))
                File.WriteAllText(Path.Combine(directory, "POCKET-SOURCE-NOTICE.txt"),
                    "MNN export: https://huggingface.co/" + PocketRepository + "/tree/" + PocketRevision + "\n" +
                    "Original model: Kyutai Labs Pocket TTS english_2026-04, CC-BY-4.0 (https://creativecommons.org/licenses/by/4.0/).\n" +
                    "ONNX export: IgnitiveLabs/PocketTTS-ONNX. Managed pipeline follows developerabu and KevinAHM/pocket-tts-onnx.\n" +
                    "This package runs all five MNN stages, restores two exported Shape slices and specializes empty-sequence concatenation. Original model files are preserved.\n" +
                    "English, 24 kHz mono, up to 50 tokens / 30 seconds; raw reference WAV supported. Gated predefined voice embeddings are not downloaded.\n");
            if (IsSherpaVitsRepository(repository))
                File.WriteAllText(Path.Combine(directory, "VITS-SOURCE-NOTICE.txt"),
                    "MNN export: https://huggingface.co/" + SherpaVitsRepository + "/tree/" + SherpaVitsHuggingFaceRevision + "\n" +
                    "Publisher credits https://github.com/Plachtaa/VITS-fast-fine-tuning for training.\n" +
                    "The package does not supply the repository's Jieba or FST frontends. Numbers must be written as Chinese words; polyphonic words follow the installed lexicon.\n");
            if (IsMmsTamilRepository(repository))
            {
                File.WriteAllText(Path.Combine(directory, "mnn_mms_tamil.json"), "{\"schema\":1,\"model_type\":\"mms-tamil\",\"sample_rate\":16000,\"revision\":\"" + MmsTamilRevision + "\"}");
                File.WriteAllText(Path.Combine(directory, "MMS-SOURCE-NOTICE.txt"),
                    "MNN export: https://huggingface.co/" + MmsTamilRepository + "/tree/" + MmsTamilRevision + "\n" +
                    "Original model: facebook/mms-tts-tam, revision e9cf59dae34f0f51e3b1842876a658e4516f9fe4\n" +
                    "Original model license: CC-BY-NC-4.0 (https://creativecommons.org/licenses/by-nc/4.0/)\n" +
                    "The export repository's Apache-2.0 metadata does not replace the original model license.\n");
            }
        }

        internal static void Validate(string directory, string repository)
        {
            foreach (var file in IsHuggingFaceRepository(repository) ? HuggingFaceFiles(repository) : Files(repository))
            {
                string path = System.IO.Path.Combine(directory, file.Path);
                if (!File.Exists(path) || new FileInfo(path).Length != file.Bytes)
                    throw new InvalidDataException("Incomplete TTS download: " + file.Path);
            }
            if (IsCosyVoice300MRepository(repository))
            {
                if (!MNNCosyVoice300MModel.IsComplete(directory)) throw new InvalidDataException("Incomplete CosyVoice-300M-MNN stages, compact decoder or reference assets.");
            }
            else if (IsBertVits2Repository(repository))
            {
                if (MNNTtsModelProfiles.Detect(directory) != MNNSpeechModelKind.BertVits2) throw new InvalidDataException("Missing Bert-VITS2 configuration.");
            }
            else if (IsPiperLessacRepository(repository))
            {
                MNNPiper.ResolveDataDirectory(directory);
                if (MNNPiper.InstalledVoices(directory).Length != 1) throw new InvalidDataException("Incomplete Piper Lessac voice assets.");
            }
            else if (IsIndicF5Repository(repository))
            {
                if (!MNNIndicF5Tts.IsComplete(directory)) throw new InvalidDataException("Incomplete IndicF5 stages or vocabulary.");
            }
            else if (IsPocketRepository(repository))
            {
                if (!MNNPocketTts.IsComplete(directory)) throw new InvalidDataException("Incomplete Pocket-TTS stages or frontend.");
            }
            else if (IsQwen3ExplicitRepository(repository))
            {
                if (!MNNQwen3ExplicitTts.IsComplete(directory)) throw new InvalidDataException("Incomplete Qwen3 explicit KV assets.");
            }
            else if (IsDhVaaniRepository(repository))
            {
                if (!MNNDhVaani.IsComplete(directory)) throw new InvalidDataException("Incomplete DhVaani stages or frontend.");
            }
            else if (IsMossRepository(repository))
            {
                if (!MNNMossNano.IsComplete(directory)) throw new InvalidDataException("Incomplete MOSS Nano stages or frontend.");
            }
            else if (IsSherpaVitsRepository(repository))
            {
                if (!MNNSherpaVitsChinese.IsComplete(directory))
                    throw new InvalidDataException("Missing Chinese VITS graph, weights or frontend.");
            }
            else if (IsMmsTamilRepository(repository))
            {
                if (!File.Exists(Path.Combine(directory, "model_int8.mnn")) || !File.Exists(Path.Combine(directory, "tokens.txt")))
                    throw new InvalidDataException("Missing MMS Tamil graph or character tokens.");
            }
            else if (IsIndicVitsRepository(repository))
            {
                if (!MNNIndicVits.IsComplete(directory))
                    throw new InvalidDataException("Missing Indic VITS graph or frontend.");
            }
            else if (repository.IndexOf("Kokoro", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (!MNNKokoroModel.IsComplete(directory))
                    throw new InvalidDataException("Missing Kokoro frontend or voices.");
            }
            else if (!MNNSupertonic.IsComplete(directory))
                throw new InvalidDataException("Missing Supertonic stage or voices.");
        }
    }
}
