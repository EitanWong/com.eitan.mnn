namespace MNN.Unity
{
    /// <summary>Application-owned tools may carry explicit network permission independent of cloud model consent.</summary>
    public interface IMNNAgentNetworkPermission
    {
        bool CanUseNetwork { get; }
    }
}
