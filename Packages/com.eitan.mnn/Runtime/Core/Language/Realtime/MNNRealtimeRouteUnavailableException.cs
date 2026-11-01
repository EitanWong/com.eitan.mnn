using System;

namespace MNN.Unity
{
    /// <summary>
    /// Explicit backend classification permitting finite fallback for this topology only. Never use for
    /// user cancellation, permissions, shared ABI/device failures or failure to quiesce/release resources.
    /// </summary>
    public sealed class MNNRealtimeRouteUnavailableException : NotSupportedException
    {
        public MNNRealtimeRoute Route { get; }

        public MNNRealtimeRouteUnavailableException(MNNRealtimeRoute route, string message, Exception innerException = null)
            : base(message, innerException)
        {
            if (!Enum.IsDefined(typeof(MNNRealtimeRoute), route))
                throw new ArgumentOutOfRangeException(nameof(route));
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("A route-specific failure reason is required.", nameof(message));
            Route = route;
        }
    }
}
