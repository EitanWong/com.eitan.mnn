using System.Runtime.Serialization;

namespace MNN.Samples.SiriAI.Harnesses
{
    /// <summary>A bounded native session/load replay, distinct from current task results and delivery receipts.</summary>
    [DataContract]
    public sealed class HarnessNativeHistory
    {
        [DataMember] public bool Supported;
        [DataMember] public string SessionId;
        [DataMember] public bool Uncertain;
        [DataMember] public bool Truncated;
        [DataMember] public int ReceivedEntries;
        [DataMember] public HarnessNativeHistoryEntry[] Entries = new HarnessNativeHistoryEntry[0];
    }
    [DataContract]
    public sealed class HarnessNativeHistoryEntry
    {
        [DataMember] public int Sequence;
        [DataMember] public string Kind;
        [DataMember(EmitDefaultValue = false)] public string MessageId;
        [DataMember] public string Text;
        [DataMember] public string Payload;
    }
}
