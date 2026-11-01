using System;

namespace MNN.Unity
{
    /// <summary>Immutable measurements of captured or rendered PCM, independent of visualization.</summary>
    /// <remarks>RMS/peak and band fractions are [0,1]. Pitch is a 70–450 Hz voiced estimate,
    /// valid only with sufficient PitchConfidence. Onset measures rising block energy.</remarks>
    public readonly struct MNNAudioFeatures
    {
        public readonly float Rms, Peak, LowBand, HighBand, PitchHz, PitchConfidence, Onset;
        public MNNAudioFeatures(float rms, float peak, float lowBand = 0, float highBand = 0,
            float pitchHz = 0, float pitchConfidence = 0, float onset = 0)
        {
            Rms = Bound(rms, 1); Peak = Bound(peak, 1);
            LowBand = Bound(lowBand, 1); HighBand = Bound(highBand, 1);
            PitchHz = Bound(pitchHz, 500); PitchConfidence = Bound(pitchConfidence, 1); Onset = Bound(onset, 1);
        }
        public MNNAudioFeatures WithGain(float gain) => new MNNAudioFeatures(Rms * gain, Peak * gain,
            LowBand, HighBand, PitchHz, PitchConfidence, gain > 0 ? Onset : 0);
        /// <summary>Perceptual speech level in [0,1], retaining consonant peaks and loud-syllable headroom.</summary>
        public float GetEnvelope(float sensitivity = 2f) => MNNAudioEnvelope.Measure(Rms, Peak, sensitivity);

        private static float Bound(float value, float max) => float.IsNaN(value) || float.IsInfinity(value)
            ? 0 : Math.Max(0, Math.Min(max, value));
    }
}
