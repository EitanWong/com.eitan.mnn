using System;
using System.Diagnostics;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>Control-thread publication of qualified backend AEC health; worker reads never inspect plugin objects.
    /// A stalled supervisor expires qualification without waiting for another control-thread update.</summary>
    public sealed class MNNRealtimeEchoHealth
    {
        private long _healthyUntil;
        public bool Healthy => IsHealthyAt(Stopwatch.GetTimestamp());
        public bool IsHealthyAt(long ticks) => ticks < Interlocked.Read(ref _healthyUntil);
        public void Publish(bool qualified, int validMilliseconds = 50) => PublishAt(qualified, Stopwatch.GetTimestamp(), validMilliseconds);
        public void PublishAt(bool qualified, long ticks, int validMilliseconds = 50)
        {
            if (ticks < 0 || validMilliseconds < 1 || validMilliseconds > 1000) throw new ArgumentOutOfRangeException(nameof(validMilliseconds));
            Interlocked.Exchange(ref _healthyUntil, qualified ? checked(ticks + (long)validMilliseconds * Stopwatch.Frequency / 1000) : 0);
        }
        public void Reset() => Interlocked.Exchange(ref _healthyUntil, 0);
    }
}
