using System;
using System.IO;

namespace MNN.Unity.Editor
{
    internal sealed partial class MNNStudioNativeBackend
    {
        private MNNStudioResult RunSpeakerExtraction(MNNStudioRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Audio) || string.IsNullOrWhiteSpace(request.SpeakerReference))
                throw new ArgumentException("Choose the mixture WAV and a separate recording of the target speaker.");
            var reference = _spExPlus.EncodeReferenceWaveFile(request.SpeakerReference, request.Cancellation);
            var waveform = _spExPlus.ExtractWaveFile(request.Audio, reference, out int rate, request.Cancellation);
            float peak = 0;
            foreach (float sample in waveform)
            {
                if (float.IsNaN(sample) || float.IsInfinity(sample)) throw new InvalidDataException("SpEx+ returned nonfinite audio.");
                peak = Math.Max(peak, Math.Abs(sample));
            }
            if (peak > 1)
            {
                float scale = .95f / peak;
                for (int i = 0; i < waveform.Length; ++i) waveform[i] *= scale;
            }
            return new MNNStudioResult { Text = "Target speaker extraction complete. Playback is peak normalized; the runtime API preserves the model's original amplitude.",
                Waveform = waveform, SampleRate = rate, BackendDescription = "SpEx+ WSJ0-2mix · " + _spExPlus.Backend + " / High · 8 kHz" };
        }
    }
}
