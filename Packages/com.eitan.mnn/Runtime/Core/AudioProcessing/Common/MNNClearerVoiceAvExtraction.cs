using System;
using System.IO;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>Shared noncausal ClearerVoice audio-visual extraction contract.</summary>
    internal sealed class MNNClearerVoiceAvExtraction : IDisposable
    {
        public const int SampleRate = 16000;
        public const int FaceSize = 112;
        public const int FramesPerSecond = 25;
        private readonly object _gate = new object();
        private readonly MNNGenerationGraph _visual, _audio;
        private readonly string _owner;
        private bool _disposed;
        public MNNBackendType Backend => _visual.Backend;
        public MNNBackendType AudioBackend => _audio.Backend;

        internal MNNClearerVoiceAvExtraction(string directory, string visualGraph, string audioGraph, int threads,
            MNNBackendType visualBackend, MNNBackendType audioBackend, string owner, MNNGpuTuningMode gpuTuningMode = MNNGpuTuningMode.None)
        {
            _owner = owner;
            _visual = new MNNGenerationGraph(Path.Combine(directory, visualGraph), threads,
                backendType: visualBackend, precisionMode: MNNPrecisionMode.High, gpuTuningMode: gpuTuningMode,
                recreateGpuSessionOnShapeChange: true);
            try
            {
                _audio = new MNNGenerationGraph(Path.Combine(directory, audioGraph), threads,
                    backendType: audioBackend, precisionMode: MNNPrecisionMode.High, gpuTuningMode: gpuTuningMode,
                    recreateGpuSessionOnShapeChange: true);
            }
            catch { _visual.Dispose(); throw; }
        }

        /// <summary>Extract the visible target. Supply frame-major normalized grayscale 112x112 crops at 25 FPS,
        /// aligned from the beginning of the mono recording. Supports 40 ms–6 s at 8–96 kHz.</summary>
        public float[] Extract(float[] samples, float[] visualFrames, int videoFrameCount, int sampleRate = SampleRate, CancellationToken cancellationToken = default)
        {
            Validate(samples, visualFrames, videoFrameCount, sampleRate, cancellationToken);
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(_owner);
                var pcm = MNNSpeechDsp.Resample(samples, sampleRate, SampleRate, cancellationToken);
                if (pcm.Length < 640 || pcm.Length > 96000) throw new ArgumentException("Supply 640–96000 resampled samples.", nameof(samples));
                if (videoFrameCount != (long)samples.Length * FramesPerSecond / sampleRate) throw new ArgumentException("Supply floor(duration*25) synchronized target frames.", nameof(videoFrameCount));
                var features = _visual.RunOutputs(new[] { "visual_features" },
                    new MNNGenerationGraph.Input("frames", visualFrames, 1, videoFrameCount, FaceSize, FaceSize))[0];
                if (features.Shape.Length != 3 || features.Shape[0] != 1 || features.Shape[1] != 256 || features.Shape[2] != videoFrameCount)
                    throw new InvalidDataException("Unexpected ClearerVoice visual feature shape.");
                cancellationToken.ThrowIfCancellationRequested();
                var output = _audio.RunOutputs(new[] { "target" }, new MNNGenerationGraph.Input("mixture", pcm, 1, pcm.Length),
                    new MNNGenerationGraph.Input("visual_features", features.Values, features.Shape))[0];
                if (output.Shape.Length != 2 || output.Shape[0] != 1 || output.Shape[1] != pcm.Length)
                    throw new InvalidDataException("Unexpected ClearerVoice target waveform shape.");
                cancellationToken.ThrowIfCancellationRequested();
                if (sampleRate == SampleRate) return output.Values;
                var restored = MNNSpeechDsp.Resample(output.Values, SampleRate, sampleRate, cancellationToken);
                if (restored.Length != samples.Length) Array.Resize(ref restored, samples.Length);
                return restored;
            }
        }

        public float[] ExtractWaveFile(string path, float[] visualFrames, int videoFrameCount, out int sampleRate, CancellationToken cancellationToken = default)
        {
            var samples = MNNWaveAudio.Read(path, out sampleRate);
            return Extract(samples, visualFrames, videoFrameCount, sampleRate, cancellationToken);
        }

        private static void Validate(float[] samples, float[] frames, int count, int rate, CancellationToken token)
        {
            if (samples == null || samples.Length == 0) throw new ArgumentException("Supply nonempty mono PCM.", nameof(samples));
            if (frames == null) throw new ArgumentNullException(nameof(frames));
            if (count < 1 || count > 150 || frames.Length != count * FaceSize * FaceSize)
                throw new ArgumentException("Supply 1–150 frame-major grayscale 112x112 target crops.", nameof(frames));
            if (rate < 8000 || rate > 96000) throw new ArgumentOutOfRangeException(nameof(rate));
            if (samples.Length > (long)rate * 6) throw new ArgumentException("Supply at most six seconds of mono PCM.", nameof(samples));
            Finite(samples, token); Finite(frames, token);
        }

        private static void Finite(float[] values, CancellationToken token)
        {
            for (int i = 0; i < values.Length; ++i)
            {
                if ((i & 16383) == 0) token.ThrowIfCancellationRequested();
                if (float.IsNaN(values[i]) || float.IsInfinity(values[i])) throw new ArgumentException("Supply finite audio and normalized visual values.");
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                _visual.Dispose(); _audio.Dispose();
            }
        }
    }
}
