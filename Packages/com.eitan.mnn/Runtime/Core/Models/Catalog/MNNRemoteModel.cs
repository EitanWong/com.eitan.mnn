using System;

namespace MNN.Unity
{
    [Serializable]
    public class MNNRemoteModel
    {
        public string id;
        public MNNModelSource provider = MNNModelSource.ModelScope;
        public string repositoryId;
        public string revision;
        public string modelScopeId;
        public string name;
        public string displayName;
        public string category;
        public string family;
        public string generation;
        public string description;
        public string author;
        public string version;
        public string downloadUrl;
        public long fileSize;
        public long parameterCount;
        public bool parameterCountInferred;
        public long downloadCount;
        public string[] tags;
        public string[] tasks;
        public string previewImage;
        public MNNModelInputSpec inputSpec;
        public MNNModelOutputSpec outputSpec;
        public bool isRepository;
        public bool requiresTranscript;
        public bool runtimeAvailable = true;
        public string[] mnnFiles;
        public string supportSummary;
    }

    public enum MNNModelSource
    {
        ModelScope,
        HuggingFace
    }
}
