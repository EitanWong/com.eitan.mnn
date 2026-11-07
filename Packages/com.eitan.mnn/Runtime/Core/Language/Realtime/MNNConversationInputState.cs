using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace MNN.Unity
{
    /// <summary>Thread-safe input ownership. Manual recordings are bounded mono PCM at 16 kHz.</summary>
    public sealed class MNNConversationInputState : IMNNConversationInput
    {
        public const int SampleRate = 16000;
        public const int MaximumSamples = SampleRate * 60;
        private readonly object _sync = new object();
        // Allocate one second at a time. Short/cancelled gestures never reserve a full minute,
        // and growing a recording never recopies earlier PCM on the input worker.
        private const int RecordingBlockSamples = SampleRate;
        private List<float[]> _recording;
        private int _count, _revision;
        private long _modeStartedAt;
        private string _recordingError;
        private float _level;
        // The input worker owns analysis; readers receive an immutable snapshot under _sync.
        private readonly MNNAudioAnalysis _voiceAnalysis = new MNNAudioAnalysis();
        private MNNAudioFeatures _voiceFeatures;
        private readonly float[] _waveform = new float[64];
        private int _waveIndex, _binSamples;
        private double _binEnergy;
        private long _meterUpdated;
        private bool _speaking;
        private bool MeterFresh => Stopwatch.GetTimestamp() - _meterUpdated < Stopwatch.Frequency / 5;
        public bool Speaking { get { lock (_sync) return _realtime && _speaking && MeterFresh; } }
        public bool TryReadWaveform(float[] points)
        {
            if (points == null || points.Length != 64) throw new ArgumentException("Supply 64 voice history points.");
            lock (_sync)
            {
                bool fresh = MeterFresh && (_realtime || _recording != null);
                for (int i = 0; i < points.Length; ++i) points[i] = fresh ? _waveform[(_waveIndex + i) % 64] : 0f;
                return fresh;
            }
        }
        private void ResetMeter()
        {
            _level = 0f; _speaking = false; _meterUpdated = 0;
            _voiceFeatures = default; _voiceAnalysis.Clear();
            _waveIndex = _binSamples = 0; _binEnergy = 0;
            Array.Clear(_waveform, 0, _waveform.Length);
        }
        private bool _realtime;
        public MNNConversationInputState(bool realtime = false) { _realtime = realtime; }
        /// <summary>Reserved recording PCM capacity; excludes the final returned snapshot.</summary>
        public int RecordingAllocatedSamples { get { lock (_sync) return (_recording?.Count ?? 0) * RecordingBlockSamples; } }
        public bool Realtime { get { lock (_sync) return _realtime; } }
        public bool Recording { get { lock (_sync) return _recording != null; } }
        public int RealtimeRevision { get { lock (_sync) return _realtime ? _revision : -1; } }
        public int Revision { get { lock (_sync) return _revision; } }
        public float Level { get { lock (_sync) return MeterFresh ? _level : 0f; } }
        public MNNAudioFeatures VoiceFeatures { get { lock (_sync) return MeterFresh ? _voiceFeatures : default; } }
        public float Duration { get { lock (_sync) return (float)_count / SampleRate; } }
        public void SetRealtime(bool enabled) => SetRealtime(enabled, null);
        // Publish a wake snapshot before the new revision can be observed by the input worker.
        public void SetRealtime(bool enabled, Action initialize)
        {
            lock (_sync)
            {
                _modeStartedAt = Stopwatch.GetTimestamp();
                initialize?.Invoke();
                _realtime = enabled; _recording = null; _count = 0; ResetMeter(); ++_revision;
            }
        }
        public void BeginRecording()
        {
            lock (_sync)
            {
                if (_recording != null) throw new InvalidOperationException("A recording is already active.");
                _realtime = false;
                _recording = new List<float[]>(4);
                _count = 0;
                _recordingError = null;
                ResetMeter();
                _modeStartedAt = Stopwatch.GetTimestamp();
                ++_revision;
            }
        }
        /// <summary>Consumes manual/idle frames; only live conversation frames reach turn detection.</summary>
        public bool Consume(float[] frame, bool speechActive = false, long enqueuedMonotonicTicks = 0, bool discontinuity = false)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            lock (_sync)
            {
                // Keep the first fresh frame even when the input worker observes a mode
                // change late. Only queued PCM from before that boundary is discarded.
                if (enqueuedMonotonicTicks > 0 && enqueuedMonotonicTicks < _modeStartedAt) return true;
                if (_recording != null && _count > 0 && discontinuity)
                    _recordingError ??= "Recording contains missing microphone samples. Please record again.";
                if (_realtime || _recording != null)
                {
                    // A gap begins a new acoustic window, even when the input mode is unchanged.
                    if (discontinuity || !MeterFresh || frame.Length == 0) _voiceAnalysis.Clear();
                    _voiceFeatures = frame.Length > 0 ? _voiceAnalysis.Analyze(frame, frame.Length, 1, SampleRate) : default;
                    double energy = 0;
                    for (int i = 0; i < frame.Length; ++i)
                    {
                        float value = frame[i];
                        if (float.IsNaN(value) || float.IsInfinity(value)) value = 0f;
                        energy += value * value;
                        _binEnergy += value * value;
                        if (++_binSamples == 320) // 20 ms bins, 1.28 seconds of actual capture history.
                        {
                            _waveform[_waveIndex] = Math.Min(1f, (float)Math.Sqrt(_binEnergy / _binSamples) * 16f);
                            _waveIndex = (_waveIndex + 1) % _waveform.Length;
                            _binSamples = 0; _binEnergy = 0;
                        }
                    }
                    _level = frame.Length > 0 ? Math.Min(1f, (float)Math.Sqrt(energy / frame.Length) * 16f) : 0f;
                    _speaking = speechActive;
                    _meterUpdated = Stopwatch.GetTimestamp();
                }
                if (_recording != null)
                {
                    if (frame.Length > MaximumSamples - _count)
                        _recordingError ??= "Recording exceeded the 60 second limit. Please record a shorter sentence.";
                    int take = Math.Min(frame.Length, MaximumSamples - _count);
                    int offset = 0;
                    while (offset < take)
                    {
                        int block = _count / RecordingBlockSamples;
                        int position = _count % RecordingBlockSamples;
                        if (block == _recording.Count) _recording.Add(new float[RecordingBlockSamples]);
                        int copy = Math.Min(take - offset, RecordingBlockSamples - position);
                        Array.Copy(frame, offset, _recording[block], position, copy);
                        _count += copy;
                        offset += copy;
                    }
                }
                return !_realtime;
            }
        }
        // Creation of an automatic reply epoch is atomic with leaving realtime mode.
        public bool TryAcceptRealtime(int revision, Action accept)
        {
            lock (_sync)
            {
                if (!_realtime || revision != _revision) return false;
                accept();
                return true;
            }
        }
        public float[] FinishRecording(bool cancel)
        {
            lock (_sync)
            {
                if (!cancel && _recording != null && _recordingError != null)
                {
                    _recording = null;
                    _count = 0;
                    ResetMeter();
                    ++_revision;
                    throw new InvalidOperationException(_recordingError);
                }
                float[] result = null;
                if (!cancel && _recording != null && _count >= SampleRate / 4)
                {
                    if (_count == RecordingBlockSamples) result = _recording[0];
                    else
                    {
                        result = new float[_count];
                        int offset = 0;
                        foreach (var block in _recording)
                        {
                            int copy = Math.Min(block.Length, _count - offset);
                            Array.Copy(block, 0, result, offset, copy);
                            offset += copy;
                        }
                    }
                }
                _recording = null;
                _count = 0;
                ResetMeter();
                _modeStartedAt = Stopwatch.GetTimestamp();
                ++_revision;
                return result;
            }
        }
    }
}
