using System.Diagnostics;
using MNN.Samples.SiriAI.Harnesses;

namespace SiriAI.HarnessHost;

/// <summary>User think time is outside the execution budget; repeated forms do not reset the remaining budget.</summary>
internal sealed class AcpOperationBudget : IDisposable
{
    private readonly object _sync = new();
    private readonly CancellationTokenSource _operation;
    private double _remaining;
    private long _started = Stopwatch.GetTimestamp();
    private bool _waiting, _disposed;
    internal AcpOperationBudget(CancellationTokenSource operation, int milliseconds)
    { _operation = operation; _remaining = milliseconds; if (milliseconds > 0) operation.CancelAfter(milliseconds); }
    internal void Observe(HarnessActivity activity)
    {
        lock (_sync)
        {
            if (_disposed || _remaining == 0) return;
            bool waiting = activity.WaitingFor != HarnessWaitingFor.None;
            if (waiting == _waiting) return;
            if (waiting)
            {
                _remaining -= Stopwatch.GetElapsedTime(_started).TotalMilliseconds;
                _operation.CancelAfter(Timeout.Infinite);
                if (_remaining <= 0) _operation.Cancel();
            }
            else
            {
                _started = Stopwatch.GetTimestamp();
                _operation.CancelAfter(TimeSpan.FromMilliseconds(Math.Max(0, _remaining)));
            }
            _waiting = waiting;
        }
    }
    public void Dispose() { lock (_sync) _disposed = true; }
}
