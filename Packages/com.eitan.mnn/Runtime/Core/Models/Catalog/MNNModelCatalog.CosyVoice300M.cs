using System.Linq;

namespace MNN.Unity
{
    public partial class MNNModelCatalog
    {
        public void IncludeCosyVoice300MProfile()
        {
            string repository = MNNVerifiedTtsModel.CosyVoice300MRepository;
            var model = models.FirstOrDefault(item => item.provider == MNNModelSource.ModelScope && item.repositoryId == repository);
            if (model == null)
            {
                model = new MNNRemoteModel {id = repository, provider = MNNModelSource.ModelScope, repositoryId = repository,
                    modelScopeId = repository, name = "CosyVoice-300M-MNN", displayName = "CosyVoice-300M-MNN",
                    author = "EitanWang", isRepository = true, parameterCount = 300000000,
                    description = "First-generation CosyVoice-300M / 初代语音合成. FP16, full reference and ten Flow steps; 22050 Hz mono.",
                    tags = new[] {"MNN", "Unity", "CosyVoice", "语音合成"}, tasks = new[] {"text-to-speech"}};
                models.Add(model);
            }
            ApplyArtifactEvidence(model, MNNVerifiedTtsModel.CosyVoice300MFiles().Select(file => file.Path).ToArray());
        }
    }
}
