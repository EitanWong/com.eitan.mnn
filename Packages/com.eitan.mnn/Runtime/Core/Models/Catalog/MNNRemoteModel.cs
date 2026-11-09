using System;
using System.Runtime.Serialization;

namespace MNN.Unity
{
    [Serializable, DataContract, UnityEngine.Scripting.Preserve]
    public class MNNRemoteModel
    {
        [DataMember, UnityEngine.Scripting.Preserve] public string id;
        [DataMember, UnityEngine.Scripting.Preserve] public MNNModelSource provider = MNNModelSource.ModelScope;
        [DataMember, UnityEngine.Scripting.Preserve] public string repositoryId;
        [DataMember, UnityEngine.Scripting.Preserve] public string revision;
        [DataMember, UnityEngine.Scripting.Preserve] public string modelScopeId;
        [DataMember, UnityEngine.Scripting.Preserve] public string name;
        [DataMember, UnityEngine.Scripting.Preserve] public string displayName;
        [DataMember, UnityEngine.Scripting.Preserve] public string category;
        [DataMember, UnityEngine.Scripting.Preserve] public string family;
        [DataMember, UnityEngine.Scripting.Preserve] public string generation;
        [DataMember, UnityEngine.Scripting.Preserve] public string description;
        [DataMember, UnityEngine.Scripting.Preserve] public string author;
        [DataMember, UnityEngine.Scripting.Preserve] public string version;
        [DataMember, UnityEngine.Scripting.Preserve] public string downloadUrl;
        [DataMember, UnityEngine.Scripting.Preserve] public long fileSize;
        [DataMember, UnityEngine.Scripting.Preserve] public long parameterCount;
        [DataMember, UnityEngine.Scripting.Preserve] public bool parameterCountInferred;
        [DataMember, UnityEngine.Scripting.Preserve] public long downloadCount;
        [DataMember, UnityEngine.Scripting.Preserve] public string[] tags;
        [DataMember, UnityEngine.Scripting.Preserve] public string[] tasks;
        [DataMember, UnityEngine.Scripting.Preserve] public string previewImage;
        [DataMember, UnityEngine.Scripting.Preserve] public MNNModelInputSpec inputSpec;
        [DataMember, UnityEngine.Scripting.Preserve] public MNNModelOutputSpec outputSpec;
        [DataMember, UnityEngine.Scripting.Preserve] public bool isRepository;
        [DataMember, UnityEngine.Scripting.Preserve] public bool requiresTranscript;
        [DataMember, UnityEngine.Scripting.Preserve] public bool runtimeAvailable = true;
        [DataMember, UnityEngine.Scripting.Preserve] public string[] mnnFiles;
        [DataMember, UnityEngine.Scripting.Preserve] public string supportSummary;
    }

    public enum MNNModelSource
    {
        ModelScope,
        HuggingFace
    }
}
