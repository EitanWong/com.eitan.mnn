using System.Threading;
using System.Threading.Tasks;
namespace MNN.Unity
{
    /// <summary>Borrowed task tool. Validates parsed arguments before any side effect; receives no conversation history.</summary>
    public interface IMNNAgentTool
    {
        string Name { get; }
        string Description { get; }
        bool RequiresNetwork { get; }
        Task<string> ExecuteAsync(string input, CancellationToken token);
    }
}
