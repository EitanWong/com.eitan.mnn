using System;

namespace MNN.Unity
{
    /// <summary>Improved proportionate NLMS (alpha=0) estimates the linear room response after bulk-delay alignment.
    /// Its residual power bounds suppression of DTLN output. One processing worker owns this state.</summary>
    public sealed class MNNAdaptiveEchoFilter
    {
        // 128 ms acoustic tail, separate from the estimator's 0–500 ms bulk device delay.
        public const int TapCount = 16000 * 128 / 1000;
        private const int Taps = TapCount;
        // Refresh the proportionate preconditioner every 1 ms. Prediction, normalized
        // projection and coefficient updates still run for every sample, including double talk.
        private const int ProportionInterval = 16;
        private const double MinimumMeanSquareEnergy = 1e-7;
        private readonly float[] _history = new float[Taps * 2], _weights = new float[Taps], _proportions = new float[Taps];
        private int _head, _untilProportions;
        private double _power;
        public float ResidualPowerRatio { get; private set; } = 1;
        public void Reset()
        {
            Array.Clear(_history, 0, _history.Length); Array.Clear(_weights, 0, _weights.Length);
            _head = _untilProportions = 0; _power = 0; ResidualPowerRatio = 1;
        }
        // Measure on the same causal reference used by the adaptive filter, including
        // its stored samples. A zero-lag comparison against the bulk-aligned neural
        // reference mistakes permitted delay error for double talk and freezes tracking.
        // Call before Process appends the current reference hop. No lookahead or allocation.
        /// <summary>Measures squared coherence across causal lags from zero through maximumLag (at most TapCount).</summary>
        public float EstimateCoherence(float[] microphone, float[] reference, int maximumLag)
        {
            ValidateInputs(microphone, reference);
            if (maximumLag < 0 || maximumLag > TapCount)
                throw new ArgumentOutOfRangeException(nameof(maximumLag));
            double microphonePower = 0, best = 0;
            for (int i = 0; i < microphone.Length; ++i) microphonePower += microphone[i] * (double)microphone[i];
            if (microphonePower <= microphone.Length * MinimumMeanSquareEnergy) return 0;
            for (int lag = 0; lag <= maximumLag; ++lag)
            {
                double cross = 0, referencePower = 0;
                int stored = Math.Min(lag, microphone.Length);
                for (int i = 0; i < stored; ++i)
                {
                    float value = _history[_head + lag - i - 1];
                    cross += microphone[i] * (double)value; referencePower += value * (double)value;
                }
                for (int i = stored; i < microphone.Length; ++i)
                {
                    float value = reference[i - lag];
                    cross += microphone[i] * (double)value; referencePower += value * (double)value;
                }
                if (referencePower > microphone.Length * MinimumMeanSquareEnergy)
                    best = Math.Max(best, cross * cross / (microphonePower * referencePower));
            }
            return (float)Math.Min(1, best);
        }

        /// <summary>Writes one residual per input sample with adaptation in [0,1]. Output must not alias either input.
        /// Validate before advancing filter state; processing reuses all storage.</summary>
        public void Process(float[] microphone, float[] reference, float[] residual, float adaptation = 1)
        {
            ValidateInputs(microphone, reference);
            if (residual == null || residual.Length != microphone.Length ||
                ReferenceEquals(residual, microphone) || ReferenceEquals(residual, reference))
                throw new ArgumentException("Supply separate, equally sized residual storage.", nameof(residual));
            if (float.IsNaN(adaptation) || adaptation < 0 || adaptation > 1)
                throw new ArgumentOutOfRangeException(nameof(adaptation));
            double microphonePower = 0, residualPower = 0;
            for (int sample = 0; sample < microphone.Length; ++sample)
            {
                if (--_head < 0) _head = Taps - 1;
                double previous = _history[_head], value = reference[sample];
                _power = Math.Max(0, _power + value * value - previous * previous);
                _history[_head] = _history[_head + Taps] = (float)value;
                double estimate = 0;
                for (int tap = 0; tap < Taps; ++tap)
                {
                    estimate += _weights[tap] * (double)_history[_head + tap];
                }
                double error = microphone[sample] - estimate;
                residual[sample] = (float)Math.Max(-1, Math.Min(1, error));
                microphonePower += microphone[sample] * (double)microphone[sample];
                residualPower += residual[sample] * (double)residual[sample];
                // Normalized projection step in [0,1], within the NLMS stability interval (0,2).
                // Freeze without excitation; the caller reduces adaptation during double talk.
                if (adaptation == 0 || _power <= Taps * MinimumMeanSquareEnergy) continue;
                if (_untilProportions == 0)
                {
                    RefreshProportions();
                    _untilProportions = ProportionInterval;
                }
                --_untilProportions;
                double normalizedPower = 0;
                for (int tap = 0; tap < Taps; ++tap)
                {
                    value = _history[_head + tap];
                    normalizedPower += _proportions[tap] * value * value;
                }
                if (normalizedPower <= 0) continue;
                float correction = adaptation * (float)(error / normalizedPower);
                for (int tap = 0; tap < Taps; ++tap)
                    _weights[tap] += correction * _proportions[tap] * _history[_head + tap];
            }
            ResidualPowerRatio = microphonePower > 0 ? (float)Math.Min(1, residualPower / microphonePower) : 1;
        }

        private static void ValidateInputs(float[] microphone, float[] reference)
        {
            if (microphone == null || reference == null || microphone.Length != reference.Length)
                throw new ArgumentException("Supply synchronized mono samples.");
            for (int i = 0; i < microphone.Length; ++i)
            {
                float mic = microphone[i], render = reference[i];
                if (float.IsNaN(mic) || mic < -1 || mic > 1 || float.IsNaN(render) || render < -1 || render > 1)
                    throw new ArgumentException("Supply finite normalized mono PCM.");
            }
        }

        private void RefreshProportions()
        {
            // IPNLMS alpha=0, with the common .5 / sum(abs(weights)) scale cancelled.
            // Cache only the preconditioner; never cache prediction or skip frozen-path echo removal.
            double magnitude = 0;
            for (int tap = 0; tap < Taps; ++tap) magnitude += Math.Abs(_weights[tap]);
            float uniform = (float)(magnitude / Taps);
            for (int tap = 0; tap < Taps; ++tap)
                _proportions[tap] = magnitude > 0 ? uniform + Math.Abs(_weights[tap]) : 1;
        }
    }
}
