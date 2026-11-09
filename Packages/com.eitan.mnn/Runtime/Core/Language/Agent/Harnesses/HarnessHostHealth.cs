using System.Runtime.Serialization;

namespace MNN.Samples.SiriAI.Harnesses
{
    [DataContract]
    public sealed class HarnessHostHealth
    {
        [DataMember(Order = 0)] public int ProtocolVersion { get; set; }
        [DataMember(Order = 1)] public string HostId { get; set; }
        [DataMember(Order = 2)] public string InstanceId { get; set; }
        [DataMember(Order = 3)] public string ServiceLabel { get; set; }
        [DataMember(Order = 4)] public int ProcessId { get; set; }
        [DataMember(Order = 5)] public long StartedUtcTicks { get; set; }
        [DataMember(Order = 6)] public string OwnerId { get; set; }
    }
}
