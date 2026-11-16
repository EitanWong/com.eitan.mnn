using System;

namespace MNN.Unity
{
    public enum MNNPresenceKind { None, Person, Face }
    public enum MNNPresenceState { Sleeping, Acquiring, Present, Grace, Standby }

    /// <summary>One sensor result in the caller's monotonic clock. TrackId is continuity, Identity is recognition.
    /// A face does not prove identity, liveness, profession or authorization.</summary>
    public readonly struct MNNPresenceObservation
    {
        public double CapturedAt { get; }
        public MNNPresenceKind Kind { get; }
        public string TrackId { get; }
        public string Identity { get; }
        public float Confidence { get; }

        public MNNPresenceObservation(double capturedAt, MNNPresenceKind kind, string trackId,
            float confidence, string identity = null)
        {
            MNNPresenceOptions.RequireTime(capturedAt, nameof(capturedAt));
            if (kind < MNNPresenceKind.None || kind > MNNPresenceKind.Face)
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (float.IsNaN(confidence) || confidence < 0 || confidence > 1)
                throw new ArgumentOutOfRangeException(nameof(confidence));
            if (kind != MNNPresenceKind.None && string.IsNullOrWhiteSpace(trackId))
                throw new ArgumentException("A detection needs a stable tracker identifier.", nameof(trackId));
            CapturedAt = capturedAt;
            Kind = kind;
            TrackId = trackId;
            Confidence = confidence;
            Identity = identity;
        }

        public static MNNPresenceObservation Absent(double capturedAt) =>
            new MNNPresenceObservation(capturedAt, MNNPresenceKind.None, null, 0);
    }

    public readonly struct MNNPresenceSnapshot
    {
        public MNNPresenceState State { get; }
        public string TrackId { get; }
        public bool CanStartInteraction { get; }
        public bool KeepCurrentInteraction { get; }
        public float PresentationTarget { get; }
        public double EvaluatedAt { get; }
        public double ConfirmedAt { get; }

        internal MNNPresenceSnapshot(MNNPresenceState state, string trackId, bool admit, bool keep, double now,
            double confirmedAt = double.PositiveInfinity)
        {
            State = state;
            TrackId = trackId;
            CanStartInteraction = admit;
            KeepCurrentInteraction = keep;
            PresentationTarget = state == MNNPresenceState.Sleeping || state == MNNPresenceState.Acquiring ? 0 : state == MNNPresenceState.Standby ? .25f : 1;
            EvaluatedAt = now;
            ConfirmedAt = confirmedAt;
        }
    }
}
