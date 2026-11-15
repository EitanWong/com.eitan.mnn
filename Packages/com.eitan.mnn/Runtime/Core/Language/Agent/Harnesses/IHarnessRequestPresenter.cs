using System.Threading;

namespace MNN.Samples.SiriAI.Harnesses
{
    /// <summary>Optional presentation port for real pending questions, separate from terminal results.</summary>
    public interface IHarnessRequestPresenter
    {
        IHarnessResultReply TryBeginRequestReply(string eventId, string context, CancellationToken cancellation);
    }
}
