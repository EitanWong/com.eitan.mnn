using System;
using System.Linq;

namespace MNN.Unity
{
    public partial class MNNModelCatalog
    {
        /// <summary>Register the complete MiniMind-o MNN export shared by Editor and runtime catalogs.</summary>
        public void IncludeMiniMindOProfile()
        {
            models.RemoveAll(model => model.provider == MNNModelSource.ModelScope &&
                string.Equals(model.repositoryId, MNNMiniMindO.Repository, StringComparison.OrdinalIgnoreCase));
            models.Add(CreateMiniMindOProfile());
        }

        private static MNNRemoteModel CreateMiniMindOProfile() => new MNNRemoteModel
            {
                id = MNNMiniMindO.Repository, repositoryId = MNNMiniMindO.Repository, modelScopeId = MNNMiniMindO.Repository,
                provider = MNNModelSource.ModelScope, revision = MNNMiniMindO.Revision, version = MNNMiniMindO.Revision,
                name = MNNMiniMindO.InstallationName, displayName = "MiniMind-o 113M (MNN)",
                family = "MiniMind", category = MNNModelCatalogCategory.OmniModel.ToString(),
                author = "jingyaogong / EitanWang", parameterCount = 113000000,
                fileSize = 1399325814L, isRepository = true, runtimeAvailable = true,
                tags = new[] { "MiniMind-o", "MiniMindO", "omni", "zh", "en" },
                tasks = new[] { "text-generation", "image-text-to-text", "audio-text-to-text", "text-to-speech" },
                mnnFiles = new[] { "prefill", "decode", "multimodal_prefill", "audio_encoder", "vision_encoder", "mimi_decoder" }
                    .SelectMany(name => new[] { name + ".mnn", name + ".mnn.weight" }).ToArray(),
                description = "MiniMind-o: text, image/audio input and streamed text/speech output. Mixed W8/FP16 MNN export.",
                supportSummary = "Requires all six graphs, external weights, tokenizer and mnn_minimind_o.json. Completed input turns; no model-native full duplex."
            };
    }
}
