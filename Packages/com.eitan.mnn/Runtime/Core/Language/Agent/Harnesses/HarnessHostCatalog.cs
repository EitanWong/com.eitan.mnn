using System.Runtime.Serialization;

namespace MNN.Samples.SiriAI.Harnesses
{
    [DataContract]
    public sealed class HarnessHostCatalog
    {
        [DataMember(Name = "configured")] public ConfiguredHarness[] Configured;
        [DataMember(Name = "installations")] public HarnessInstallation[] Installations;
        [DataMember(Name = "workspaces")] public string[] Workspaces;
    }
    [DataContract]
    public sealed class ConfiguredHarness
    {
        [DataMember(Name = "backend")] public string Backend;
        [DataMember(Name = "permissionMode")] public string PermissionMode;
        [DataMember(Name = "id")] public string Id;
        [DataMember(Name = "capabilities")] public HarnessCapabilities Capabilities;
        [DataMember(Name = "cancellationScope", EmitDefaultValue = false)] public string CancellationScope;
    }
    [DataContract]
    public sealed class HarnessInstallation
    {
        [DataMember(Name = "backend")] public string Backend;
        [DataMember(Name = "integration")] public string Integration;
        [DataMember(Name = "id")] public string Id;
        [DataMember(Name = "name")] public string Name;
        [DataMember(Name = "executable")] public string Executable;
        [DataMember(Name = "adapterExecutable")] public string AdapterExecutable;
        [DataMember(Name = "arguments")] public string[] Arguments;
        [DataMember(Name = "installed")] public bool Installed;
        [DataMember(Name = "protocolCandidate")] public bool ProtocolCandidate;
        [DataMember(Name = "interoperabilityVerified")] public bool InteroperabilityVerified;
        [DataMember(Name = "minimumVersion", EmitDefaultValue = false)] public string MinimumVersion;
        [DataMember(Name = "requiresExplicitConfiguration")] public bool RequiresExplicitConfiguration;
    }
}
