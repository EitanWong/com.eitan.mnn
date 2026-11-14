namespace MNN.Samples.SiriAI.Harnesses
{
    /// <summary>A control-plane adapter executes in its own workspace and capacity pool.
    /// Its children still use the ordinary project lane and execution limit.</summary>
    public interface IIsolatedCoordinatorHarness
    {
        string CoordinatorLane(HarnessTask task);
    }
}
