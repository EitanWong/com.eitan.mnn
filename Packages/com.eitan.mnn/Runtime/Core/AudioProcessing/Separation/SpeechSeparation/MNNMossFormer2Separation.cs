using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    /// <summary>Two-speaker MossFormer2 separation with managed chunking and overlap-add.</summary>
    public sealed class MNNMossFormer2Separation : IDisposable
    {
        public const int SampleRate = 16000;
        private const int ChunkSamples = 64000;
        private const int HopSamples = ChunkSamples / 2;
        private readonly object _gate = new object();
        private readonly MNNGenerationGraph _graph;
        private bool _disposed;

        private MNNMossFormer2Separation(string directory, int threads, MNNBackendType backend)
        {
            MNNMossFormer2Model.Verify(directory);
            var config = MNNMossFormer2Config.Read(directory);
            if (config.sample_rate != SampleRate || config.speakers != 2 || config.input != "inputs" || config.outputs == null || config.outputs.Length != 2)
                throw new InvalidDataException("Unsupported MossFormer2 separation graph contract.");
            _graph = new MNNGenerationGraph(Path.Combine(directory, MNNMossFormer2Model.Graph), threads, backendType: backend,
                precisionMode: MNNPrecisionMode.High);
        }

        public static MNNMossFormer2Separation Load(string directory, int threadCount = 2, MNNBackendType backendType = MNNBackendType.Auto)
        {
            MNNPlatformSupport.RequireSupported();
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentNullException(nameof(directory));
            if (threadCount < 1 || threadCount > 64) throw new ArgumentOutOfRangeException(nameof(threadCount));
            if (!Enum.IsDefined(typeof(MNNBackendType), backendType)) throw new ArgumentOutOfRangeException(nameof(backendType));
            return new MNNMossFormer2Separation(Path.GetFullPath(directory), threadCount, backendType);
        }

        public MNNBackendType Backend => _graph.Backend;

        public MNNSeparatedAudio Separate(float[] samples, int sampleRate = SampleRate, CancellationToken cancellationToken = default)
        {
            if (samples == null || samples.Length == 0) throw new ArgumentException("Supply nonempty mono PCM.", nameof(samples));
            if (sampleRate < 8000 || sampleRate > 96000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (samples.Length > (long)sampleRate * 600) throw new ArgumentException("Supply at most ten minutes of mono PCM.", nameof(samples));
            lock (_gate)
            {
                ThrowIfDisposed();
                Validate(samples, cancellationToken);
                var pcm = MNNSpeechDsp.Resample(samples, sampleRate, SampleRate, cancellationToken);
                var separated = pcm.Length <= ChunkSamples ? SeparateSingle(pcm, cancellationToken) : SeparateLong(pcm, cancellationToken);
                MatchInputRms(separated.Speaker0, pcm);
                MatchInputRms(separated.Speaker1, pcm);
                if (sampleRate == SampleRate)
                    return new MNNSeparatedAudio(separated.Speaker0, separated.Speaker1, SampleRate);
                var left = MNNSpeechDsp.Resample(separated.Speaker0, SampleRate, sampleRate, cancellationToken);
                var right = MNNSpeechDsp.Resample(separated.Speaker1, SampleRate, sampleRate, cancellationToken);
                if (left.Length != samples.Length) Array.Resize(ref left, samples.Length);
                if (right.Length != samples.Length) Array.Resize(ref right, samples.Length);
                return new MNNSeparatedAudio(left, right, sampleRate);
            }
        }

        public MNNSeparatedAudio SeparateWaveFile(string path, CancellationToken cancellationToken = default)
        {
            var samples = MNNWaveAudio.Read(path, out int sampleRate);
            return Separate(samples, sampleRate, cancellationToken);
        }

        public Task<MNNSeparatedAudio> SeparateAsync(float[] samples, int sampleRate = SampleRate, CancellationToken cancellationToken = default)
        {
            var copy = samples == null ? null : (float[])samples.Clone();
            return Task.Run(() => Separate(copy, sampleRate, cancellationToken), cancellationToken);
        }

        private MNNSeparatedAudio SeparateSingle(float[] pcm, CancellationToken token)
        {
            // The exported encoder/decoder strides by eight and does not restore
            // incomplete trailing frames. Pad before inference, then crop.
            int length = Math.Max(16, checked((pcm.Length + 7) / 8 * 8));
            var input = new float[length];
            Array.Copy(pcm, input, pcm.Length);
            var outputs = RunChunk(input, token);
            if (outputs.Speaker0.Length != length || outputs.Speaker1.Length != length)
                throw new InvalidDataException("MossFormer2 returned an unexpected waveform length.");
            if (length != pcm.Length)
            {
                Array.Resize(ref outputs.Speaker0, pcm.Length);
                Array.Resize(ref outputs.Speaker1, pcm.Length);
            }
            return new MNNSeparatedAudio(outputs.Speaker0, outputs.Speaker1, SampleRate);
        }

        private static void MatchInputRms(float[] output, float[] input)
        {
            double inputEnergy = 0, outputEnergy = 0;
            for (int i = 0; i < input.Length; ++i)
            {
                inputEnergy += (double)input[i] * input[i];
                outputEnergy += (double)output[i] * output[i];
            }
            double gain = Math.Sqrt(inputEnergy / input.Length) / Math.Max(1e-12, Math.Sqrt(outputEnergy / output.Length));
            for (int i = 0; i < output.Length; ++i) output[i] = (float)(output[i] * gain);
        }

        private MNNSeparatedAudio SeparateLong(float[] pcm, CancellationToken token)
        {
            var left = new float[pcm.Length];
            var right = new float[pcm.Length];
            var weights = new float[pcm.Length];
            bool first = true;
            for (int start = 0; start < pcm.Length; start += HopSamples)
            {
                token.ThrowIfCancellationRequested();
                int count = Math.Min(ChunkSamples, pcm.Length - start);
                var input = new float[ChunkSamples];
                Array.Copy(pcm, start, input, 0, count);
                var chunk = RunChunk(input, token);
                if (chunk.Speaker0.Length != ChunkSamples || chunk.Speaker1.Length != ChunkSamples)
                    throw new InvalidDataException("MossFormer2 returned an unexpected chunk length.");
                if (!first)
                    AlignChannels(chunk, left, right, weights, start, Math.Min(start + HopSamples, pcm.Length));
                first = false;
                int end = Math.Min(start + count, pcm.Length);
                for (int i = start; i < end; ++i)
                {
                    float weight = (float)Math.Pow(Math.Sin(Math.PI * ((i - start) + .5) / ChunkSamples), 2);
                    left[i] += chunk.Speaker0[i - start] * weight;
                    right[i] += chunk.Speaker1[i - start] * weight;
                    weights[i] += weight;
                }
                if (start + ChunkSamples >= pcm.Length)
                    break;
            }
            for (int i = 0; i < pcm.Length; ++i)
            {
                float weight = weights[i];
                left[i] /= weight;
                right[i] /= weight;
            }
            return new MNNSeparatedAudio(left, right, SampleRate);
        }

        private MNNMossFormer2Chunk RunChunk(float[] input, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var outputs = _graph.RunOutputs(new[]{"spk0", "spk1"}, new MNNGenerationGraph.Input("inputs", input, 1, input.Length));
            return new MNNMossFormer2Chunk(outputs[0].Values, outputs[1].Values);
        }

        private static void AlignChannels(MNNMossFormer2Chunk chunk, float[] left, float[] right, float[] weights, int start, int end)
        {
            double same = 0, swapped = 0;
            for (int i = start; i < end; ++i)
            {
                if (weights[i] <= 1e-6f) continue;
                float previousLeft = left[i] / weights[i];
                float previousRight = right[i] / weights[i];
                float currentLeft = chunk.Speaker0[i - start];
                float currentRight = chunk.Speaker1[i - start];
                same += previousLeft * currentLeft + previousRight * currentRight;
                swapped += previousLeft * currentRight + previousRight * currentLeft;
            }
            if (swapped > same)
            {
                var values = chunk.Speaker0;
                chunk.Speaker0 = chunk.Speaker1;
                chunk.Speaker1 = values;
            }
        }

        private static void Validate(float[] samples, CancellationToken token)
        {
            for (int i = 0; i < samples.Length; ++i)
            {
                if ((i & 16383) == 0) token.ThrowIfCancellationRequested();
                if (float.IsNaN(samples[i]) || float.IsInfinity(samples[i]) || Math.Abs(samples[i]) > 1)
                    throw new ArgumentException("Audio must contain finite normalized PCM in [-1,1].", nameof(samples));
            }
        }

        private void ThrowIfDisposed() { if (_disposed) throw new ObjectDisposedException(nameof(MNNMossFormer2Separation)); }
        public void Dispose() { lock (_gate) { if (_disposed) return; _disposed = true; _graph.Dispose(); } }

        private sealed class MNNMossFormer2Chunk
        {
            internal float[] Speaker0, Speaker1;
            internal MNNMossFormer2Chunk(float[] speaker0, float[] speaker1) { Speaker0 = speaker0; Speaker1 = speaker1; }
        }
    }
}
