using UnityEngine;

namespace MNN.Unity.Editor
{
    /// <summary>Display gain and time-based attack/release, independent of silence detection.</summary>
    internal static class MNNStudioVoiceEnvelope
    {
        internal static float Next(float current, float rms, float seconds)
        {
            float target = Mathf.Pow(Mathf.Clamp01((rms - .003f) * 12), .55f);
            float response = target > current ? .035f : .18f;
            return Mathf.Lerp(current, target, 1 - Mathf.Exp(-Mathf.Max(0, seconds) / response));
        }
    }
}
