using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    /// <summary>Creates a fresh, unprepared runtime for one validated route of the selected model.</summary>
    public interface IMNNRealtimeRuntimeFactory
    {
        /// <summary>
        /// Transfer sole ownership on successful return; on throw, clean up all acquired resources yourself.
        /// Do not start capture, generation or output here. Prepare restores application-owned committed user
        /// history and only output.GetSegment(i).IsPresented assistant history; never replay cancelled speech.
        /// Keep the shared output format and presentation timeline across device instances, and drain final
        /// receipts/presentation before old runtime quiescence completes. Dispatch Unity work to its main thread.
        /// Report only route-specific unavailability using MNNRealtimeRouteUnavailableException with report.Route.
        /// Permission, cancellation, ABI and shared device failures must retain their original exception types.
        /// </summary>
        Task<IMNNRealtimeSessionRuntime> CreateAsync(MNNRealtimeReport report,
            MNNRealtimeOutputPipeline output, CancellationToken cancellationToken);
    }
}
