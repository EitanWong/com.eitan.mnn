using System;

namespace MNN.Unity.Editor
{
    internal sealed partial class MNNStudioNativeBackend
    {
        private MNNResembleEnhancer _resembleEnhancer;

        private MNNStudioResult RunResembleEnhancer(MNNStudioRequest request)
        {
            request.Cancellation.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(request.Audio))
                throw new ArgumentException("Supply a 44.1 kHz mono WAV with 400–132300 samples for Resemble Enhance.");
            float[] waveform = _resembleEnhancer.EnhanceWaveFile(request.Audio, out int sampleRate, request.Cancellation);
            return new MNNStudioResult
            {
                Text = "Resemble CFM speech enhancement complete.",
                Waveform = waveform,
                SampleRate = sampleRate,
                BackendDescription = "Resemble-Enhance-44K · denoiser " + _resembleEnhancer.DenoiserBackend +
                    " / encoder " + _resembleEnhancer.EncoderBackend + " / velocity " + _resembleEnhancer.VelocityBackend +
                    " / decoder " + _resembleEnhancer.DecoderBackend + " / vocoder " + _resembleEnhancer.VocoderBackend +
                    " · High / CPU4 / 32 NFE midpoint / seed 0"
            };
        }

        private void DisposeResembleEnhancer()
        {
            _resembleEnhancer?.Dispose();
            _resembleEnhancer = null;
        }
    }
}
