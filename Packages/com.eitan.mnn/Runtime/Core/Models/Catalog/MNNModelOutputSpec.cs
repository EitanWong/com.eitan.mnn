using System;
using System.Runtime.Serialization;

namespace MNN.Unity
{
    [Serializable, DataContract, UnityEngine.Scripting.Preserve]
    public class MNNModelOutputSpec
    {
        [DataMember, UnityEngine.Scripting.Preserve] public string name;
        [DataMember, UnityEngine.Scripting.Preserve] public int[] shape;
        [DataMember, UnityEngine.Scripting.Preserve] public string dataType;
        [DataMember, UnityEngine.Scripting.Preserve] public int classCount;
    }
}
