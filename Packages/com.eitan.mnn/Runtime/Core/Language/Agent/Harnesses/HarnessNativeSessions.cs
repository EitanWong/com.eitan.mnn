using System;
using System.Runtime.Serialization;

namespace MNN.Samples.SiriAI.Harnesses
{
    /// <summary>One native list page filtered to the task's already-owned context. Listing does not load history.</summary>
    [DataContract]
    public sealed class HarnessNativeSessionPage
    {
        [DataMember] public bool Supported;
        [DataMember] public bool Uncertain;
        [DataMember] public HarnessNativeSession[] Sessions = new HarnessNativeSession[0];
        [DataMember(EmitDefaultValue = false)] public string NextCursor;
    }
    [DataContract]
    public sealed class HarnessNativeSession
    {
        [DataMember] public string SessionId;
        [DataMember] public string Cwd;
        [DataMember(EmitDefaultValue = false)] public string Title;
        [DataMember(EmitDefaultValue = false)] public string UpdatedAt;
    }
}
