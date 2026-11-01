using System;

namespace MNN.Unity
{
    internal static partial class MNNVerifiedTtsModel
    {
        internal const string CosyVoice300MRepository = "EitanWang/CosyVoice-300M-MNN";
        internal const string CosyVoice300MRevision = "cc83ebcf27d261dad7da6f3b79303a083c942004";
        internal static bool IsCosyVoice300MRepository(string repository)
            => string.Equals(repository, CosyVoice300MRepository, StringComparison.OrdinalIgnoreCase);
        internal static string ResolveCosyVoice300MRevision(string revision)
        {
            if (string.IsNullOrWhiteSpace(revision) || revision == "master" || revision == "main" || revision == CosyVoice300MRevision)
                return CosyVoice300MRevision;
            throw new ArgumentException("CosyVoice-300M-MNN requires its verified published revision.", nameof(revision));
        }
        internal static FileEntry[] CosyVoice300MFiles() => new[]
        {
            new FileEntry("LICENSE", 11357L, "c71d239df91726fc519c6eb72d318ec65820627232b2f796219e87dcf35d0ab4", CosyVoice300MRevision),
            new FileEntry("NOTICE.md", 865L, "0063e9e5862cba9bc2f8e1e44cc8d551eae5af4001d2e3151cd009eba3548ff6", CosyVoice300MRevision),
            new FileEntry("README.md", 4373L, "785f49c0d86161b911053381218159e7d212742f99db938253a623207cae015d", CosyVoice300MRevision),
            new FileEntry("TOKENIZER-LICENSE", 1078L, "418cb499b436128d653d79941333a5437b7be2ea9213dcc2f04d15d5d2c51d86", CosyVoice300MRevision),
            new FileEntry("common/flow_speaker.npz", 62358L, "41313bd21707bf183f98f855c2141f5d5306852969a2393e98a8ce2a31ec348a", CosyVoice300MRevision),
            new FileEntry("common/hift_source.npz", 546L, "6895901d352ab375627e0fc6d3723a3eca6466dbab3543caf02a425b120e000d", CosyVoice300MRevision),
            new FileEntry("common/llm_embeddings.npz", 17577084L, "c423d351f6bcffc5f9832d3507b71e969e991b6bad9ab85d5eab53189f278ad9", CosyVoice300MRevision),
            new FileEntry("common/projected_positions.npy", 234823808L, "1f48b7c5c022bb0d140bd8c1e2825ad93ee73169fb6c46c9f266a933f9f477c7", CosyVoice300MRevision),
            new FileEntry("common/prompt.npz", 98226L, "f2faf984197a838e9dedc109aa3dbcf65b30ba631227a5e0d68e7eaed4b2ab51", CosyVoice300MRevision),
            new FileEntry("common/whisper-special-tokens.json", 34648L, "c5f8e4220fd1c512f9df765292df30a8c1629d4cd9a4f5023543370e260cfef4", CosyVoice300MRevision),
            new FileEntry("common/whisper.tiktoken", 816730L, "b34b360dbb493e781e479794586d661700670d65564001f23024971d1f2fa126", CosyVoice300MRevision),
            new FileEntry("configuration.json", 158L, "f5e6637adf0e2f2448fc3a042fcfd9654ad818690f26700cd58b5f43cf514622", CosyVoice300MRevision),
            new FileEntry("fp16/flow_encoder.mnn", 14636576L, "c76e0459604752194dd7dffab971b3e4754d3553aa2a1529390f8255c4c94eab", CosyVoice300MRevision),
            new FileEntry("fp16/flow_encoder.mnn.weight", 41683264L, "1c16d4799690105eedce3a447d7dd1b999acb41fef156ef7b1eaf3e02d1371fc", CosyVoice300MRevision),
            new FileEntry("fp16/flow_estimator.mnn", 1552248L, "223a95689fb03fdd3de14432267c284a10a192936eceb15f36486154a083e6a5", CosyVoice300MRevision),
            new FileEntry("fp16/flow_estimator.mnn.weight", 164484416L, "8fa3123613c7fd7da47579122ea88934316ef39c0e48930dc72b5b3e90d628b6", CosyVoice300MRevision),
            new FileEntry("fp16/flow_regulator.mnn", 12064L, "ec33fc2fc993a9ebe754c3b0adc9cc2c70297b5a98e1712eb2b015a955d1d404", CosyVoice300MRevision),
            new FileEntry("fp16/flow_regulator.mnn.weight", 168000L, "7593194f25808dcd2116a26f4d57b02c4dc018aa080e41886c97e781457da4b1", CosyVoice300MRevision),
            new FileEntry("fp16/hift_core.mnn", 114920L, "e107a54b31fb3b1cb8fcbef0d179a28014151ebf2a4dd65454380386735bed11", CosyVoice300MRevision),
            new FileEntry("fp16/hift_core.mnn.weight", 34354248L, "f32c67f90a4749289736734679d888e6f9c0cceca09b70a1aafa44bdef3df917", CosyVoice300MRevision),
            new FileEntry("fp16/hift_f0.mnn", 5680L, "0a23e368811ff00a530455906c69102a494a84d2afe8c051df216c40f96b4658", CosyVoice300MRevision),
            new FileEntry("fp16/hift_f0.mnn.weight", 6548484L, "6acbf64ad89c5842c6fcb6f4f12057352ae8efa3615f8f304f87cad9852f3e89", CosyVoice300MRevision),
            new FileEntry("fp16/llm_cached_projected.mnn", 448008L, "282a7a70ee266ff8789be27d89bf0d611e2ba9c08b3f36a3b4b54fa394a6189c", CosyVoice300MRevision),
            new FileEntry("fp16/llm_cached_projected.mnn.weight", 363591684L, "71786ad1d9fa6915d772a325f6f2824f636e7657d71a1cd72eb507c948354b46", CosyVoice300MRevision),
            new FileEntry("fp16/llm_decode_compact.mnn", 234160L, "46a6e36fb42012f0512e2d666d963934b54a3f6944ba83f3fba05cee110ea3ad", CosyVoice300MRevision),
            new FileEntry("fp16/llm_decode_compact.mnn.weight", 363591684L, "d7cd54a1f38abc18a877ee32e956fed616da57589133a5954dd77f9d55c220b8", CosyVoice300MRevision),
            new FileEntry("fp16/text_encoder.mnn", 73796248L, "aad3c0f1d046704035b91f85814ff5aea0a5f0084e26dde5e4b55faa426470c9", CosyVoice300MRevision),
            new FileEntry("fp16/text_encoder.mnn.weight", 167092224L, "07c3568c8c178c4702410f14bd8cc014abf48a6efdf53409ac5d68f9ed5735f7", CosyVoice300MRevision),
            new FileEntry("mnn_cosyvoice300m.json", 399L, "8e79dc29860ef214ef3eba830d21eb98bf474cc04bf68a043a5490194fd44f2d", CosyVoice300MRevision),
            new FileEntry("manifest.json", 12777L, "785242fb4538ee59bdb869334b5453ad043210fe677185e461afd48ecedaccd8", CosyVoice300MRevision),
        };
    }
}
