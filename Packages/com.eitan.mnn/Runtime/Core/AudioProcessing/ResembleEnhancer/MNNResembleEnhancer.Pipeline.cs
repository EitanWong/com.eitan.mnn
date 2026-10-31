using System.IO;
using System.Threading;

namespace MNN.Unity
{
    public sealed partial class MNNResembleEnhancer
    {
        private float[] EnhanceCore(float[] pcm, CancellationToken token)
        {
            float[] padded = _dsp.Prepare(pcm, out float scale);
            float[] originalMel = _dsp.Mel(padded, token);
            int frames = originalMel.Length / 128;
            float[] features = _dsp.DenoiserFeatures(padded, out int denoiserFrames, token);
            token.ThrowIfCancellationRequested();
            float[] spectrum = MNNResembleEnhancerGraphs.Run(_graphs.Denoiser, "spectrum", new[] { 1, 2, 841, denoiserFrames },
                new MNNGenerationGraph.Input("features", features, 1, 3, 841, denoiserFrames));
            float[] denoised = _dsp.DenoiserWave(spectrum, denoiserFrames, padded.Length, token);
            float[] condition = _dsp.Mel(denoised, token);
            for (int i = 0; i < condition.Length; ++i) condition[i] = .5f * condition[i] + .5f * originalMel[i];
            MNNResembleEnhancerDsp.NormalizeCondition(condition, frames);
            token.ThrowIfCancellationRequested();
            float[] latent = MNNResembleEnhancerGraphs.Run(_graphs.Encoder, "output", new[] { 1, 64, frames },
                new MNNGenerationGraph.Input("mel", originalMel, 1, 128, frames));
            var random = new MNNSeededNormal(0);
            var prior = new float[latent.Length];
            random.Fill(prior);
            for (int i = 0; i < latent.Length; ++i) latent[i] = .5f * prior[i] + .5f * (latent[i] * 6f);
            Integrate(latent, condition, frames, token);
            for (int i = 0; i < latent.Length; ++i) latent[i] /= 6f;
            token.ThrowIfCancellationRequested();
            float[] decoded = MNNResembleEnhancerGraphs.Run(_graphs.Decoder, "output", new[] { 1, 160, frames },
                new MNNGenerationGraph.Input("latent", latent, 1, 64, frames));
            var noise = new float[128 * (frames + 10)];
            random.Fill(noise);
            token.ThrowIfCancellationRequested();
            float[] wave = _graphs.Vocoder.Run("output",
                new MNNGenerationGraph.Input("features", decoded, 1, 160, frames),
                new MNNGenerationGraph.Input("noise", noise, 1, 128, frames + 10));
            _graphs.RequireQualifiedBackends();
            if (wave.Length < pcm.Length)
                throw new InvalidDataException("Resemble Enhance vocoder returned a short waveform.");
            var output = new float[pcm.Length];
            for (int i = 0; i < output.Length; ++i)
            {
                output[i] = wave[i] * scale;
                if (float.IsNaN(output[i]) || float.IsInfinity(output[i]))
                    throw new InvalidDataException("Nonfinite Resemble Enhance waveform after restoring amplitude.");
            }
            token.ThrowIfCancellationRequested();
            return output;
        }

        private void Integrate(float[] latent, float[] condition, int frames, CancellationToken token)
        {
            var midpoint = new float[latent.Length];
            for (int step = 0; step < 16; ++step)
            {
                token.ThrowIfCancellationRequested();
                float[] first = Velocity(latent, condition, frames, step * 2);
                for (int i = 0; i < midpoint.Length; ++i) midpoint[i] = latent[i] + _config.dt[step] * first[i] / 2f;
                token.ThrowIfCancellationRequested();
                float[] second = Velocity(midpoint, condition, frames, step * 2 + 1);
                for (int i = 0; i < latent.Length; ++i) latent[i] += _config.dt[step] * second[i];
            }
        }

        private float[] Velocity(float[] state, float[] condition, int frames, int embedding) =>
            MNNResembleEnhancerGraphs.Run(_graphs.Velocity, "output", new[] { 1, 64, frames },
                new MNNGenerationGraph.Input("state", state, 1, 64, frames),
                new MNNGenerationGraph.Input("condition", condition, 1, 128, frames),
                new MNNGenerationGraph.Input("embedding", _embeddings[embedding], 1, 128));
    }
}
