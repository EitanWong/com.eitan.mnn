using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MNN.Unity
{
    /// <summary>Original-checkpoint ERes2NetV2, CAM++, ECAPA and TitaNet unit speaker vectors.</summary>
    public sealed class MNNSpeakerEmbedding : IDisposable
    {
        private readonly MNNGenerationGraph _graph;
        private readonly bool _official;
        private readonly MNNSpeechBrainFeatures _speechBrain;
        private readonly MNNTitaNetFeatures _titaNet;
        private readonly object _gate = new object();
        private bool _disposed;
        public string ModelName { get; }
        private MNNSpeakerEmbedding(string directory, int threads, MNNBackendType backend)
        {
            MNNSpeakerModel.Verify(directory);
            var config = JsonUtility.FromJson<MNNSpeakerModelConfig>(File.ReadAllText(Path.Combine(directory, MNNSpeakerModelConfig.FileName)));
            var profile = MNNSpeakerModel.FindSourceProfile(config);
            _official = profile != null;
            if (profile?.Frontend == MNNSpeechBrainFeatures.Frontend) _speechBrain = new MNNSpeechBrainFeatures(config);
            if (profile?.Frontend == MNNTitaNetFeatures.Frontend) _titaNet = new MNNTitaNetFeatures(config);
            ModelName = profile?.Name ?? "ERes2NetV2 (legacy)";
            if (backend == MNNBackendType.Auto)
                backend = profile != null && profile.PreferMetal && MNNAcceleration.IsBackendAvailable(MNNBackendType.Metal) ? MNNBackendType.Metal : MNNBackendType.CPU;
            string graph = backend != MNNBackendType.CPU && backend != MNNBackendType.Auto && !string.IsNullOrEmpty(config.graph_fp16) && File.Exists(Path.Combine(directory, config.graph_fp16)) ? config.graph_fp16 : config.graph;
            _graph = new MNNGenerationGraph(Path.Combine(directory, graph), threads, backendType: backend, precisionMode: MNNPrecisionMode.High,
                gpuTuningMode: profile?.GpuTuning ?? MNNGpuTuningMode.Fast, recreateGpuSessionOnShapeChange: profile?.RecreateGpuSessionOnShapeChange ?? false);
        }
        public static MNNSpeakerEmbedding Load(string directory, int threadCount = 4, MNNBackendType backendType = MNNBackendType.Auto)
        {
            MNNPlatformSupport.RequireSupported();
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentNullException(nameof(directory));
            if (threadCount < 1 || threadCount > 64) throw new ArgumentOutOfRangeException(nameof(threadCount));
            MNNAcceleration.Validate(backendType);
            if (backendType == MNNBackendType.NN) throw new NotSupportedException("Speaker embedding NNAPI graph/device compatibility is unverified.");
            return new MNNSpeakerEmbedding(Path.GetFullPath(directory), threadCount, backendType);
        }
        public float[] Encode(float[] samples, int sampleRate = 16000, CancellationToken cancellationToken = default)
        {
            if (samples == null || samples.Length == 0) throw new ArgumentException("Supply nonempty mono PCM.", nameof(samples));
            if (sampleRate < 8000 || sampleRate > 96000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (samples.Length > (long)sampleRate * 30) throw new ArgumentException("Supply at most thirty seconds of mono PCM.", nameof(samples));
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(MNNSpeakerEmbedding));
                cancellationToken.ThrowIfCancellationRequested();
                if (samples.Any(value => float.IsNaN(value) || float.IsInfinity(value) || Math.Abs(value) > 1))
                    throw new ArgumentException("Supply finite normalized PCM in [-1,1].", nameof(samples));
                float[] pcm = MNNSpeechDsp.Resample(samples, sampleRate, 16000, cancellationToken);
                float[] output = _titaNet != null ? RunTitaNet(pcm, cancellationToken) : RunFrameFeatures(pcm, cancellationToken);
                if (output.Length != 192 || output.Any(value => float.IsNaN(value) || float.IsInfinity(value))) throw new InvalidDataException("Speaker graph returned an invalid embedding.");
                float norm = (float)Math.Sqrt(output.Sum(value => value * value));
                if (norm <= 1e-12f) throw new InvalidDataException("Speaker graph returned a zero embedding.");
                for (int i = 0; i < output.Length; ++i) output[i] /= norm;
                cancellationToken.ThrowIfCancellationRequested();
                return output;
            }
        }
        private float[] RunFrameFeatures(float[] pcm, CancellationToken cancellationToken)
        {
            int frames;
            float[] features = _speechBrain != null ? _speechBrain.Extract(pcm, out frames, cancellationToken)
                    : _official ? MNNKaldiFeatures.Extract3dSpeaker(pcm, out frames, cancellationToken)
                    : MNNKaldiFeatures.ExtractSpeaker(pcm, out frames, cancellationToken);
            return _graph.RunOutputs(new[] {"embedding"}, new MNNGenerationGraph.Input("x", features, 1, frames, 80))[0].Values;
        }
        private float[] RunTitaNet(float[] pcm, CancellationToken token)
        {
            float[] features = _titaNet.Extract(pcm, out int frames, out int valid, token);
            return _graph.RunOutputs(new[] { "embedding" }, new MNNGenerationGraph.Input("audio_signal", features, 1, 80, frames),
                new MNNGenerationGraph.Input("length", new[] { valid }, 1))[0].Values;
        }
        public MNNBackendType Backend => _graph.Backend;
        public float[] EncodeWaveFile(string path, CancellationToken cancellationToken = default)
        {
            var pcm = MNNWaveAudio.Read(path, out int rate);
            return Encode(pcm, rate, cancellationToken);
        }
        public static float Similarity(float[] left, float[] right) => MNNVectorSimilarity.Cosine(left, right);
        public Task<float[]> EncodeAsync(float[] samples, int sampleRate = 16000, CancellationToken cancellationToken = default)
        {
            var copy = samples == null ? null : (float[])samples.Clone();
            return Task.Run(() => Encode(copy, sampleRate, cancellationToken), cancellationToken);
        }
        public void Dispose() { lock (_gate) { if (_disposed) return; _disposed = true; _graph.Dispose(); } }
    }
}
