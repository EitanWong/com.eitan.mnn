using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    /// <summary>MarbleNet multilingual frame VAD with managed log-mel extraction.</summary>
    public sealed class MNNMarbleNetVad : IDisposable
    {
        private readonly object _gate = new object();
        private readonly MNNGenerationGraph _graph;
        private readonly MNNMarbleNetVadConfig _config;
        private bool _disposed;

        private MNNMarbleNetVad(string directory, int threads, MNNBackendType backend)
        {
            _config = MNNMarbleNetVadConfig.Read(directory);
            if (!MNNMarbleNetVadModel.IsRepository(_config.repository) || _config.sample_rate != 16000 || _config.feature_bins != 80 ||
                _config.frame_shift_samples != 160 || _config.output_frame_samples != 320 || _config.speech_class != 1)
                throw new InvalidDataException("Unsupported MarbleNet VAD profile.");
            MNNMarbleNetVadModel.Verify(directory);
            _graph = new MNNGenerationGraph(Path.Combine(directory, MNNMarbleNetVadModel.Graph), threads, backendType: backend == MNNBackendType.Auto ? MNNBackendType.CPU : backend,
                precisionMode: MNNPrecisionMode.High);
        }

        public static MNNMarbleNetVad Load(string directory, int threadCount = 1, MNNBackendType backendType = MNNBackendType.Auto)
        {
            MNNPlatformSupport.RequireSupported();
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentNullException(nameof(directory));
            if (threadCount < 1 || threadCount > 64) throw new ArgumentOutOfRangeException(nameof(threadCount));
            if (!Enum.IsDefined(typeof(MNNBackendType), backendType)) throw new ArgumentOutOfRangeException(nameof(backendType));
            return new MNNMarbleNetVad(Path.GetFullPath(directory), threadCount, backendType);
        }

        public MNNBackendType Backend => _graph.Backend;

        public MNNVoiceActivityResult Detect(float[] samples, int sampleRate, float threshold = .5f, CancellationToken cancellationToken = default)
        {
            if (samples == null || samples.Length == 0 || samples.Length > (long)sampleRate * 600)
                throw new ArgumentException("Supply nonempty mono audio up to ten minutes.", nameof(samples));
            if (sampleRate < 8000 || sampleRate > 96000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (float.IsNaN(threshold) || threshold < .01f || threshold > .99f) throw new ArgumentOutOfRangeException(nameof(threshold));
            lock (_gate)
            {
                ThrowIfDisposed();
                Validate(samples, cancellationToken);
                var pcm = MNNSpeechDsp.Resample(samples, sampleRate, _config.sample_rate, cancellationToken);
                int frames;
                var features = MNNMarbleNetFeatures.Extract(pcm, out frames, cancellationToken);
                var output = _graph.RunOutputs(new[] {_config.output}, new MNNGenerationGraph.Input(_config.input, features, 1, _config.feature_bins, frames))[0];
                var values = output.Values;
                int classCount = output.Shape.Length == 0 ? 0 : output.Shape[output.Shape.Length - 1];
                if (classCount < 2 || values.Length % classCount != 0)
                    throw new InvalidDataException("MarbleNet VAD returned an invalid output shape.");
                int outputFrames = values.Length / classCount;
                var probabilities = new float[outputFrames];
                for (int i = 0; i < outputFrames; ++i)
                {
                    int offset = i * classCount;
                    float max = values[offset];
                    for (int c = 1; c < classCount; ++c) max = Math.Max(max, values[offset + c]);
                    double total = 0, speech = 0;
                    for (int c = 0; c < classCount; ++c)
                    {
                        double value = Math.Exp(Math.Max(-80, values[offset + c] - max));
                        total += value;
                        if (c == _config.speech_class) speech = value;
                    }
                    probabilities[i] = (float)Math.Max(0, Math.Min(1, speech / Math.Max(1e-12, total)));
                }
                return new MNNVoiceActivityResult(Segment(probabilities, pcm.Length, _config.output_frame_samples, threshold), probabilities,
                    (double)samples.Length / sampleRate);
            }
        }

        public MNNVoiceActivityResult DetectWaveFile(string path, float threshold = .5f, CancellationToken cancellationToken = default)
        {
            var samples = MNNWaveAudio.Read(path, out int rate);
            return Detect(samples, rate, threshold, cancellationToken);
        }

        public Task<MNNVoiceActivityResult> DetectAsync(float[] samples, int sampleRate, float threshold = .5f, CancellationToken cancellationToken = default)
        {
            var copy = samples == null ? null : (float[])samples.Clone();
            return Task.Run(() => Detect(copy, sampleRate, threshold, cancellationToken), cancellationToken);
        }

        internal static MNNSpeechSegment[] Segment(float[] probabilities, int samples, int frameSamples, float threshold)
        {
            const int minSpeech = 2400, minSilence = 1600, pad = 800;
            var intervals = new List<int[]>();
            int start = -1, pendingEnd = -1;
            float negative = Math.Max(.05f, threshold - .15f);
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
                        if (pendingEnd - start >= minSpeech) intervals.Add(new[] {Math.Max(0, start - pad), Math.Min(samples, pendingEnd + pad)});
                        start = -1;
                        pendingEnd = -1;
                    }
                }
            }
            if (start >= 0 && samples - start >= minSpeech)
                intervals.Add(new[] {Math.Max(0, start - pad), samples});
            return intervals.Select(value => new MNNSpeechSegment(value[0], value[1])).ToArray();
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

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(MNNMarbleNetVad));
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
