using System;

namespace MNN.Unity
{
    /// <summary>Detached mutable DTO copies for API boundaries; no shared arrays or tensor shapes.</summary>
    public static class MNNRemoteModelSnapshot
    {
        public static MNNRemoteModel Copy(MNNRemoteModel model)
        {
            if (model == null) return null;
            return new MNNRemoteModel
            {
                id = model.id,
                provider = model.provider,
                repositoryId = model.repositoryId,
                revision = model.revision,
                modelScopeId = model.modelScopeId,
                name = model.name,
                displayName = model.displayName,
                category = model.category,
                family = model.family,
                generation = model.generation,
                description = model.description,
                author = model.author,
                version = model.version,
                downloadUrl = model.downloadUrl,
                fileSize = model.fileSize,
                parameterCount = model.parameterCount,
                parameterCountInferred = model.parameterCountInferred,
                downloadCount = model.downloadCount,
                previewImage = model.previewImage,
                isRepository = model.isRepository,
                requiresTranscript = model.requiresTranscript,
                runtimeAvailable = model.runtimeAvailable,
                supportSummary = model.supportSummary,
                tags = Clone(model.tags),
                tasks = Clone(model.tasks),
                mnnFiles = Clone(model.mnnFiles),
                inputSpec = model.inputSpec == null ? null : new MNNModelInputSpec
                {
                    name = model.inputSpec.name,
                    shape = Clone(model.inputSpec.shape),
                    dataType = model.inputSpec.dataType,
                    format = model.inputSpec.format
                },
                outputSpec = model.outputSpec == null ? null : new MNNModelOutputSpec
                {
                    name = model.outputSpec.name,
                    shape = Clone(model.outputSpec.shape),
                    dataType = model.outputSpec.dataType,
                    classCount = model.outputSpec.classCount
                }
            };
        }
        private static T[] Clone<T>(T[] value) => value == null ? null : (T[])value.Clone();
    }
}
