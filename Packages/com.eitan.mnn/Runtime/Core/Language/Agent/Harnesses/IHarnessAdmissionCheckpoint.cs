namespace MNN.Samples.SiriAI.Harnesses
{
    /// <summary>Seals adapter-specific admission identity in the same durable commit as a new task.
    /// The checkpoint must distinguish admission from execution; recovery must never replay unknown work.</summary>
    public interface IHarnessAdmissionCheckpoint
    {
        string CreateAdmissionCheckpoint(TaskSubmission request);
    }
}
