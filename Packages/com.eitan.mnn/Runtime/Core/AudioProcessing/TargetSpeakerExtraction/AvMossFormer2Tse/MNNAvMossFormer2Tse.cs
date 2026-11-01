using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    /// <summary>Audio-visual target-speaker extraction using lip-conditioned MossFormer2.</summary>
    public sealed class MNNAvMossFormer2Tse : IDisposable
    {
        private const int HopSamples = 28800;
        private readonly object _gate = new object();
        private readonly MNNGenerationGraph _graph;
        private bool _disposed;

        private MNNAvMossFormer2Tse(string directory, int threads, MNNBackendType backend)
        {
            MNNAvMossFormer2TseModel.Verify(directory);
            var config = MNNAvMossFormer2TseConfig.Read(directory);
            var resolved = MNNAcceleration.Resolve(MNNAcceleration.ForDynamicGraph(backend));
            _graph = new MNNGenerationGraph(Path.Combine(directory, resolved == MNNBackendType.CPU ? config.graph_fp32 : config.graph),
                threads, backendType: resolved, precisionMode: MNNPrecisionMode.High);
        }

        public static MNNAvMossFormer2Tse Load(string directory, int threadCount = 4, MNNBackendType backendType = MNNBackendType.Auto)
        {
            MNNPlatformSupport.RequireSupported();
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentNullException(nameof(directory));
            if (threadCount < 1 || threadCount > 64) throw new ArgumentOutOfRangeException(nameof(threadCount));
            return new MNNAvMossFormer2Tse(Path.GetFullPath(directory), threadCount, backendType);
        }

        public MNNBackendType Backend => _graph.Backend;

        /// <summary>
        /// Extracts the visible speaker. Visual frames are grayscale, normalized to the
        /// ClearerVoice range, and flattened as frame-major [frame,112,112] samples.
        /// </summary>
        public float[] Extract(float[] audio, float[] visualFrames, int videoFrameCount, int sampleRate = 16000, CancellationToken cancellationToken = default)
        {
            if (audio == null || audio.Length == 0) throw new ArgumentException("Supply nonempty mono PCM.", nameof(audio));
            if (visualFrames == null) throw new ArgumentNullException(nameof(visualFrames));
            if (videoFrameCount < 1 || visualFrames.Length != checked(videoFrameCount * MNNAvMossFormer2TseModel.FaceSize * MNNAvMossFormer2TseModel.FaceSize))
                throw new ArgumentException("Visual frames must be flattened grayscale 112x112 images.", nameof(visualFrames));
            if (sampleRate < 8000 || sampleRate > 96000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (audio.Length > (long)sampleRate * 600) throw new ArgumentException("Supply at most ten minutes of mono PCM.", nameof(audio));
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(MNNAvMossFormer2Tse));
                ValidateAudio(audio, cancellationToken);
                ValidateFinite(visualFrames, cancellationToken);
                var pcm = MNNSpeechDsp.Resample(audio, sampleRate, MNNAvMossFormer2TseModel.SampleRate, cancellationToken);
                var result = ExtractAt16K(pcm, visualFrames, cancellationToken);
                if (sampleRate == MNNAvMossFormer2TseModel.SampleRate) return result;
                var resampled = MNNSpeechDsp.Resample(result, MNNAvMossFormer2TseModel.SampleRate, sampleRate, cancellationToken);
                if (resampled.Length != audio.Length) Array.Resize(ref resampled, audio.Length);
                return resampled;
            }
        }

        public Task<float[]> ExtractAsync(float[] audio, float[] visualFrames, int videoFrameCount, int sampleRate = 16000, CancellationToken cancellationToken = default)
        {
            var audioCopy = audio == null ? null : (float[])audio.Clone();
            var visualCopy = visualFrames == null ? null : (float[])visualFrames.Clone();
            return Task.Run(() => Extract(audioCopy, visualCopy, videoFrameCount, sampleRate, cancellationToken), cancellationToken);
        }

        private float[] ExtractAt16K(float[] pcm, float[] visual, CancellationToken token)
        {
            var output = new float[pcm.Length];
            var weights = new float[pcm.Length];
            for (int start = 0; start < pcm.Length; start += HopSamples)
            {
                token.ThrowIfCancellationRequested();
                var audioChunk = new float[MNNAvMossFormer2TseModel.ChunkSamples];
                int count = Math.Min(audioChunk.Length, pcm.Length - start);
                Array.Copy(pcm, start, audioChunk, 0, count);
                var visualChunk = new float[MNNAvMossFormer2TseModel.ChunkFrames * MNNAvMossFormer2TseModel.FaceSize * MNNAvMossFormer2TseModel.FaceSize];
                int frameStart = start * MNNAvMossFormer2TseModel.FramesPerSecond / MNNAvMossFormer2TseModel.SampleRate;
                CopyVisual(visual, frameStart, visualChunk);
                var values = _graph.Run("target_frames", new MNNGenerationGraph.Input("mixture", audioChunk, 1, audioChunk.Length),
                    new MNNGenerationGraph.Input("visual", visualChunk, 1, MNNAvMossFormer2TseModel.ChunkFrames, MNNAvMossFormer2TseModel.FaceSize, MNNAvMossFormer2TseModel.FaceSize));
                var chunkOutput = OverlapAddDecoderFrames(values);
                int end = Math.Min(start + count, pcm.Length);
                for (int i = start; i < end; ++i)
                {
                    double phase = ((i - start) + .5) / MNNAvMossFormer2TseModel.ChunkSamples;
                    float weight = (float)Math.Pow(Math.Sin(Math.PI * phase), 2);
                    output[i] += chunkOutput[i - start] * weight;
                    weights[i] += weight;
                }
                if (start + MNNAvMossFormer2TseModel.ChunkSamples >= pcm.Length) break;
            }
            for (int i = 0; i < output.Length; ++i) output[i] /= weights[i];
            return output;
        }

        private static float[] OverlapAddDecoderFrames(float[] values)
        {
            int expected = checked(MNNAvMossFormer2TseModel.DecoderFrameCount * MNNAvMossFormer2TseModel.DecoderFrameLength);
            if (values.Length != expected)
                throw new InvalidDataException("AV MossFormer2 returned an unexpected decoder-frame tensor.");
            var output = new float[MNNAvMossFormer2TseModel.ChunkSamples];
            for (int frame = 0; frame < MNNAvMossFormer2TseModel.DecoderFrameCount; ++frame)
            {
                int outputStart = frame * MNNAvMossFormer2TseModel.DecoderFrameStep;
                int inputStart = frame * MNNAvMossFormer2TseModel.DecoderFrameLength;
                for (int sample = 0; sample < MNNAvMossFormer2TseModel.DecoderFrameLength; ++sample)
                    output[outputStart + sample] += values[inputStart + sample];
            }
            return output;
        }

        private static void CopyVisual(float[] source, int startFrame, float[] destination)
        {
            int pixels = MNNAvMossFormer2TseModel.FaceSize * MNNAvMossFormer2TseModel.FaceSize;
            for (int frame = 0; frame < MNNAvMossFormer2TseModel.ChunkFrames; ++frame)
            {
                int sourceFrame = Math.Min(source.Length / pixels - 1, Math.Max(0, startFrame + frame));
                Array.Copy(source, sourceFrame * pixels, destination, frame * pixels, pixels);
            }
        }

        private static void ValidateAudio(float[] values, CancellationToken token)
        {
            for (int i = 0; i < values.Length; ++i)
            {
                if ((i & 16383) == 0) token.ThrowIfCancellationRequested();
                if (float.IsNaN(values[i]) || float.IsInfinity(values[i]) || Math.Abs(values[i]) > 1)
                    throw new ArgumentException("Audio must contain finite normalized PCM in [-1,1].", nameof(values));
            }
        }

        private static void ValidateFinite(float[] values, CancellationToken token)
        {
            for (int i = 0; i < values.Length; ++i)
            {
                if ((i & 16383) == 0) token.ThrowIfCancellationRequested();
                if (float.IsNaN(values[i]) || float.IsInfinity(values[i]))
                    throw new ArgumentException("Visual values must be finite.", nameof(values));
            }
        }

        public void Dispose() { lock (_gate) { if (_disposed) return; _disposed = true; _graph.Dispose(); } }
    }
}
