using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    /// <summary>Silero v5 voice activity detection with per-recording state reset.</summary>
    public sealed class MNNSileroVad : IDisposable
    {
        private readonly object _gate = new object();
        private readonly MNNSileroFrameRunner _runner;
        private readonly float[] _output = new float[257];
        private bool _disposed;
        public MNNBackendType Backend => _runner.Backend;
        private MNNSileroVad(string directory, int threads, MNNBackendType backend)
        {
            _runner = new MNNSileroFrameRunner(directory, threads, backend);
        }
        public static MNNSileroVad Load(string directory, int threadCount = 1, MNNBackendType backendType = MNNBackendType.Auto)
        {
            MNNPlatformSupport.RequireSupported();
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentNullException(nameof(directory));
            if (threadCount < 1 || threadCount > 64) throw new ArgumentOutOfRangeException(nameof(threadCount));
            if (!Enum.IsDefined(typeof(MNNBackendType), backendType)) throw new ArgumentOutOfRangeException(nameof(backendType));
            return new MNNSileroVad(Path.GetFullPath(directory), threadCount, backendType);
        }

        /// <summary>Detect speech in normalized mono PCM at 8–96 kHz, up to ten minutes.
        /// Fixed 250 ms minimum speech, 100 ms silence and 30 ms boundary padding.</summary>
        public MNNVoiceActivityResult Detect(float[] samples, int sampleRate, float threshold = .5f, CancellationToken cancellationToken = default)
        {
            if (sampleRate < 8000 || sampleRate > 96000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (samples == null || samples.Length == 0 || samples.Length > (long)sampleRate * 600)
                throw new ArgumentException("Supply nonempty mono audio up to ten minutes.", nameof(samples));
            if (float.IsNaN(threshold) || threshold < .01f || threshold > 1) throw new ArgumentOutOfRangeException(nameof(threshold));
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(MNNSileroVad));
                cancellationToken.ThrowIfCancellationRequested();
                for (int i = 0; i < samples.Length; ++i)
                {
                    if ((i & 16383) == 0) cancellationToken.ThrowIfCancellationRequested();
                    if (float.IsNaN(samples[i]) || float.IsInfinity(samples[i]) || Math.Abs(samples[i]) > 1)
                        throw new ArgumentException("Audio must contain finite normalized PCM in [-1,1].", nameof(samples));
                }
                var pcm = MNNSpeechDsp.Resample(samples, sampleRate, 16000, cancellationToken);
                var probabilities = new float[(pcm.Length + 511) / 512];
                var packed = new float[832]; // 64 context + 512 audio + 2*128 recurrent state.
                for (int i = 0; i < probabilities.Length; ++i)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Array.Copy(packed, 512, packed, 0, 64);
                    Array.Clear(packed, 64, 512);
                    Array.Copy(pcm, i * 512, packed, 64, Math.Min(512, pcm.Length - i * 512));
                    _runner.Run(packed, _output);
                    var output = _output;
                    if (output.Length != 257 || output[0] < 0 || output[0] > 1)
                        throw new InvalidDataException("Invalid Silero VAD output.");
                    probabilities[i] = output[0];
                    Array.Copy(output, 1, packed, 576, 256);
                }
                cancellationToken.ThrowIfCancellationRequested();
                return new MNNVoiceActivityResult(Segment(probabilities, pcm.Length, threshold), probabilities, (double)samples.Length / sampleRate);
            }
        }

        // Published Silero hysteresis/minimum-duration algorithm with unlimited segment length.
        internal static MNNSpeechSegment[] Segment(float[] probabilities, int samples, float threshold)
        {
            var intervals = new List<int[]>();
            int start = -1, pendingEnd = 0;
            float negative = Math.Max(.01f, threshold - .15f);
            for (int i = 0; i < probabilities.Length; ++i)
            {
                int position = i * 512;
                if (probabilities[i] >= threshold)
                {
                    pendingEnd = 0;
                    if (start < 0) { start = position; continue; }
                }
                if (start >= 0 && probabilities[i] < negative)
                {
                    if (pendingEnd == 0) pendingEnd = position;
                    if (position - pendingEnd < 1600) continue;
                    if (pendingEnd - start > 4000) intervals.Add(new[] {start, pendingEnd});
                    start = -1; pendingEnd = 0;
                }
            }
            if (start >= 0 && samples - start > 4000) intervals.Add(new[] {start, samples});
            for (int i = 0; i < intervals.Count; ++i)
            {
                var current = intervals[i];
                if (i == 0) current[0] = Math.Max(0, current[0] - 480);
                if (i + 1 < intervals.Count)
                {
                    int silence = intervals[i + 1][0] - current[1];
                    int padding = silence < 960 ? silence / 2 : 480;
                    current[1] = Math.Min(samples, current[1] + padding);
                    intervals[i + 1][0] = Math.Max(0, intervals[i + 1][0] - padding);
                }
                else current[1] = Math.Min(samples, current[1] + 480);
            }
            return intervals.ConvertAll(p => new MNNSpeechSegment(p[0], p[1])).ToArray();
        }
        public MNNVoiceActivityResult DetectWaveFile(string path, float threshold = .5f, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var samples = MNNWaveAudio.Read(path, out int sampleRate);
            return Detect(samples, sampleRate, threshold, cancellationToken);
        }
        public Task<MNNVoiceActivityResult> DetectAsync(float[] samples, int sampleRate, float threshold = .5f, CancellationToken cancellationToken = default)
        {
            var copy = samples == null ? null : (float[])samples.Clone();
            return Task.Run(() => Detect(copy, sampleRate, threshold, cancellationToken), cancellationToken);
        }
        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                _runner.Dispose();
            }
        }
    }
}
