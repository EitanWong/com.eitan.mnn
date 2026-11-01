using System;
using System.IO;

namespace MNN.Unity.Editor
{
    internal sealed partial class MNNStudioNativeBackend
    {
        private MNNStudioResult RunGestureExtraction(MNNStudioRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Audio) || string.IsNullOrWhiteSpace(request.GestureFeatures))
                throw new ArgumentException("Choose the mixture WAV and its synchronized SEG gesture features JSON.");
            var gestures = MNNSegGestureFeatures.ReadJson(request.GestureFeatures, request.Cancellation);
            var waveform = _seg.ExtractWaveFile(request.Audio, gestures, out int rate, request.Cancellation);
            float peak = 0;
            foreach (float sample in waveform)
            {
                if (float.IsNaN(sample) || float.IsInfinity(sample)) throw new InvalidDataException("SEG returned nonfinite audio.");
                peak = Math.Max(peak, Math.Abs(sample));
            }
            if (peak > 1)
            {
                float scale = .95f / peak;
                for (int i = 0; i < waveform.Length; ++i) waveform[i] *= scale;
            }
            return new MNNStudioResult { Text = "Gesture-conditioned target extraction complete. Playback is peak normalized; the runtime API preserves the model's original amplitude.",
                Waveform = waveform, SampleRate = rate, BackendDescription = "SEG YGD · " + _seg.Backend + " / High · 16 kHz" };
        }
    }
}
