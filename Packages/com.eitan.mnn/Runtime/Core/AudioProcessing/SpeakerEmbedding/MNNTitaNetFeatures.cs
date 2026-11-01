using System;
using System.Threading;

namespace MNN.Unity
{
    internal sealed class MNNTitaNetFeatures
    {
        internal const string Frontend = "nemo-2.3.0-titanet-fbank80-per-feature";
        private readonly MNNNeMoMelFeatures _mel;
        internal MNNTitaNetFeatures(MNNSpeakerModelConfig config)
        {
            if (!HasSourceCoefficients(config)) throw new ArgumentException("Expected original TitaNet frontend coefficients.", nameof(config));
            _mel = new MNNNeMoMelFeatures(config.analysis_window, config.mel_filters);
        }
        internal static bool HasSourceCoefficients(MNNSpeakerModelConfig config) => config?.analysis_window?.Length == 400 &&
            config.mel_filters?.Length == 80 * 257 && MNNSpeakerSourceProfile.HasCoefficientHash(
                "fc5e3fcef24594f3bc905d1449d8048cff6ae4aa7b873dba420e8412d6648fa8", config.analysis_window, config.mel_filters);

        internal float[] Extract(float[] pcm, out int frames, out int validFrames, CancellationToken token)
        {
            if (pcm == null || pcm.Length < 640) throw new ArgumentException("TitaNet requires at least 640 samples (40 ms at 16 kHz).", nameof(pcm));
            float[] values = _mel.Extract(pcm, 16, out frames, out validFrames, token);
            var squared = new float[validFrames];
            for (int mel = 0; mel < 80; ++mel)
            {
                token.ThrowIfCancellationRequested();
                int offset = mel * frames;
                float mean = MNNFloatCascadeSum.Sum(values, offset, validFrames) / validFrames;
                for (int frame = 0; frame < validFrames; ++frame)
                {
                    float difference = values[offset + frame] - mean;
                    squared[frame] = difference * difference;
                }
                float variance = MNNFloatCascadeSum.Sum(squared, 0, validFrames) / (validFrames - 1f);
                float deviation = (float)Math.Sqrt(variance) + 1e-5f;
                for (int frame = 0; frame < validFrames; ++frame) values[offset + frame] = (values[offset + frame] - mean) / deviation;
            }
            return values;
        }
    }
}
