using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MNN.Unity
{
    /// <summary>WeSpeaker ResNet34-LM 256-dimensional speaker vectors using official MNN graphs.</summary>
    public sealed class MNNWeSpeakerEmbedding : IDisposable
    {
        private readonly MNNGenerationGraph _graph;
        private readonly object _gate = new object();
        private bool _disposed;
        private MNNWeSpeakerEmbedding(string directory, int threads, MNNBackendType backend)
        {
            MNNWeSpeakerModel.Verify(directory);
            var config = MNNWeSpeakerModelConfig.Read(directory);
            _graph = new MNNGenerationGraph(Path.Combine(directory, config.graph), threads, backendType: backend,
                precisionMode: MNNPrecisionMode.High);
            if (config.embedding_dimension != 256 || config.input != "feats" || config.output != "embs")
                throw new InvalidDataException("Unsupported WeSpeaker graph contract.");
        }
        public static MNNWeSpeakerEmbedding Load(string directory, int threadCount = 4, MNNBackendType backendType = MNNBackendType.Auto)
        {
            MNNPlatformSupport.RequireSupported();
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentNullException(nameof(directory));
            if (threadCount < 1 || threadCount > 64) throw new ArgumentOutOfRangeException(nameof(threadCount));
            return new MNNWeSpeakerEmbedding(Path.GetFullPath(directory), threadCount, backendType);
        }
        public float[] Encode(float[] samples, int sampleRate = 16000, CancellationToken cancellationToken = default)
        {
            if (samples == null || samples.Length == 0) throw new ArgumentException("Supply nonempty mono PCM.", nameof(samples));
            if (sampleRate < 8000 || sampleRate > 96000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (samples.Length > (long)sampleRate * 30) throw new ArgumentException("Supply at most thirty seconds of mono PCM.", nameof(samples));
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(MNNWeSpeakerEmbedding));
                cancellationToken.ThrowIfCancellationRequested();
                if (samples.Any(value => float.IsNaN(value) || float.IsInfinity(value) || Math.Abs(value) > 1))
                    throw new ArgumentException("Supply finite normalized PCM in [-1,1].", nameof(samples));
                float[] pcm = MNNSpeechDsp.Resample(samples, sampleRate, 16000, cancellationToken);
                int frames;
                float[] features = MNNKaldiFeatures.ExtractSpeaker(pcm, out frames, cancellationToken);
                float[] output = _graph.Run("embs", new MNNGenerationGraph.Input("feats", features, 1, frames, 80));
                if (output.Length != 256) throw new InvalidDataException("WeSpeaker graph returned an invalid embedding.");
                float norm = (float)Math.Sqrt(output.Sum(value => value * value));
                if (norm <= 1e-12f) throw new InvalidDataException("WeSpeaker graph returned a zero embedding.");
                for (int i = 0; i < output.Length; ++i) output[i] /= norm;
                return output;
            }
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
