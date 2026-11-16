using System;
using System.Collections.Generic;

namespace MNN.Unity
{
    /// <summary>Single-owner policy, independent of camera, inference, UI and Unity time scale.
    /// Observe on new sensor results; Tick even if the camera stalls. Busy means an existing turn, never new admission.</summary>
    public sealed class MNNPresenceController
    {
        private readonly MNNPresenceOptions _options;
        private readonly HashSet<string> _allowed;
        private double _now, _lastObservation = double.NegativeInfinity;
        private double _lastSeen = double.NegativeInfinity, _candidateSince, _confirmedAt = double.PositiveInfinity;
        private string _track, _identity;
        private bool _visible, _confirmed;
        public MNNPresenceSnapshot Current { get; private set; }

        public MNNPresenceController(MNNPresenceOptions options = null, IEnumerable<string> allowedIdentities = null)
        {
            _options = options ?? new MNNPresenceOptions();
            _allowed = allowedIdentities == null ? null : new HashSet<string>(allowedIdentities, StringComparer.Ordinal);
            Current = new MNNPresenceSnapshot(MNNPresenceState.Sleeping, null, false, false, 0);
        }

        public MNNPresenceSnapshot Observe(MNNPresenceObservation observation, double now, bool interactionBusy = false)
        {
            ValidateNow(now);
            if (observation.CapturedAt > now) throw new ArgumentOutOfRangeException(nameof(observation), "Sensor time is in the future.");
            // Replayed, late or old frames cannot renew presence or complete confirmation.
            if (observation.CapturedAt <= _lastObservation || now - observation.CapturedAt > _options.FreshnessSeconds)
                return Tick(now, interactionBusy);
            _lastObservation = observation.CapturedAt;
            if (!Accept(observation))
            {
                if (observation.Kind != MNNPresenceKind.None &&
                    (observation.TrackId != _track || observation.Identity != null && observation.Identity != _identity))
                    _confirmed = false;
                _visible = false;
                return Tick(now, interactionBusy);
            }
            bool same = _track == observation.TrackId && _identity == observation.Identity;
            bool recent = observation.CapturedAt - _lastSeen <= _options.GraceSeconds;
            if (!same || !recent)
            {
                _confirmed = false;
                _track = observation.TrackId;
                _identity = observation.Identity;
            }
            if (!same || !_visible || observation.CapturedAt - _lastSeen > _options.FreshnessSeconds)
                _candidateSince = observation.CapturedAt;
            _lastSeen = observation.CapturedAt;
            _visible = true;
            if (!_confirmed && observation.CapturedAt - _candidateSince >= _options.ConfirmationSeconds)
            {
                _confirmed = true;
                _confirmedAt = observation.CapturedAt;
            }
            return Tick(now, interactionBusy);
        }

        public MNNPresenceSnapshot Tick(double now, bool interactionBusy = false)
        {
            ValidateNow(now);
            _now = now;
            double absence = now - _lastSeen;
            bool fresh = _visible && absence <= _options.FreshnessSeconds;
            bool admit = _confirmed && fresh;
            bool hold = _confirmed && interactionBusy && absence < _options.MaximumBusyHoldSeconds;
            MNNPresenceState state;
            if (admit) state = MNNPresenceState.Present;
            else if (fresh) state = MNNPresenceState.Acquiring;
            else if (_confirmed && (absence < _options.GraceSeconds || hold)) state = MNNPresenceState.Grace;
            else if (_track != null && absence < _options.SleepSeconds) state = MNNPresenceState.Standby;
            else state = MNNPresenceState.Sleeping;
            if (state == MNNPresenceState.Sleeping) _confirmed = false;
            Current = new MNNPresenceSnapshot(state, _track, admit, _confirmed && (absence < _options.GraceSeconds || hold), now, _confirmedAt);
            return Current;
        }

        private bool Accept(MNNPresenceObservation observation) =>
            observation.Kind != MNNPresenceKind.None && (!_options.RequireFace || observation.Kind == MNNPresenceKind.Face) &&
            observation.Confidence >= _options.MinimumConfidence &&
            (_allowed == null || observation.Identity != null && _allowed.Contains(observation.Identity));

        private void ValidateNow(double now)
        {
            MNNPresenceOptions.RequireTime(now, nameof(now));
            if (now < _now) throw new ArgumentOutOfRangeException(nameof(now), "Use one monotonic clock.");
        }
    }
}
