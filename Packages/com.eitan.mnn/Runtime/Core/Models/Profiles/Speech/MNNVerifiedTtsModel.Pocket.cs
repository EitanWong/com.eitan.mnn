using System;

namespace MNN.Unity
{
    internal static partial class MNNVerifiedTtsModel
    {
        internal const string PocketRepository = "developerabu/pocket-tts-mnn";
        internal const string PocketRevision = "4e75a38d20a9c532ed55591186584a244c381095";
        internal static bool IsPocketRepository(string repository) => string.Equals(repository, PocketRepository, StringComparison.OrdinalIgnoreCase);
        internal static FileEntry[] PocketFiles() => new[]
        {
            new FileEntry("mnn/flow_lm_main_w8.mnn", 76478284L, "20e155ff4b6d9fc40ca3d231153bfa297d0095cd7c807ed9f914f22821194ef2", PocketRevision),
            new FileEntry("mnn/flow_lm_flow_w8.mnn", 10010560L, "d86d82b6c4a4ecfdb2bdf6c48445118a7e19c8544415b76c6fae464441a666e0", PocketRevision),
            new FileEntry("mnn/mimi_decoder_w8.mnn", 10615828L, "82ac6b342cc3a59c6bccab6c8eff63711f50c99362020a8450e2f8797fde3173", PocketRevision),
            new FileEntry("mnn/mimi_encoder_w8.mnn", 10006056L, "e9e589d5eaf6f16921483852496a82f2254b02ff0e938c66003b1608eac4e2fb", PocketRevision),
            new FileEntry("mnn/text_conditioner_fp32.mnn", 16389132L, "348b1c524875cf5a72015419337443eb4be9ef0303ef9d8137b454294510cd6f", PocketRevision),
            new FileEntry("bundle.json", 25462L, "be0ebd4a90dd2024e9e84caf1aee620defdcfd947f0c4e05c3584e245a996085", PocketRevision),
            new FileEntry("bos_before_voice.npy", 4224L, "f46edf4f7007b7ba4ea58831f49d003e59e167b4641c44bb3addfe9231a780b1", PocketRevision),
            new FileEntry("tokenizer.model", 59339L, "d461765ae179566678c93091c5fa6f2984c31bbe990bf1aa62d92c64d91bc3f6", PocketRevision),
            new FileEntry("reference_sample.wav", 286060L, "88fbb0d31ec26674e97e531a71758cabe4e0e4e5b5a18dafa783021a7f5c9366", PocketRevision),
            new FileEntry("README.md", 3065L, "dcb3bec3cf031fa1c19dd5d8a3c7c98172b506b9b05564854c4432569ff37acd", PocketRevision),
            new FileEntry("LICENSE", 1135L, "d5b32a1bdb18310363fdb88bb87aa95ff501fa32716ab759da8726d784bae234", PocketRevision),
        };
    }
}
