using System;

namespace MNN.Unity
{
    /// <summary>Managed correlation estimator inspired by AEC3's decimation, excitation gating and lag voting.
    /// This is not a port of WebRTC's adaptive matched-filter bank. Inputs share a 16 kHz timeline.
    /// One processing worker owns this state; calls must not overlap.</summary>
    public sealed class MNNEchoDelayEstimator
    {
        public const int SampleRate = 16000;
        private const int Decimation = 8, EstimateRate = SampleRate / Decimation;
        // All time bounds use the decimated clock. The power-of-two ring covers window + maximum lag.
        private const int Capacity = 4096, Mask = Capacity - 1;
        private const int Window = EstimateRate * 192 / 1000, MaximumLag = EstimateRate / 2;
        private const int EstimateInterval = EstimateRate * 64 / 1000, EvidenceLifetime = EstimateRate * 5;
        public const int AlignmentToleranceSamples = SampleRate * 4 / 1000;
        private const int DelayTolerance = AlignmentToleranceSamples / Decimation, PeakExclusion = EstimateRate * 8 / 1000;
        // The returned bulk delay may retain one hysteresis band beyond a winning
        // candidate, which itself may lie one aggregation band beyond the observed
        // peak. Reserve both independent bands and one coarse sample bin so the
        // causal FIR can represent an earlier direct path without extra buffering.
        public const int AlignmentHeadroomSamples = AlignmentToleranceSamples + DelayTolerance * Decimation + Decimation;
        // Confidence policy: reject below -70 dBFS, correlation below 0.5, or competing squared
        // correlations within 0.08. Require five estimates at 64 ms intervals. These gates are
        // regression-checked on silence, unrelated near-end input, delayed signals and paired recordings;
        // they do not establish universal acoustic qualification.
        private const double MinimumMeanSquareEnergy = 1e-7, MinimumSquaredCorrelation = .25, MinimumPeakSeparation = .08;
        private const int RequiredVotes = 5;
        // A bounded lag histogram tolerates occasional competing speech/reflection peaks,
        // instead of requiring uninterrupted identical candidates (AEC3 uses lag aggregation).
        private const int VoteLifetime = EstimateRate;
        private static readonly double HighPassAlpha = 1 - Math.Exp(-2 * Math.PI * 80 / SampleRate);
        private readonly float[] _mic = new float[Capacity], _reference = new float[Capacity];
        private readonly double[] _scores = new double[MaximumLag + 1];
        private readonly int[] _lags = new int[VoteLifetime / EstimateInterval + 1];
        private readonly long[] _lagTimes = new long[VoteLifetime / EstimateInterval + 1];
        private long _written, _excitedSamples, _lastEvidence;
        private int _phase, _nextLag, _lagCount, _untilEstimate;
        private double _micSum, _referenceSum, _micLow, _referenceLow;
        private bool _locked;
        public int DelaySamples { get; private set; }
        public float Confidence { get; private set; }
        // Retain a qualified route through brief double talk; one competing peak cannot revoke it.
        // Silence cannot disprove a measured route. Age evidence only while render is excited,
        // as adaptive echo estimators freeze their route estimate without an observable input.
        public bool IsLocked => _locked && _excitedSamples - _lastEvidence < EvidenceLifetime;

        public void Reset()
        {
            Array.Clear(_mic, 0, Capacity); Array.Clear(_reference, 0, Capacity);
            _written = _excitedSamples = _lastEvidence = 0;
            _phase = _nextLag = _lagCount = _untilEstimate = DelaySamples = 0;
            _micSum = _referenceSum = _micLow = _referenceLow = Confidence = 0;
            _locked = false;
        }

        /// <summary>Consumes equally sized finite normalized mono PCM blocks. Invalid input leaves state unchanged.</summary>
        public void Process(float[] microphone, float[] reference)
        {
            if (microphone == null || reference == null || microphone.Length != reference.Length)
                throw new ArgumentException("Supply synchronized mono samples.");
            ValidatePcm(microphone);
            ValidatePcm(reference);
            for (int i = 0; i < microphone.Length; ++i)
            {
                // Matched 80 Hz DC/high-pass conditioning, then box decimation for the coarse estimator only.
                // DTLN receives the full-band sinc-resampled PCM, without AGC ahead of the echo model.
                _micLow += HighPassAlpha * (microphone[i] - _micLow);
                _referenceLow += HighPassAlpha * (reference[i] - _referenceLow);
                _micSum += microphone[i] - _micLow;
                _referenceSum += reference[i] - _referenceLow;
                if (++_phase != Decimation) continue;
                int index = (int)(_written++ & Mask);
                _mic[index] = (float)(_micSum / Decimation);
                _reference[index] = (float)(_referenceSum / Decimation);
                _phase = 0; _micSum = _referenceSum = 0;
                // A short route is observable before the longest supported route's history
                // exists. Search only complete windows; grow the range as samples arrive.
                if (++_untilEstimate < EstimateInterval || _written < Window) continue;
                _untilEstimate = 0;
                Estimate();
            }
        }

        private static void ValidatePcm(float[] samples)
        {
            for (int i = 0; i < samples.Length; ++i)
            {
                float value = samples[i];
                if (float.IsNaN(value) || value < -1 || value > 1)
                    throw new ArgumentException("Supply finite normalized mono PCM.", nameof(samples));
            }
        }

        private void Estimate()
        {
            double microphoneEnergy = 0, renderEnergy = 0;
            for (int i = 0; i < Window; ++i)
            {
                float value = _mic[(int)((_written - Window + i) & Mask)];
                microphoneEnergy += value * value;
                value = _reference[(int)((_written - Window + i) & Mask)];
                renderEnergy += value * value;
            }
            if (renderEnergy < Window * MinimumMeanSquareEnergy) return;
            _excitedSamples += EstimateInterval;
            if (microphoneEnergy < Window * MinimumMeanSquareEnergy) return;
            int best = 0;
            double bestScore = 0;
            int maximumLag = (int)Math.Min(MaximumLag, _written - Window);
            double energy = renderEnergy;
            for (int lag = 0; lag <= maximumLag; ++lag)
            {
                // Adjacent lag windows differ by one sample. Slide the reference energy
                // instead of recomputing the same 192 ms sum for every candidate.
                if (lag > 0)
                {
                    float entering = _reference[(int)((_written - Window - lag) & Mask)];
                    float leaving = _reference[(int)((_written - lag) & Mask)];
                    energy = Math.Max(0, energy + entering * entering - leaving * leaving);
                }
                double cross = 0;
                for (int i = 0; i < Window; ++i)
                {
                    float mic = _mic[(int)((_written - Window + i) & Mask)];
                    float render = _reference[(int)((_written - Window + i - lag) & Mask)];
                    cross += mic * render;
                }
                double score = energy < Window * MinimumMeanSquareEnergy ? 0 : cross * cross / (microphoneEnergy * energy);
                _scores[lag] = score;
                if (score > bestScore) { bestScore = score; best = lag; }
            }
            double second = 0;
            for (int lag = 0; lag <= maximumLag; ++lag)
                if (Math.Abs(lag - best) > PeakExclusion) second = Math.Max(second, _scores[lag]);
            Confidence = (float)Math.Sqrt(bestScore);
            // Ambiguous/weak evidence (silence, periodic tones or double talk) cannot move the delay.
            if (bestScore < MinimumSquaredCorrelation || bestScore - second < MinimumPeakSeparation) return;
            _lags[_nextLag] = best;
            _lagTimes[_nextLag] = _excitedSamples;
            _nextLag = (_nextLag + 1) % _lags.Length;
            _lagCount = Math.Min(_lagCount + 1, _lags.Length);
            int winner = best, winnerVotes = 0, competingVotes = 0;
            for (int candidate = 0; candidate < _lagCount; ++candidate)
            {
                if (_excitedSamples - _lagTimes[candidate] > VoteLifetime) continue;
                int votes = 0;
                for (int i = 0; i < _lagCount; ++i)
                    if (_excitedSamples - _lagTimes[i] <= VoteLifetime && Math.Abs(_lags[i] - _lags[candidate]) <= DelayTolerance) ++votes;
                if (votes > winnerVotes) { winnerVotes = votes; winner = _lags[candidate]; }
            }
            for (int i = 0; i < _lagCount; ++i)
                if (_excitedSamples - _lagTimes[i] <= VoteLifetime && Math.Abs(_lags[i] - winner) > PeakExclusion) ++competingVotes;
            if (winnerVotes >= RequiredVotes && winnerVotes > competingVotes)
            {
                // Small room-response peak motion must not keep resetting DTLN's recurrent states.
                if (!_locked || Math.Abs(winner * Decimation - DelaySamples) > DelayTolerance * Decimation)
                    DelaySamples = winner * Decimation;
                _locked = true;
                _lastEvidence = _excitedSamples;
            }
            else if (_locked && Math.Abs(best * Decimation - DelaySamples) <= DelayTolerance * Decimation)
                _lastEvidence = _excitedSamples;
        }
    }
}
