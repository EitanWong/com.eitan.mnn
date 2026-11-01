using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    /// <summary>
    /// Owns concrete device/model resources for one session. Implementations must keep native resources alive
    /// until WaitForInferenceQuiescenceAsync completes and must not execute device callbacks on the control path.
    /// Lifecycle methods run on asynchronous control continuations, not Unity's main thread. Dispatch device
    /// and model work to their owners and return without blocking. Do not synchronously wait for a reentrant
    /// session operation or await shutdown from a method that shutdown itself must join.
    /// </summary>
    public interface IMNNRealtimeSessionRuntime
    {
        Task PrepareAsync(CancellationToken cancellationToken);
        Task StartListeningAsync(CancellationToken cancellationToken);
        Task RequestCancellationAsync(MNNResponseId response);
        Task WaitForInferenceQuiescenceAsync(MNNResponseId response);
        Task RestoreContextAsync(MNNResponseId interruptedResponse);
        Task StopListeningAsync();
        Task WaitForQuiescenceAsync();
        Task DisposeAsync();
    }
}
