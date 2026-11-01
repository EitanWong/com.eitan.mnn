using System;
using System.IO;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>Noncausal LRS2 AV-ConvTasNet extraction with synchronized target-face crops.</summary>
    public sealed class MNNAvConvTasNetExtraction : IDisposable
    {
        public const int SampleRate = MNNClearerVoiceAvExtraction.SampleRate;
        public const int FaceSize = MNNClearerVoiceAvExtraction.FaceSize;
        public const int FramesPerSecond = MNNClearerVoiceAvExtraction.FramesPerSecond;
        private readonly MNNClearerVoiceAvExtraction _pipeline;
        public MNNBackendType Backend => _pipeline.Backend;
        public MNNBackendType AudioBackend => _pipeline.AudioBackend;

        private MNNAvConvTasNetExtraction(string directory, int threads, MNNBackendType backend)
        {
            if (!MNNAvConvTasNetModel.IsComplete(directory)) throw new InvalidDataException("AvConvTasNet profile or graph verification failed.");
            if (backend == MNNBackendType.Auto)
                backend = MNNAcceleration.IsBackendAvailable(MNNBackendType.Metal) ? MNNBackendType.Metal : MNNBackendType.CPU;
            _pipeline = new MNNClearerVoiceAvExtraction(directory, MNNAvConvTasNetModel.VisualGraph, MNNAvConvTasNetModel.AudioGraph,
                threads, backend, backend, nameof(MNNAvConvTasNetExtraction), MNNGpuTuningMode.None);
        }

        public static MNNAvConvTasNetExtraction Load(string directory, int threadCount = 4, MNNBackendType backendType = MNNBackendType.Auto)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentNullException(nameof(directory));
            if (threadCount < 1 || threadCount > 64) throw new ArgumentOutOfRangeException(nameof(threadCount));
            MNNAcceleration.Validate(backendType);
            if (backendType != MNNBackendType.Auto && backendType != MNNBackendType.CPU && backendType != MNNBackendType.Metal)
                throw new NotSupportedException("AvConvTasNet supports validated CPU and Metal profiles only.");
            MNNPlatformSupport.RequireSupported();
            return new MNNAvConvTasNetExtraction(Path.GetFullPath(directory), threadCount, backendType);
        }

        /// <summary>Extract the visible target from mono PCM and normalized grayscale 112x112 target crops at 25 FPS.
        /// Frames start at the beginning of the recording. Accepts 40 ms–6 s at 8–96 kHz and restores the input sample count.</summary>
        public float[] Extract(float[] samples, float[] visualFrames, int videoFrameCount, int sampleRate = SampleRate, CancellationToken cancellationToken = default) =>
            _pipeline.Extract(samples, visualFrames, videoFrameCount, sampleRate, cancellationToken);

        public float[] ExtractWaveFile(string path, float[] visualFrames, int videoFrameCount, out int sampleRate, CancellationToken cancellationToken = default) =>
            _pipeline.ExtractWaveFile(path, visualFrames, videoFrameCount, out sampleRate, cancellationToken);

        public void Dispose() => _pipeline.Dispose();
    }
}
