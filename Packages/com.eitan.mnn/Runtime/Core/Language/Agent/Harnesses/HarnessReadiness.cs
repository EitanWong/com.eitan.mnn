using System;
using System.Runtime.Serialization;

namespace MNN.Samples.SiriAI.Harnesses
{
    /// <summary>Timestamped protocol evidence, never a promise of model inference or current service availability.</summary>
    [DataContract]
    public sealed class HarnessReadiness
    {
        [DataMember(Order = 0)] public string HarnessId;
        [DataMember(Order = 1)] public string WorkspaceKey;
        [DataMember(Order = 2)] public string Protocol;
        [DataMember(Order = 3)] public string State;
        [DataMember(Order = 4)] public string Evidence;
        [DataMember(Order = 5)] public long CheckedUtcTicks;
        [DataMember(Order = 6)] public HarnessAcpCapabilities AcpCapabilities;
        [OnDeserialized] private void OnDeserialized(StreamingContext context)
        {
            HarnessText.Required(HarnessId, 128); HarnessText.Required(WorkspaceKey, 1024); HarnessText.Required(Protocol, 128);
            if (Array.IndexOf(new[] { "unknown", "checking", "protocol_verified", "unavailable", "unsupported" }, State) < 0)
                throw new SerializationException("Invalid backend check state.");
            ValidateTimestamp();
            if (Evidence != null) HarnessText.Required(Evidence, 128);
            if (AcpCapabilities != null && (Protocol != "acp" || State != "protocol_verified"))
                throw new SerializationException("Backend capabilities require a confirmed ACP protocol check.");
        }
        private void ValidateTimestamp()
        {
            if (CheckedUtcTicks < 0 || CheckedUtcTicks > DateTime.MaxValue.Ticks)
                throw new SerializationException("Invalid backend check timestamp.");
            if (State != "unknown" && State != "checking" && CheckedUtcTicks == 0)
                throw new SerializationException("Completed backend check requires a timestamp.");
        }
    }
    [DataContract]
    public sealed class HarnessAcpCapabilities
    {
        [DataMember(Order = 0)] public bool LoadSession;
        [DataMember(Order = 1)] public bool ResumeSession;
        [DataMember(Order = 2)] public bool ListSessions;
        [DataMember(Order = 3)] public bool Images;
        [DataMember(Order = 4)] public bool Audio;
        [DataMember(Order = 5)] public bool EmbeddedContext;
        [DataMember(Order = 6)] public bool McpHttp;
        [DataMember(Order = 7)] public bool McpSse;
        [DataMember(Order = 8)] public bool Steering;
    }
    [DataContract]
    public sealed class HarnessReadinessRequest
    {
        [DataMember(Order = 0)] public string WorkspaceKey;
    }
}
