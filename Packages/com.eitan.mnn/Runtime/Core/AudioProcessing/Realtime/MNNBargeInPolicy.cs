using System;

namespace MNN.Unity
{
    /// <summary>
    /// Confirms interruption from VAD plus an explicit post-AEC near-end signal. It does not perform echo cancellation.
    /// Call once per audio-processing frame, outside device callbacks.
    /// </summary>
    public sealed class MNNBargeInPolicy
    {
        private readonly int _confirmationMilliseconds;
        private readonly int _releaseMilliseconds;
        private readonly float _minimumConfidence;
        private int _candidateMilliseconds;
        private int _releaseElapsedMilliseconds;
        private bool _candidate;

        public MNNBargeInPolicy(int confirmationMilliseconds = 96, int releaseMilliseconds = 64,
            float minimumConfidence = 0.65f)
        {
            if (confirmationMilliseconds < 1 || confirmationMilliseconds > 1000)
                throw new ArgumentOutOfRangeException(nameof(confirmationMilliseconds));
            if (releaseMilliseconds < 0 || releaseMilliseconds > 1000)
                throw new ArgumentOutOfRangeException(nameof(releaseMilliseconds));
            if (float.IsNaN(minimumConfidence) || minimumConfidence < 0 || minimumConfidence > 1)
                throw new ArgumentOutOfRangeException(nameof(minimumConfidence));
            _confirmationMilliseconds = confirmationMilliseconds;
            _releaseMilliseconds = releaseMilliseconds;
            _minimumConfidence = minimumConfidence;
        }

        public bool HasCandidate => _candidate;

        public MNNBargeInAction Process(MNNRealtimeVadUpdate vad, bool postAecNearEnd,
            float nearEndConfidence, bool assistantAudible, int frameMilliseconds,
            bool explicitInterrupt = false)
        {
            if (frameMilliseconds < 0 || frameMilliseconds > 1000)
                throw new ArgumentOutOfRangeException(nameof(frameMilliseconds));
            if (float.IsNaN(nearEndConfidence) || nearEndConfidence < 0 || nearEndConfidence > 1)
                throw new ArgumentOutOfRangeException(nameof(nearEndConfidence));

            if (explicitInterrupt)
            {
                Reset();
                return MNNBargeInAction.Confirmed;
            }

            bool nearEndSpeech = assistantAudible && vad.IsSpeechActive && postAecNearEnd &&
                                 nearEndConfidence >= _minimumConfidence;
            if (nearEndSpeech)
            {
                _releaseElapsedMilliseconds = 0;
                _candidateMilliseconds = Math.Min(_confirmationMilliseconds,
                    _candidateMilliseconds + frameMilliseconds);
                if (_candidateMilliseconds >= _confirmationMilliseconds)
                {
                    Reset();
                    return MNNBargeInAction.Confirmed;
                }
                if (!_candidate)
                {
                    _candidate = true;
                    return MNNBargeInAction.Candidate;
                }
                return MNNBargeInAction.None;
            }

            if (!_candidate)
                return MNNBargeInAction.None;

            _releaseElapsedMilliseconds += frameMilliseconds;
            if (_releaseElapsedMilliseconds < _releaseMilliseconds)
                return MNNBargeInAction.None;

            Reset();
            return MNNBargeInAction.Cancelled;
        }

        public void Reset()
        {
            _candidateMilliseconds = 0;
            _releaseElapsedMilliseconds = 0;
            _candidate = false;
        }
    }
}
