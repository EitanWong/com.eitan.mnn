using System.Threading;
using System.Threading.Tasks;

namespace MNN.Samples.SiriAI.Harnesses
{
    /// <summary>Application-owned dialogue and presentation. Invoke on the application's owning context.</summary>
    public interface IHarnessResultPresenter
    {
        bool CanPresent { get; }
        bool UsesSpeech { get; }
        IHarnessResultReply TryBeginReply(string eventId, string context, CancellationToken cancellation);
        Task InterruptAsync(IHarnessResultReply reply);
    }

    /// <summary>Observed presentation evidence. Model completion and queued audio do not imply delivery.</summary>
    public interface IHarnessResultReply
    {
        bool ContextInjected { get; }
        bool Succeeded { get; }
        bool TextDisplayed { get; }
        bool PlaybackStarted { get; }
        bool PlaybackCompleted { get; }
        bool Interrupted { get; }
        Task Completion { get; }
    }
}
