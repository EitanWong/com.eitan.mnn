using System;
using System.Collections.Generic;

namespace MNN.Unity
{
    /// <summary>Single-owner geometric face continuity. Keeps the current face ahead of larger newcomers.
    /// Crossing/ambiguous matches invalidate the track. Geometry is not biometric identity.</summary>
    public sealed class MNNFaceTargetTracker
    {
        private readonly string _session = Guid.NewGuid().ToString("N");
        private readonly float _minimumScore;
        private readonly double _maximumGap;
        private MNNDetection _previous;
        private double _lastSeen, _lastTime;
        private long _sequence;
        public string TrackId { get; private set; }

        public MNNFaceTargetTracker(float minimumScore = .6f, double maximumGapSeconds = .75)
        {
            if (float.IsNaN(minimumScore) || minimumScore < 0 || minimumScore > 1) throw new ArgumentOutOfRangeException(nameof(minimumScore));
            MNNPresenceOptions.RequireTime(maximumGapSeconds, nameof(maximumGapSeconds));
            if (maximumGapSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(maximumGapSeconds));
            _minimumScore = minimumScore;
            _maximumGap = maximumGapSeconds;
        }

        public int Select(IReadOnlyList<MNNDetection> faces, double capturedAt)
        {
            if (faces == null) throw new ArgumentNullException(nameof(faces));
            MNNPresenceOptions.RequireTime(capturedAt, nameof(capturedAt));
            if (capturedAt < _lastTime) throw new ArgumentOutOfRangeException(nameof(capturedAt));
            _lastTime = capturedAt;
            bool continuing = _previous != null && capturedAt - _lastSeen <= _maximumGap;
            int index = -1;
            double best = 0, runnerUp = 0;
            for (int i = 0; i < faces.Count; ++i)
            {
                var face = faces[i];
                if (!Usable(face)) continue;
                double score = continuing ? IntersectionOverUnion(_previous, face) : (double)face.Width * face.Height;
                if (score > best) { runnerUp = best; best = score; index = i; }
                else runnerUp = Math.Max(runnerUp, score);
            }
            if (continuing && best < .3)
            {
                Reset();
                return Select(faces, capturedAt);
            }
            if (index < 0 || best - runnerUp <= best * .1)
            {
                if (index >= 0 || capturedAt - _lastSeen > _maximumGap) Reset();
                return -1;
            }
            if (!continuing) TrackId = _session + ":" + (++_sequence);
            _previous = faces[index];
            _lastSeen = capturedAt;
            return index;
        }

        public void Reset() { _previous = null; TrackId = null; }

        private bool Usable(MNNDetection face) => face != null && face.Score >= _minimumScore && face.Score <= 1 &&
            face.Width > 0 && face.Height > 0 && !float.IsInfinity(face.Width) && !float.IsInfinity(face.Height) &&
            !float.IsNaN(face.X) && !float.IsNaN(face.Y) && !float.IsInfinity(face.X) && !float.IsInfinity(face.Y);

        private static double IntersectionOverUnion(MNNDetection a, MNNDetection b)
        {
            double width = Math.Max(0, Math.Min((double)a.X + a.Width, (double)b.X + b.Width) - Math.Max(a.X, b.X));
            double height = Math.Max(0, Math.Min((double)a.Y + a.Height, (double)b.Y + b.Height) - Math.Max(a.Y, b.Y));
            double intersection = width * height;
            return intersection / ((double)a.Width * a.Height + (double)b.Width * b.Height - intersection);
        }
    }
}
