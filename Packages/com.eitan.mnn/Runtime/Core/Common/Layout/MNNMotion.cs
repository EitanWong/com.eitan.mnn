using UnityEngine;

namespace MNN.Unity
{
    /// <summary>Analytic, velocity-preserving springs, independent of frame rate and time scale.</summary>
    public static class MNNMotion
    {
        public static void Spring(ref float position, ref float velocity, float target, float deltaTime,
            float frequency = 18f, float damping = .82f)
        {
            if (deltaTime <= 0f || position == target && velocity == 0f) return;
            damping = Mathf.Clamp(damping, .05f, .99f);
            float decay = damping * frequency;
            float oscillation = frequency * Mathf.Sqrt(1f - damping * damping);
            float offset = position - target;
            float sine = Mathf.Sin(oscillation * deltaTime);
            float cosine = Mathf.Cos(oscillation * deltaTime);
            float envelope = Mathf.Exp(-decay * deltaTime);
            float coefficient = (velocity + decay * offset) / oscillation;
            position = target + envelope * (offset * cosine + coefficient * sine);
            velocity = envelope * (velocity * cosine -
                (decay * coefficient + offset * oscillation) * sine);
        }

        /// <summary>Asymptotic resistance in logical points; stretching never exceeds the limit.</summary>
        public static float RubberBand(float distance, float limit = 28f)
        {
            if (limit <= 0f) return 0f;
            return Mathf.Sign(distance) * limit * (1f - 1f / (1f + Mathf.Abs(distance) * .55f / limit));
        }

        public static float ProjectDrawer(float position, float velocity, Vector2 range, float coastTime) =>
            Mathf.Clamp(position + velocity * coastTime, range.x, range.y);

        /// <summary>Retarget a rectangle without restarting its position or size velocity.</summary>
        public static void SpringRect(ref Rect bounds, ref Vector4 velocity, Rect target, float deltaTime,
            float frequency = 30f, float damping = .99f)
        {
            float x = bounds.x, y = bounds.y, width = bounds.width, height = bounds.height;
            Spring(ref x, ref velocity.x, target.x, deltaTime, frequency, damping);
            Spring(ref y, ref velocity.y, target.y, deltaTime, frequency, damping);
            Spring(ref width, ref velocity.z, target.width, deltaTime, frequency, damping);
            Spring(ref height, ref velocity.w, target.height, deltaTime, frequency, damping);
            bounds = new Rect(x, y, Mathf.Max(0f, width), Mathf.Max(0f, height));
            if ((bounds.position - target.position).sqrMagnitude < .0001f &&
                (bounds.size - target.size).sqrMagnitude < .0001f && velocity.sqrMagnitude < .01f)
            {
                bounds = target;
                velocity = Vector4.zero;
            }
        }

        // Exact constant-force integration preserves velocity on retarget and release at any render rate.
        public static void Spring(ref Vector3 position, ref Vector3 velocity, Vector3 target,
            float omega, float damping, float dt)
        {
            Vector3 x = position - target;
            if (damping >= 1f)
            {
                Vector3 c = velocity + omega * x;
                float decay = Mathf.Exp(-omega * dt);
                position = target + (x + c * dt) * decay;
                velocity = (velocity - omega * c * dt) * decay;
                return;
            }
            float a = omega * damping, b = omega * Mathf.Sqrt(1f - damping * damping);
            float s = Mathf.Sin(b * dt), co = Mathf.Cos(b * dt), e = Mathf.Exp(-a * dt);
            Vector3 coefficient = (velocity + a * x) / b;
            position = target + e * (x * co + coefficient * s);
            velocity = e * (velocity * co - (a * velocity + omega * omega * x) * (s / b));
        }
    }
}
