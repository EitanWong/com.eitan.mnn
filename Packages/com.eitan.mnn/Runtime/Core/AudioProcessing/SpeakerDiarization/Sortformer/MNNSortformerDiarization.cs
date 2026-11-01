using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>Batch-one Sortformer v2.1 with the original synchronous cache policy, driven from a complete recording.</summary>
    public sealed class MNNSortformerDiarization : IDisposable
    {
        public const int SampleRate = 16000;
        private readonly object _gate = new object();
        private readonly MNNGenerationGraph _preencode, _core;
        private readonly MNNNeMoMelFeatures _features;
        private bool _disposed;
        public MNNBackendType PreencodeBackend => _preencode.Backend;
        public MNNBackendType CoreBackend => _core.Backend;

        private MNNSortformerDiarization(string directory, int threads, MNNBackendType backend)
        {
            if (!MNNSortformerModel.IsComplete(directory)) throw new InvalidDataException("Sortformer profile or graph verification failed.");
            var config = MNNSortformerConfig.Read(directory);
            _features = new MNNNeMoMelFeatures(config.analysis_window, config.mel_filters, 128, true);
            if (backend == MNNBackendType.Auto) backend = MNNBackendType.CPU;
            _preencode = new MNNGenerationGraph(Path.Combine(directory, MNNSortformerModel.Preencode), threads,
                backendType: backend, precisionMode: MNNPrecisionMode.High, gpuTuningMode: MNNGpuTuningMode.None,
                recreateGpuSessionOnShapeChange: true);
            try
            {
                _core = new MNNGenerationGraph(Path.Combine(directory, MNNSortformerModel.Core), threads,
                    backendType: backend, precisionMode: MNNPrecisionMode.High, gpuTuningMode: MNNGpuTuningMode.None,
                    recreateGpuSessionOnShapeChange: true);
            }
            catch
            {
                _preencode.Dispose();
                throw;
            }
        }

        public static MNNSortformerDiarization Load(string directory, int threadCount = 4, MNNBackendType backendType = MNNBackendType.Auto)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentNullException(nameof(directory));
            if (threadCount < 1 || threadCount > 64) throw new ArgumentOutOfRangeException(nameof(threadCount));
            MNNAcceleration.Validate(backendType);
            if (backendType != MNNBackendType.CPU && backendType != MNNBackendType.Auto && backendType != MNNBackendType.Metal)
                throw new NotSupportedException("Sortformer accelerator placement is unverified on this backend.");
            MNNPlatformSupport.RequireSupported();
            return new MNNSortformerDiarization(Path.GetFullPath(directory), threadCount, backendType);
        }

        /// <summary>Analyze 10 ms–60 s mono PCM. Four overlapping speaker slots are local to this recording.</summary>
        public MNNSpeakerActivity Analyze(float[] samples, int sampleRate = SampleRate, CancellationToken cancellationToken = default)
        {
            Validate(samples, sampleRate, cancellationToken);
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(MNNSortformerDiarization));
                var pcm = MNNSpeechDsp.Resample(samples, sampleRate, SampleRate, cancellationToken);
                if (pcm.Length < 160 || pcm.Length > 960000) throw new ArgumentException("Expected 160–960000 samples after resampling.", nameof(samples));
                var values = _features.Extract(pcm, 16, out int paddedFrames, out int validFrames, cancellationToken);
                return new MNNSpeakerActivity(AnalyzeFeatures(values, paddedFrames, validFrames, cancellationToken), (double)samples.Length / sampleRate);
            }
        }

        public MNNSpeakerActivity AnalyzeWaveFile(string path, CancellationToken cancellationToken = default)
        {
            var samples = MNNWaveAudio.Read(path, out int sampleRate);
            return Analyze(samples, sampleRate, cancellationToken);
        }

        private float[] AnalyzeFeatures(float[] features, int paddedFrames, int validFrames, CancellationToken token)
        {
            var cache = new MNNSortformerCache();
            var result = new List<float>(checked((validFrames + 7) / 8 * 4));
            for (int start = 0; start < validFrames; start += 188 * 8)
            {
                token.ThrowIfCancellationRequested();
                int end = Math.Min(start + 188 * 8, validFrames);
                int left = Math.Min(8, start), right = Math.Min(8, validFrames - end);
                int frames = end - start + left + right;
                var chunk = new float[checked(frames * 128)];
                for (int t = 0; t < frames; ++t)
                    for (int mel = 0; mel < 128; ++mel) chunk[t * 128 + mel] = features[mel * paddedFrames + start - left + t];
                var encoded = _preencode.RunOutputs(new[] { "preencoded" },
                    new MNNGenerationGraph.Input("features", chunk, 1, frames, 128),
                    new MNNGenerationGraph.Input("lengths", new[] { frames }, 1))[0];
                int encodedFrames = (frames + 7) / 8;
                RequireShape(encoded, encodedFrames, 512, "preencoder");
                var combined = MNNSortformerCache.Concat(cache.Embeddings, encoded.Values);
                int coreFrames = combined.Length / 512;
                var output = _core.RunOutputs(new[] { "predictions" },
                    new MNNGenerationGraph.Input("embeddings", combined, 1, coreFrames, 512),
                    new MNNGenerationGraph.Input("lengths", new[] { coreFrames }, 1))[0];
                RequireShape(output, coreFrames, 4, "core");
                var probabilities = output.Values;
                int leftFrames = left / 8, rightFrames = (right + 7) / 8;
                int first = (cache.Count + leftFrames) * 4;
                int count = (encodedFrames - leftFrames - rightFrames) * 4;
                for (int i = first; i < first + count; ++i)
                {
                    if (probabilities[i] < 0 || probabilities[i] > 1) throw new InvalidDataException("Sortformer returned an invalid probability.");
                    result.Add(probabilities[i]);
                }
                cache.Update(encoded.Values, probabilities, leftFrames, rightFrames);
                token.ThrowIfCancellationRequested();
            }
            return result.ToArray();
        }

        private static void RequireShape(MNNGenerationGraph.Output output, int frames, int channels, string stage)
        {
            var shape = output.Shape;
            if (shape.Length != 3 || shape[0] != 1 || shape[1] != frames || shape[2] != channels)
                throw new InvalidDataException("Unexpected Sortformer " + stage + " output shape.");
        }

        private static void Validate(float[] samples, int sampleRate, CancellationToken token)
        {
            if (samples == null || samples.Length == 0) throw new ArgumentException("Supply nonempty mono PCM.", nameof(samples));
            if (sampleRate < 8000 || sampleRate > 96000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (samples.Length * 100L < sampleRate || samples.Length > sampleRate * 60L)
                throw new ArgumentException("Supply 10 ms–60 s of audio.", nameof(samples));
            for (int i = 0; i < samples.Length; ++i)
            {
                if ((i & 16383) == 0) token.ThrowIfCancellationRequested();
                if (float.IsNaN(samples[i]) || float.IsInfinity(samples[i]) || Math.Abs(samples[i]) > 1)
                    throw new ArgumentException("Supply finite normalized PCM in [-1,1].", nameof(samples));
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                _core.Dispose();
                _preencode.Dispose();
            }
        }
    }
}
