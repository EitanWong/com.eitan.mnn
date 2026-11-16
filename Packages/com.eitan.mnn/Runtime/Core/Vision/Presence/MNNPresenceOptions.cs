using System;

namespace MNN.Unity
{
    /// <summary>Immutable presence timing. Defaults confirm for 350 ms, retain UI for 3 s, sleep after 15 s.</summary>
    public sealed class MNNPresenceOptions
    {
        public double ConfirmationSeconds { get; }
        public double FreshnessSeconds { get; }
        public double GraceSeconds { get; }
        public double SleepSeconds { get; }
        public double MaximumBusyHoldSeconds { get; }
        public float MinimumConfidence { get; }
        public bool RequireFace { get; }

        public MNNPresenceOptions(double confirmationSeconds = .35, double freshnessSeconds = .75,
            double graceSeconds = 3, double sleepSeconds = 15, double maximumBusyHoldSeconds = 30,
            float minimumConfidence = .6f, bool requireFace = true)
        {
            RequireTime(confirmationSeconds, nameof(confirmationSeconds));
            RequireTime(freshnessSeconds, nameof(freshnessSeconds));
            RequireTime(graceSeconds, nameof(graceSeconds));
            RequireTime(sleepSeconds, nameof(sleepSeconds));
            RequireTime(maximumBusyHoldSeconds, nameof(maximumBusyHoldSeconds));
            if (freshnessSeconds <= 0 || graceSeconds < freshnessSeconds || sleepSeconds < graceSeconds || maximumBusyHoldSeconds < graceSeconds)
                throw new ArgumentOutOfRangeException(nameof(sleepSeconds), "Require 0 < freshness <= grace <= sleep, and busy hold >= grace.");
            if (float.IsNaN(minimumConfidence) || minimumConfidence < 0 || minimumConfidence > 1)
                throw new ArgumentOutOfRangeException(nameof(minimumConfidence));
            ConfirmationSeconds = confirmationSeconds;
            FreshnessSeconds = freshnessSeconds;
            GraceSeconds = graceSeconds;
            SleepSeconds = sleepSeconds;
            MaximumBusyHoldSeconds = maximumBusyHoldSeconds;
            MinimumConfidence = minimumConfidence;
            RequireFace = requireFace;
        }

        internal static void RequireTime(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
                throw new ArgumentOutOfRangeException(name);
        }
    }
}
