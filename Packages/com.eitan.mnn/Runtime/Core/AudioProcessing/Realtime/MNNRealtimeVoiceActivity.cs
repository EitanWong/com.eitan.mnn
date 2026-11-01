using System;
using System.IO;

namespace MNN.Unity
{
    /// <summary>
    /// Stateful Silero v5 inference for consecutive mono 16 kHz PCM. Run on an audio-processing worker,
    /// never from an audio-device callback. Call Reset after any lost samples or format change.
    /// </summary>
    public sealed class MNNRealtimeVoiceActivity : IDisposable, IMNNRealtimeVoiceActivity
    {
        public const int SampleRate = 16000;
        public const int SamplesPerInference = 512;
        public const int ContextSamples = 64;
        public const int StateSamples = 256;
        private readonly object _gate = new object();
        private readonly MNNSileroFrameRunner _runner;
        private readonly float[] _output = new float[257];
        private readonly float[] _input = new float[ContextSamples + SamplesPerInference + StateSamples];
        private long _streamSamples;
        private int _silentFrames;
        private bool _active;
        private bool _disposed;

        private MNNRealtimeVoiceActivity(string directory, int threads, MNNBackendType backend)
        {
            _runner = new MNNSileroFrameRunner(directory, threads, backend);
        }

        public static MNNRealtimeVoiceActivity Load(string modelDirectory, int threadCount = 1,
            MNNBackendType backendType = MNNBackendType.Auto, float threshold = .5f,
            float releaseThreshold = .35f, int hangoverMilliseconds = 120)
        {
            MNNPlatformSupport.RequireSupported();
            if (string.IsNullOrWhiteSpace(modelDirectory)) throw new ArgumentNullException(nameof(modelDirectory));
            if (threadCount < 1 || threadCount > 64) throw new ArgumentOutOfRangeException(nameof(threadCount));
            if (!Enum.IsDefined(typeof(MNNBackendType), backendType)) throw new ArgumentOutOfRangeException(nameof(backendType));
            ValidateProbability(threshold, nameof(threshold));
            ValidateProbability(releaseThreshold, nameof(releaseThreshold));
            if (releaseThreshold > threshold) throw new ArgumentOutOfRangeException(nameof(releaseThreshold));
            if (hangoverMilliseconds < 0 || hangoverMilliseconds > 2000)
                throw new ArgumentOutOfRangeException(nameof(hangoverMilliseconds));
            return new MNNRealtimeVoiceActivity(Path.GetFullPath(modelDirectory), threadCount, backendType)
            {
                Threshold = threshold,
                ReleaseThreshold = releaseThreshold,
                HangoverFrames = Math.Max(1, (hangoverMilliseconds * SampleRate + 999) / (1000 * SamplesPerInference))
            };
        }

        public float Threshold { get; private set; }
        public MNNBackendType Backend => _runner.Backend;
        public float ReleaseThreshold { get; private set; }
        public int HangoverFrames { get; private set; }
        public long ProcessedSamples { get { lock (_gate) return _streamSamples; } }
        public bool IsSpeechActive { get { lock (_gate) return _active; } }

        /// <summary>Processes one consecutive 32 ms frame of normalized mono 16 kHz PCM on a worker thread.</summary>
        public MNNRealtimeVadUpdate ProcessFrame(float[] samples, int offset)
        {
            if (samples == null) throw new ArgumentNullException(nameof(samples));
            if (offset < 0 || offset > samples.Length - SamplesPerInference)
                throw new ArgumentOutOfRangeException(nameof(offset));
            for (int i = offset; i < offset + SamplesPerInference; ++i)
            {
                float value = samples[i];
                if (float.IsNaN(value) || float.IsInfinity(value) || value < -1 || value > 1)
                    throw new ArgumentException("Input must contain finite normalized PCM.", nameof(samples));
            }

            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(MNNRealtimeVoiceActivity));
                long frameStart = _streamSamples;
                Array.Copy(samples, offset, _input, ContextSamples, SamplesPerInference);
                _runner.Run(_input, _output);
                var output = _output;
                if (output.Length != 257 || output[0] < 0 || output[0] > 1)
                    throw new InvalidDataException("Invalid Silero output.");
                Array.Copy(_input, SamplesPerInference, _input, 0, ContextSamples);
                Array.Copy(output, 1, _input, ContextSamples + SamplesPerInference, StateSamples);
                _streamSamples += SamplesPerInference;
                bool started = false;
                bool ended = false;
                if (!_active && output[0] >= Threshold)
                {
                    _active = true;
                    _silentFrames = 0;
                    started = true;
                }
                else if (_active)
                {
                    if (output[0] < ReleaseThreshold)
                    {
                        if (++_silentFrames >= HangoverFrames)
                        {
                            _active = false;
                            _silentFrames = 0;
                            ended = true;
                        }
                    }
                    else _silentFrames = 0;
                }
                return new MNNRealtimeVadUpdate(frameStart, output[0], _active, started, ended);
            }
        }

        /// <summary>Reset on stream start, explicit gap, format change or model recovery.</summary>
        public void Reset()
        {
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(MNNRealtimeVoiceActivity));
                Array.Clear(_input, 0, _input.Length);
                _streamSamples = 0;
                _silentFrames = 0;
                _active = false;
            }
        }

        private static void ValidateProbability(float value, string name)
        {
            if (float.IsNaN(value) || value < 0 || value > 1)
                throw new ArgumentOutOfRangeException(name);
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
