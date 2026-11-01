using System;
using System.IO;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>Noncausal 16 kHz target extraction conditioned on synchronized gesture features.</summary>
    public sealed class MNNSegExtraction : IDisposable
    {
        public const int SampleRate = 16000;
        private readonly object _gate = new object();
        private readonly MNNGenerationGraph _graph;
        private bool _disposed;
        public MNNBackendType Backend => _graph.Backend;

        private MNNSegExtraction(string directory, int threads)
        {
            if (!MNNSegModel.IsComplete(directory)) throw new InvalidDataException("SEG profile or graph verification failed.");
            _graph = new MNNGenerationGraph(Path.Combine(directory, MNNSegModel.Graph), threads,
                backendType: MNNBackendType.CPU, precisionMode: MNNPrecisionMode.High);
        }

        public static MNNSegExtraction Load(string directory, int threadCount = 4, MNNBackendType backendType = MNNBackendType.Auto)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentNullException(nameof(directory));
            if (threadCount < 1 || threadCount > 64) throw new ArgumentOutOfRangeException(nameof(threadCount));
            MNNAcceleration.Validate(backendType);
            if (backendType != MNNBackendType.Auto && backendType != MNNBackendType.CPU)
                throw new NotSupportedException("SEG currently supports its validated CPU profile only.");
            MNNPlatformSupport.RequireSupported();
            return new MNNSegExtraction(Path.GetFullPath(directory), threadCount);
        }

        /// <summary>Extract 1067–160000 samples at 16 kHz. Features must span floor(duration × 15) synchronized frames.</summary>
        public float[] Extract(float[] samples, MNNSegGestureFeatures gestures, int sampleRate = SampleRate, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(MNNSegExtraction));
                var pcm = Prepare(samples, gestures, sampleRate, cancellationToken);
                var result = _graph.RunOutputs(new[] { "target" }, new MNNGenerationGraph.Input("mixture", pcm, 1, pcm.Length),
                    new MNNGenerationGraph.Input("gestures", gestures.ToArray(), 1, gestures.FrameCount, MNNSegGestureFeatures.FeaturesPerFrame))[0];
                if (result.Shape.Length != 2 || result.Shape[0] != 1 || result.Shape[1] != pcm.Length)
                    throw new InvalidDataException("Unexpected SEG waveform shape.");
                cancellationToken.ThrowIfCancellationRequested();
                if (sampleRate == SampleRate) return result.Values;
                var restored = MNNSpeechDsp.Resample(result.Values, SampleRate, sampleRate, cancellationToken);
                if (restored.Length != samples.Length) Array.Resize(ref restored, samples.Length);
                return restored;
            }
        }

        public float[] ExtractWaveFile(string path, MNNSegGestureFeatures gestures, out int sampleRate, CancellationToken cancellationToken = default)
        {
            var samples = MNNWaveAudio.Read(path, out sampleRate);
            return Extract(samples, gestures, sampleRate, cancellationToken);
        }

        private static float[] Prepare(float[] samples, MNNSegGestureFeatures gestures, int rate, CancellationToken token)
        {
            if (samples == null || samples.Length == 0) throw new ArgumentException("Supply nonempty mono PCM.", nameof(samples));
            if (gestures == null) throw new ArgumentNullException(nameof(gestures));
            if (rate < 8000 || rate > 96000) throw new ArgumentOutOfRangeException(nameof(rate));
            if ((long)samples.Length * SampleRate < 1067L * rate || (long)samples.Length * SampleRate > 160000L * rate)
                throw new ArgumentException("Recording duration is outside the validated SEG range.", nameof(samples));
            if ((long)samples.Length * MNNSegGestureFeatures.FramesPerSecond / rate != gestures.FrameCount)
                throw new ArgumentException("Gesture frames must equal floor(audio duration × 15) and start with the recording.", nameof(gestures));
            for (int i = 0; i < samples.Length; ++i)
            {
                if ((i & 16383) == 0) token.ThrowIfCancellationRequested();
                if (float.IsNaN(samples[i]) || float.IsInfinity(samples[i])) throw new ArgumentException("Supply finite mono PCM.", nameof(samples));
            }
            return MNNSpeechDsp.Resample(samples, rate, SampleRate, token);
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                _graph.Dispose();
            }
        }
    }
}
