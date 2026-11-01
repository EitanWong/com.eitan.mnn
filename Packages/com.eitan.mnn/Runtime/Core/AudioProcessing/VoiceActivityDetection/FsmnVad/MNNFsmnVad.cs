using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    /// <summary>FunASR FSMN streaming VAD with managed fbank/LFR and recurrent cache ownership.</summary>
    public sealed class MNNFsmnVad : IDisposable
    {
        private const int FeatureBins = 80, LfrM = 5, FeatureDimension = FeatureBins * LfrM, CacheCount = 4, PdfCount = 248;
        private readonly object _gate = new object();
        private readonly MNNGenerationGraph _graph;
        private readonly MNNFsmnVadConfig _config;
        private readonly float[][] _caches = new float[CacheCount][];
        private bool _disposed;

        private MNNFsmnVad(string directory, int threads, MNNBackendType backend)
        {
            var config = MNNFsmnVadConfig.Read(directory);
            if (!MNNFsmnVadModel.IsRepository(config.repository) || config.sample_rate != 16000 || config.feature_bins != FeatureBins || config.lfr_m != LfrM || config.lfr_n != 1 || config.graph != MNNFsmnVadModel.Graph)
                throw new InvalidDataException("Unsupported FSMN VAD profile.");
            MNNFsmnFeatures.ValidateCmvn(config);
            _config = config;
            MNNFsmnVadModel.Verify(directory);
            for (int i = 0; i < CacheCount; ++i) _caches[i] = new float[1 * 128 * 19 * 1];
            _graph = new MNNGenerationGraph(Path.Combine(directory, MNNFsmnVadModel.Graph), threads, backendType: backend,
                precisionMode: MNNPrecisionMode.High);
        }

        public static MNNFsmnVad Load(string directory, int threadCount = 1, MNNBackendType backendType = MNNBackendType.CPU)
        {
            MNNPlatformSupport.RequireSupported();
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentNullException(nameof(directory));
            if (threadCount < 1 || threadCount > 64) throw new ArgumentOutOfRangeException(nameof(threadCount));
            if (!Enum.IsDefined(typeof(MNNBackendType), backendType)) throw new ArgumentOutOfRangeException(nameof(backendType));
            return new MNNFsmnVad(Path.GetFullPath(directory), threadCount, backendType);
        }

        public MNNBackendType Backend => _graph.Backend;

        public MNNVoiceActivityResult Detect(float[] samples, int sampleRate, float threshold = .6f, CancellationToken cancellationToken = default)
        {
            if (samples == null || samples.Length == 0 || samples.Length > (long)sampleRate * 600) throw new ArgumentException("Supply nonempty mono audio up to ten minutes.", nameof(samples));
            if (sampleRate < 8000 || sampleRate > 96000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (threshold < .01f || threshold > .99f || float.IsNaN(threshold)) throw new ArgumentOutOfRangeException(nameof(threshold));
            lock (_gate)
            {
                ThrowIfDisposed();
                Validate(samples, cancellationToken);
                var pcm = MNNSpeechDsp.Resample(samples, sampleRate, 16000, cancellationToken);
                var features = MNNFsmnFeatures.ExtractLfr(pcm, cancellationToken, LfrM, 1);
                MNNFsmnFeatures.ApplyCmvn(features, _config);
                int frames = features.Length / FeatureDimension;
                var probabilities = new List<float>(frames);
                ResetCaches();
                const int chunkFrames = 32;
                var chunk = new float[chunkFrames * FeatureDimension];
                var inputs = new MNNGenerationGraph.Input[CacheCount + 1];
                inputs[0] = new MNNGenerationGraph.Input("speech", chunk, 1, chunkFrames, FeatureDimension);
                for (int i = 0; i < CacheCount; ++i) inputs[i + 1] = new MNNGenerationGraph.Input("in_cache" + i, _caches[i], 1, 128, 19, 1);
                for (int first = 0; first < frames; first += chunkFrames)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int count = Math.Min(chunkFrames, frames - first);
                    Array.Clear(chunk, 0, chunk.Length);
                    Array.Copy(features, first * FeatureDimension, chunk, 0, count * FeatureDimension);
                    // This causal model permits trailing padding. Keep the Metal
                    // session shape fixed; resizing the recurrent tail corrupts
                    // its cache on the validated MNN 3.6.2 build.
                    var outputs = _graph.RunOutputs(new[]{"logits", "out_cache0", "out_cache1", "out_cache2", "out_cache3"}, inputs);
                    for (int i = 0; i < CacheCount; ++i) Array.Copy(outputs[i + 1].Values, _caches[i], _caches[i].Length);
                    var logits = outputs[0].Values;
                    for (int frame = 0; frame < count; ++frame) probabilities.Add(SpeechProbability(logits, frame * PdfCount));
                }
                return new MNNVoiceActivityResult(Segment(probabilities.ToArray(), pcm.Length, threshold), probabilities.ToArray(), (double)samples.Length / sampleRate);
            }
        }

        public MNNVoiceActivityResult DetectWaveFile(string path, float threshold = .6f, CancellationToken cancellationToken = default)
        {
            var samples = MNNWaveAudio.Read(path, out int rate);
            return Detect(samples, rate, threshold, cancellationToken);
        }
        public Task<MNNVoiceActivityResult> DetectAsync(float[] samples, int sampleRate, float threshold = .6f, CancellationToken cancellationToken = default)
        {
            var copy = samples == null ? null : (float[])samples.Clone();
            return Task.Run(() => Detect(copy, sampleRate, threshold, cancellationToken), cancellationToken);
        }
        public void Reset() { lock (_gate) { ThrowIfDisposed(); ResetCaches(); } }

        internal static float SpeechProbability(float[] logits, int offset)
        {
            // The source graph already applies Softmax across its 248 PDFs.
            return Math.Max(0, Math.Min(1, 1 - logits[offset]));
        }

        internal static MNNSpeechSegment[] Segment(float[] probabilities, int samples, float threshold)
        {
            const int frameSamples = 160, minSpeech = 2400, minSilence = 2400, pad = 1600;
            var intervals = new List<int[]>();
            int start = -1, pendingEnd = -1;
            float negative = Math.Max(.05f, threshold - .2f);
            for (int i = 0; i < probabilities.Length; ++i)
            {
                int position = i * frameSamples;
                if (probabilities[i] >= threshold)
                {
                    pendingEnd = -1;
                    if (start < 0) start = position;
                }
                else if (start >= 0 && probabilities[i] <= negative)
                {
                    if (pendingEnd < 0) pendingEnd = position;
                    if (position - pendingEnd >= minSilence)
                    {
                        if (pendingEnd - start >= minSpeech) intervals.Add(new[]{Math.Max(0, start - pad), Math.Min(samples, pendingEnd + pad)});
                        start = -1; pendingEnd = -1;
                    }
                }
            }
            if (start >= 0 && samples - start >= minSpeech) intervals.Add(new[]{Math.Max(0, start - pad), samples});
            return intervals.Select(value => new MNNSpeechSegment(value[0], value[1])).ToArray();
        }

        private void ResetCaches() { for (int i = 0; i < _caches.Length; ++i) Array.Clear(_caches[i], 0, _caches[i].Length); }
        private static void Validate(float[] samples, CancellationToken token)
        {
            for (int i = 0; i < samples.Length; ++i)
            {
                if ((i & 16383) == 0) token.ThrowIfCancellationRequested();
                if (float.IsNaN(samples[i]) || float.IsInfinity(samples[i]) || Math.Abs(samples[i]) > 1) throw new ArgumentException("Audio must contain finite normalized PCM in [-1,1].", nameof(samples));
            }
        }
        private void ThrowIfDisposed() { if (_disposed) throw new ObjectDisposedException(nameof(MNNFsmnVad)); }
        public void Dispose() { lock (_gate) { if (_disposed) return; _disposed = true; _graph.Dispose(); } }
    }
}
