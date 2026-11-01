using System;
using System.Threading;

namespace MNN.Unity
{
    // SpeechBrain1.0.3 Fbank + InputNormalization(sentence, std_norm=False).
    // Its symmetric per-band triangles and normalized PCM differ from Kaldi fbank.
    internal sealed class MNNSpeechBrainFeatures
    {
        internal const string Frontend = "speechbrain-1.0.3-fbank80-sentence-cmn";
        private const int FftSize = 400, Hop = 160, Bins = 201, Mels = 80;
        private readonly float[] _window, _filters = new float[Bins * Mels];
        private readonly MNNRealFft400 _fft = new MNNRealFft400();

        internal MNNSpeechBrainFeatures(MNNSpeakerModelConfig config)
        {
            if (!HasSourceCoefficients(config)) throw new ArgumentException("Expected pinned SpeechBrain frontend coefficients.", nameof(config));
            _window = (float[])config.analysis_window.Clone();
            for (int bin = 0; bin < Bins; ++bin)
            for (int mel = 0; mel < Mels; ++mel)
            {
                float slope = (bin * 40f - config.mel_centers[mel]) / config.mel_bands[mel];
                _filters[bin * Mels + mel] = Math.Max(0, Math.Min(slope + 1f, -slope + 1f));
            }
        }

        internal static bool HasSourceCoefficients(MNNSpeakerModelConfig config)
        {
            if (config?.analysis_window?.Length != FftSize || config.mel_centers?.Length != Mels || config.mel_bands?.Length != Mels) return false;
            return MNNSpeakerSourceProfile.HasCoefficientHash("bb133f3aab378a364bec17726960b667550b577dc3d0699d516de7b68304bbd2",
                config.analysis_window, config.mel_centers, config.mel_bands);
        }

        internal float[] Extract(float[] pcm, out int frames, CancellationToken token)
        {
            if (pcm == null || pcm.Length < 640) throw new ArgumentException("ECAPA requires at least 640 samples (40 ms at 16 kHz).", nameof(pcm));
            frames = pcm.Length / Hop + 1;
            var features = new float[frames * Mels];
            var input = new float[FftSize]; var real = new float[Bins]; var imaginary = new float[Bins]; var power = new float[Bins];
            float maximum = float.NegativeInfinity;
            for (int frame = 0; frame < frames; ++frame)
            {
                token.ThrowIfCancellationRequested();
                for (int n = 0; n < FftSize; ++n)
                {
                    int index = frame * Hop + n - FftSize / 2;
                    // STFT's executable default is constant zero, despite its reflect docstring.
                    input[n] = index < 0 || index >= pcm.Length ? 0 : pcm[index] * _window[n];
                }
                _fft.Forward(input, real, imaginary);
                for (int bin = 0; bin < Bins; ++bin) power[bin] = real[bin] * real[bin] + imaginary[bin] * imaginary[bin];
                for (int mel = 0; mel < Mels; ++mel)
                {
                    double energy = 0;
                    for (int bin = 0; bin < Bins; ++bin) energy += (double)power[bin] * _filters[bin * Mels + mel];
                    float value = 10f * (float)Math.Log10(Math.Max(1e-10f, (float)energy));
                    features[frame * Mels + mel] = value;
                    maximum = Math.Max(maximum, value);
                }
            }
            Normalize(features, frames, maximum - 80f);
            return features;
        }

        private static void Normalize(float[] values, int frames, float floor)
        {
            for (int mel = 0; mel < Mels; ++mel)
            {
                double sum = 0;
                for (int frame = 0; frame < frames; ++frame)
                {
                    int index = frame * Mels + mel;
                    values[index] = Math.Max(floor, values[index]);
                    sum += values[index];
                }
                float mean = (float)(sum / frames);
                for (int frame = 0; frame < frames; ++frame) values[frame * Mels + mel] -= mean;
            }
        }
    }
}
