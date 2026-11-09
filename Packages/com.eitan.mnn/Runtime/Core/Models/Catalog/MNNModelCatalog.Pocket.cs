using System;
using System.Linq;

namespace MNN.Unity
{
    public partial class MNNModelCatalog
    {
        internal void IncludePocketProfiles()
        {
            AddPocket("MNN-Pocket-0.3B", "BF16 instruction source; use MNN-Pocket-0.3B-MNN for inference.");
            AddPocket("MNN-Pocket-0.3B-Base", "BF16 pretrained base; export with official llmexport before loading.");
            AddPocket("MNN-Pocket-0.3B-SFT", "BF16 supervised fine-tuning checkpoint; export with official llmexport before loading.");
            AddPocket("MNN-Pocket-0.3B-GGUF", "GGUF F16/Q8_0/Q4_1 for llama.cpp; MNN cannot load GGUF.");
            AddPocket("MNN-Pocket-0.3B-Dec", "Non-autoregressive LoRA and pointer head; no official MNN decision graph is published.");
            var deployment = AddPocket("MNN-Pocket-0.3B-MNN", "Official instruction model, INT4 W4A16 QAT, block 64; Chinese/English, ChatML, 32K deployment context.");
            ApplyPocketDeploymentProfile(deployment);
        }

        private static void ApplyPocketDeploymentProfile(MNNRemoteModel deployment)
        {
            deployment.category = MNNModelCatalogCategory.LargeLanguageModel.ToString();
            deployment.family = "MNN-Pocket";
            deployment.revision = deployment.version = MNNPocketModel.Revision;
            deployment.fileSize = MNNPocketModel.Bytes;
            deployment.runtimeAvailable = true;
            deployment.mnnFiles = MNNPocketModel.Files().Where(file => file.Path.EndsWith(".mnn") || file.Path.EndsWith(".mnn.weight")).Select(file => file.Path).ToArray();
            deployment.supportSummary = "Pinned official MNN assets with tokenizer.mtok and upstream chat template. Native loading validates operators under the package's supported compiler/platform ABI.";
        }

        private MNNRemoteModel AddPocket(string name, string description)
        {
            string repository = "MNN/" + name;
            var model = models.FirstOrDefault(item => item.provider == MNNModelSource.ModelScope && string.Equals(item.repositoryId, repository, StringComparison.OrdinalIgnoreCase));
            if (model != null) return model;
            model = new MNNRemoteModel
            {
                id = repository, repositoryId = repository, modelScopeId = repository,
                provider = MNNModelSource.ModelScope, revision = "master", version = "master",
                name = name, displayName = name, author = "MNN", family = "MNN-Pocket",
                category = MNNModelCatalogCategory.LargeLanguageModel.ToString(), parameterCount = 300000000,
                isRepository = true, runtimeAvailable = false, description = description, supportSummary = description,
                tags = new[] { "MNN-Pocket", "zh", "en", "apache-2.0" }, tasks = new[] { "text-generation" }
            };
            models.Add(model);
            return model;
        }
    }
}
