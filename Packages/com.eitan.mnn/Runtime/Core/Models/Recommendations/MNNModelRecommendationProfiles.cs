using System;
using System.Collections.Generic;

namespace MNN.Unity
{
    /// <summary>Versioned, extensible starting metadata; estimates are not device benchmarks or universal quality rankings.</summary>
    public static class MNNModelRecommendationProfiles
    {
        public const string Version = "2026-11-10.1";
        public const string Qwen9Id = "Qwen3.5-9B-MNN", Qwen9Repository = "MNN/Qwen3.5-9B-MNN", Qwen9Revision = "4def89fe8459266be4be64d5ab7ae8bbe9066081";
        public const string Qwen4Id = "Qwen3.5-4B-MNN", Qwen4Repository = "MNN/Qwen3.5-4B-MNN", Qwen4Revision = "33045fd83cd206d66976af438f7a58255b258a58";
        public const string Qwen27Id = "Qwen3.5-27B-MNN", Qwen27Repository = "MNN/Qwen3.5-27B-MNN", Qwen27Revision = "70e74126a5c32d98853a2fd9bd3df4e80141d2f6";
        public const long Qwen9DownloadBytes = 7274876613L, Qwen4DownloadBytes = 2845947034L, Qwen27DownloadBytes = 18861746870L;

        public static IReadOnlyList<MNNModelResourceProfile> CreateDefault() => Array.AsReadOnly(new[]
        {
            Qwen(Qwen9Id, Qwen9Repository, Qwen9Revision, 9000000000L, Qwen9DownloadBytes, 4961957226L, 32, 8, 4, 32, 4096, 768),
            Qwen(Qwen4Id, Qwen4Repository, Qwen4Revision, 4000000000L, Qwen4DownloadBytes, 2629387626L, 32, 8, 4, 32, 2560, 512),
            Qwen(Qwen27Id, Qwen27Repository, Qwen27Revision, 27000000000L, Qwen27DownloadBytes, 16019131066L, 64, 16, 4, 48, 5120, 1536),
            Qwen("Qwen3.5-0.8B-MNN", "MNN/Qwen3.5-0.8B-MNN", "594e8d3c5dcdd8ae7ff488a3ba5920c503721fe6", 800000000L, 547649117L, 470382614L, 24, 6, 2, 16, 1024, 256),
            Pocket(),
            Speech("wo105090--SenseVoiceSmall-MNN", MNNModelRecommendationTask.SpeechRecognition, 137002375L,
                new[] { "zh", "en", "ja", "ko", "yue" }, 128, 3L << 20,
                "https://modelscope.cn/models/wo105090/SenseVoiceSmall-MNN/tree/e7809552caf44ef8e40f2f0e086fb4037d1e3b88"),
            Speech("Supertonic-3-INT8-MNN", MNNModelRecommendationTask.SpeechSynthesis, 111036647L,
                new[] { "en" }, 128, 256L << 10,
                "https://huggingface.co/yunfengwang/supertonic-tts-mnn/tree/c0425ea0b5884cdb9c23a99b2373dcdb47f142fa")
        });

        private static MNNModelResourceProfile Qwen(string id, string repository, string revision, long parameters,
            long residentUpperBound, long streamedWeights, int layers, int attentionLayers, int kvHeads, int valueHeads, int hidden, int workspaceMb)
        {
            int linearLayers = layers - attentionLayers;
            // fp32 GatedDeltaNet state and a conservative four-slot convolution state; no context multiplier.
            long recurrent = linearLayers * (valueHeads * 128L * 128 * 4 + 4L * (2 * 16 * 128 + valueHeads * 128) * 4);
            return new MNNModelResourceProfile
            {
                Id = id, ExportIdentity = repository + "@" + revision, Task = MNNModelRecommendationTask.Language, RuntimeAvailable = true,
                SupportedMemoryKinds = MNNModelMemoryKind.Cpu | MNNModelMemoryKind.Unified,
                ParameterCount = parameters, WeightBytes = residentUpperBound, DownloadBytes = residentUpperBound,
                // Exact weight traffic excludes sparsely looked-up BF16 embeddings; residency includes them and vision graphs.
                WeightReadBytesPerUnit = streamedWeights, PeakWorkspaceBytes = workspaceMb * (1L << 20), PrefillBytesPerToken = hidden * 16L,
                MaximumContextTokens = 262144, Languages = new[] { "en", "zh" }, Capabilities = new[] { "text", "tools" },
                Cache = new[] { new MNNModelCacheProfile { Layers = attentionLayers, KeyValueHeads = kvHeads, KeyHeadDimension = 256,
                    BytesPerElement = 2, FixedBytesPerSequence = recurrent } },
                Evidence = "https://modelscope.cn/models/" + repository + "/tree/" + revision +
                    "; https://huggingface.co/Qwen/" + id.Replace("-MNN", "") + "/blob/main/config.json; workspace/speed/quality are conservative priors."
            };
        }

        private static MNNModelResourceProfile Pocket() => new MNNModelResourceProfile
        {
            Id = MNNPocketModel.ModelId, ExportIdentity = MNNPocketModel.Repository + "@" + MNNPocketModel.Revision, Task = MNNModelRecommendationTask.Language, RuntimeAvailable = true,
            SupportedMemoryKinds = MNNModelMemoryKind.Cpu | MNNModelMemoryKind.Unified,
            ParameterCount = 300000000, WeightBytes = MNNPocketModel.Bytes, DownloadBytes = MNNPocketModel.Bytes,
            PeakWorkspaceBytes = 128L << 20, MaximumContextTokens = 40960, Languages = new[] { "en", "zh" }, Capabilities = new[] { "text" },
            Cache = new[] { new MNNModelCacheProfile { Layers = 28, KeyValueHeads = 2, KeyHeadDimension = 64 } },
            Evidence = "https://modelscope.cn/models/MNN/MNN-Pocket-0.3B/resolve/master/config.json; https://modelscope.cn/models/" + MNNPocketModel.Repository + "/tree/" + MNNPocketModel.Revision
        };

        private static MNNModelResourceProfile Speech(string id, MNNModelRecommendationTask task, long bytes, string[] languages, int workspaceMb, long perInputUnit, string evidence) => new MNNModelResourceProfile
        {
            Id = id, Task = task, RuntimeAvailable = true, SupportedMemoryKinds = MNNModelMemoryKind.Cpu | MNNModelMemoryKind.Unified,
            WeightBytes = bytes, DownloadBytes = bytes, PeakWorkspaceBytes = workspaceMb * (1L << 20),
            WorkspaceBytesPerInputUnit = perInputUnit, QualityScore = .6, Languages = languages,
            Evidence = evidence + "; workspace/quality are priors; throughput is unknown until measured."
        };
    }
}
