using System.Runtime.Serialization;

namespace MNN.Samples.SiriAI.Harnesses
{
    /// <summary>Non-secret connection settings. Credentials belong to the platform vault, keyed by profile ID.</summary>
    [DataContract]
    public sealed class HarnessHostProfile
    {
        [DataMember] public string Id { get; private set; }
        [DataMember] public string Name { get; private set; }
        [DataMember] public string Address { get; private set; }
        [DataMember] public string HarnessId { get; private set; }
        [DataMember] public string WorkspaceKey { get; private set; }
        public HarnessHostProfile(string id, string name, string address, string harnessId, string workspaceKey)
        {
            Id = HarnessText.Required(id, 128);
            Name = HarnessText.Required(name, 128);
            Address = HarnessHostAddress.Parse(address).AbsoluteUri;
            HarnessId = HarnessText.Required(harnessId, 128);
            WorkspaceKey = HarnessText.Required(workspaceKey, 1024);
        }
        internal void Validate() => new HarnessHostProfile(Id, Name, Address, HarnessId, WorkspaceKey);
    }
}
