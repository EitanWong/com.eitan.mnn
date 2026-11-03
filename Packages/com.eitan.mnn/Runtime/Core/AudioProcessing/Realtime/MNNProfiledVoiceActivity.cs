using System;
using System.IO;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>Adapts profiled offline VAD to fixed capture frames on a worker. Uses a bounded 640 ms context;
    /// this recomputes features and makes no inference-throughput guarantee. Silero retains its native streaming path.</summary>
    public sealed class MNNProfiledVoiceActivity : IDisposable, IMNNRealtimeVoiceActivity
    {
        private readonly MNNRealtimeVoiceActivity _stream;
        private readonly MNNVoiceActivityDetector _windowed;
        private readonly float[] _window = new float[10240];
        private int _count, _silent;
        private long _position;
        private bool _active;
        public MNNBackendType Backend => _stream?.Backend ?? _windowed.Backend;
        private MNNProfiledVoiceActivity(MNNRealtimeVoiceActivity stream, MNNVoiceActivityDetector windowed)
        { _stream = stream; _windowed = windowed; }
        public static MNNProfiledVoiceActivity Load(string directory, int threadCount = 1, MNNBackendType backendType = MNNBackendType.Auto) =>
            File.Exists(Path.Combine(directory, MNNSileroVadConfig.FileName))
                ? new MNNProfiledVoiceActivity(MNNRealtimeVoiceActivity.Load(directory, threadCount, backendType,
                    threshold: .7f, releaseThreshold: .5f, hangoverMilliseconds: 64), null)
                : new MNNProfiledVoiceActivity(null, MNNVoiceActivityDetector.Load(directory, threadCount, backendType));
        public MNNRealtimeVadUpdate ProcessFrame(float[] samples, int offset)
        {
            if (_stream != null) return _stream.ProcessFrame(samples, offset);
            if (samples == null) throw new ArgumentNullException(nameof(samples));
            if (offset < 0 || offset > samples.Length - 512) throw new ArgumentOutOfRangeException(nameof(offset));
            for (int i = offset; i < offset + 512; ++i)
                if (!(samples[i] >= -1 && samples[i] <= 1)) throw new ArgumentException("Supply finite normalized PCM.", nameof(samples));
            if (_count == _window.Length) Array.Copy(_window, 512, _window, 0, _count -= 512);
            Array.Copy(samples, offset, _window, _count, 512); _count += 512;
            var audio = new float[_count]; Array.Copy(_window, audio, _count);
            var result = _windowed.Detect(audio, 16000);
            // Only classify this frame; old speech elsewhere in the window must not keep VAD active.
            var probabilities = result.Probabilities;
            float probability = probabilities.Count == 0 ? 0 : probabilities[probabilities.Count - 1];
            bool wasActive = _active;
            if (probability >= .7f) { _active = true; _silent = 0; }
            else if (_active && probability < .5f && ++_silent >= 2) { _active = false; _silent = 0; }
            else if (probability >= .5f) _silent = 0;
            var update = new MNNRealtimeVadUpdate(_position, probability, _active, !wasActive && _active, wasActive && !_active);
            _position += 512;
            return update;
        }
        public void Reset()
        { _stream?.Reset(); Array.Clear(_window, 0, _window.Length); _count = _silent = 0; _position = 0; _active = false; }
        public void Dispose() { _stream?.Dispose(); _windowed?.Dispose(); }
    }
}
