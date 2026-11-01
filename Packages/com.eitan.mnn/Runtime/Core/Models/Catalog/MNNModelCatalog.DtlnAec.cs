using System.Linq;

namespace MNN.Unity
{
    public partial class MNNModelCatalog
    {
        // Published immutable profiles remain searchable before a hub indexes new uploads,
        // including when displaying an older disk cache. Downloads still verify each hash.
        internal void IncludeDtlnAecProfiles()
        {
            foreach (int units in MNNDtlnAecModel.Variants)
            {
                string repository = MNNDtlnAecModel.Repository(units);
                var model = models.FirstOrDefault(item => item.provider == MNNModelSource.ModelScope && item.repositoryId == repository);
                if (model == null)
                {
                    model = new MNNRemoteModel {id = repository, provider = MNNModelSource.ModelScope, repositoryId = repository,
                        modelScopeId = repository, name = "DTLN-aec-" + units + "-MNN", displayName = "DTLN AEC " + units,
                        author = "EitanWang", isRepository = true, description = "Streaming acoustic echo cancellation / 回声消除 with a synchronized playback reference.",
                        tags = new[] {"MNN", "DTLN", "AEC", "回声消除"}, tasks = new[] {"acoustic-echo-cancellation"}};
                    models.Add(model);
                }
                ApplyArtifactEvidence(model, new[] {MNNDtlnAecModel.Graph(units, 1), MNNDtlnAecModel.Graph(units, 2)});
            }
        }
    }
}
