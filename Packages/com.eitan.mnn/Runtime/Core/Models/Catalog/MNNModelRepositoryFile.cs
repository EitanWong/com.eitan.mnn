using System;
using System.Runtime.Serialization;

namespace MNN.Unity
{
    /// <summary>One file in a remote model repository, including verification and revision metadata.</summary>
    [Serializable, DataContract, UnityEngine.Scripting.Preserve]
    public class MNNModelRepositoryFile
    {
        [DataMember, UnityEngine.Scripting.Preserve] public string Path;
        [DataMember, UnityEngine.Scripting.Preserve] public string Type;
        [DataMember, UnityEngine.Scripting.Preserve] public long Size;
        [DataMember, UnityEngine.Scripting.Preserve] public string Revision;
        [DataMember, UnityEngine.Scripting.Preserve] public string Sha256;
        [DataMember, UnityEngine.Scripting.Preserve] public string SourceRepository;
    }
}
