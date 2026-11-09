using System.Threading;
using System.Threading.Tasks;

namespace MNN.Samples.SiriAI.Harnesses
{
    /// <summary>Supports exact durable instruction identities and new bounded content.
    /// Validation has no side effects. Delivery must report uncertainty rather than replay unknown work.</summary>
    public interface IHarnessInstructionAdapter
    {
        void ValidateInstruction(HarnessTask task, HarnessInstruction instruction);
        Task UpdateInstructionAsync(HarnessTask task, HarnessInstruction instruction, CancellationToken cancellation);
    }
}
