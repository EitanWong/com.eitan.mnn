using System;
using System.IO;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>Offline Resemble denoising with the original 44.1 kHz single-chunk inference contract.</summary>
    public sealed class MNNResembleDenoiser : IDisposable
    {
        public const int SampleRate = 44100, MinimumSamples = 400, MaximumSamples = 132300;
        private readonly object _gate = new object();
        private readonly MNNGenerationGraph _graph;
        private readonly MNNResembleDenoiserSpectral _spectral;
        private bool _disposed;
        public MNNBackendType Backend => _graph.Backend;

        private MNNResembleDenoiser(string directory, int threads)
        {
            if (!MNNResembleDenoiserModel.IsComplete(directory)) throw new InvalidDataException("Resemble Denoiser graph or profile verification failed.");
            var config = MNNResembleDenoiserConfig.Read(directory);
            _spectral = new MNNResembleDenoiserSpectral(config.analysis_window);
            _graph = new MNNGenerationGraph(Path.Combine(directory, config.graph), threads,
                backendType: MNNBackendType.CPU, precisionMode: MNNPrecisionMode.High);
        }

        public static MNNResembleDenoiser Load(string directory, int threadCount = 8, MNNBackendType backendType = MNNBackendType.Auto)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentNullException(nameof(directory));
            if (threadCount < 1 || threadCount > 64) throw new ArgumentOutOfRangeException(nameof(threadCount));
            MNNAcceleration.Validate(backendType);
            if (backendType != MNNBackendType.Auto && backendType != MNNBackendType.CPU)
                throw new NotSupportedException("Resemble Denoiser GPU/NPU candidates have not passed the original waveform accuracy gate; use CPU or Auto.");
            MNNPlatformSupport.RequireSupported();
            return new MNNResembleDenoiser(Path.GetFullPath(directory), threadCount);
        }

        /// <summary>Denoise 400–132300 resampled samples; restore the caller's sample rate and output length.</summary>
        public float[] Enhance(float[] samples, int sampleRate = SampleRate, CancellationToken cancellationToken = default)
        {
            Validate(samples, sampleRate, cancellationToken);
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(MNNResembleDenoiser));
                var pcm = MNNSpeechDsp.Resample(samples, sampleRate, SampleRate, cancellationToken);
                if (pcm.Length < MinimumSamples || pcm.Length > MaximumSamples)
                    throw new ArgumentException("Supply 400–132300 samples after resampling to 44.1 kHz.", nameof(samples));
                var features = _spectral.Analyze(pcm, out int frames, out float scale, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                var output = _graph.RunOutputs(new[] { "spectrum" }, new MNNGenerationGraph.Input("features", features, 1, 3, 841, frames))[0];
                if (output.Shape.Length != 4 || output.Shape[0] != 1 || output.Shape[1] != 2 || output.Shape[2] != 841 || output.Shape[3] != frames)
                    throw new InvalidDataException("Unexpected Resemble Denoiser spectrum shape.");
                foreach (float value in output.Values)
                    if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidDataException("Nonfinite Resemble Denoiser spectrum.");
                var result = _spectral.Synthesize(output.Values, frames, pcm.Length, scale, cancellationToken);
                if (sampleRate != SampleRate) result = MNNSpeechDsp.Resample(result, SampleRate, sampleRate, cancellationToken);
                if (result.Length != samples.Length) Array.Resize(ref result, samples.Length);
                return result;
            }
        }

        public float[] EnhanceWaveFile(string path, out int sampleRate, CancellationToken cancellationToken = default)
        {
            var samples = MNNWaveAudio.Read(path, out sampleRate);
            return Enhance(samples, sampleRate, cancellationToken);
        }

        private static void Validate(float[] samples, int rate, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (samples == null || samples.Length == 0) throw new ArgumentException("Supply nonempty mono PCM.", nameof(samples));
            if (rate < 8000 || rate > 96000) throw new ArgumentOutOfRangeException(nameof(rate));
            if (samples.Length > (long)rate * 3) throw new ArgumentException("Supply at most three seconds of PCM.", nameof(samples));
            for (int i = 0; i < samples.Length; ++i)
            {
                if ((i & 16383) == 0) token.ThrowIfCancellationRequested();
                if (float.IsNaN(samples[i]) || float.IsInfinity(samples[i]))
                    throw new ArgumentException("Supply finite mono PCM.", nameof(samples));
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true; _graph.Dispose();
            }
        }
    }
}
