using System;

namespace MNN.Unity
{
    /// <summary>Frame-rate-independent attack/release for normalized audio-driven controls.</summary>
    /// <remarks>Own one envelope per animated value. Analysis stays on its audio writer;
    /// advance this envelope on the consumer's clock using an immutable feature snapshot.</remarks>
    public sealed class MNNAudioEnvelope
    {
        private float velocity;
        public float Value { get; private set; }
        public float AttackSeconds { get; }
        public float ReleaseSeconds { get; }

        public MNNAudioEnvelope(float attackSeconds = .065f, float releaseSeconds = .18f)
        {
            RequireDuration(attackSeconds); RequireDuration(releaseSeconds);
            AttackSeconds = attackSeconds; ReleaseSeconds = releaseSeconds;
        }

        public float Update(float target, float deltaTime)
        {
            float value = Value;
            Smooth(ref value, ref velocity, target, target > value ? AttackSeconds : ReleaseSeconds, deltaTime);
            Value = value;
            return value;
        }

        public void Reset() { Value = 0; velocity = 0; }

        /// <summary>Map normalized RMS/peak to speech loudness. Values at or below RMS .002 are gated.</summary>
        public static float Measure(float rms, float peak, float sensitivity = 2f)
        {
            if (!Finite(rms) || rms <= .002f) return 0;
            if (!Finite(peak)) peak = rms;
            if (!Finite(sensitivity)) sensitivity = 2f;
            float measured = rms * .8f + Math.Min(Math.Max(peak, rms), rms * 4f) * .2f;
            float gain = Math.Max(.1f, Math.Min(6f, sensitivity)) * .5f;
            double decibels = 20 * Math.Log10(Math.Max(.000001, measured * gain));
            return Bound((float)((decibels + 48) / 36));
        }

        /// <summary>Advance an externally owned value/velocity pair with the same exact damped response.</summary>
        /// <remarks>Duration is .02–5 seconds; deltaTime must be finite and nonnegative.
        /// The caller owns both state values and must not share them between writers.</remarks>
        public static void Smooth(ref float value, ref float velocity, float target, float duration, float deltaTime)
        {
            if (!Finite(value) || !Finite(velocity) || !Finite(target))
                throw new ArgumentOutOfRangeException(nameof(target), "Envelope state and target must be finite.");
            RequireDuration(duration);
            if (!Finite(deltaTime) || deltaTime < 0) throw new ArgumentOutOfRangeException(nameof(deltaTime));
            target = Bound(target);
            double omega = 6.64 / duration;
            double displacement = value - target;
            double coefficient = velocity + omega * displacement;
            double decay = Math.Exp(-omega * deltaTime);
            value = Bound((float)(target + (displacement + coefficient * deltaTime) * decay));
            velocity = (float)((velocity - omega * coefficient * deltaTime) * decay);
        }

        private static void RequireDuration(float duration)
        {
            if (!Finite(duration) || duration < .02f || duration > 5f) throw new ArgumentOutOfRangeException(nameof(duration));
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static float Bound(float value) => Math.Max(0, Math.Min(1, value));
    }
}
