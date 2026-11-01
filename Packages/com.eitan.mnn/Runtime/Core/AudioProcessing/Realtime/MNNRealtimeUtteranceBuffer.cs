using System;

namespace MNN.Unity
{
    /// <summary>
    /// Single input-owner endpoint buffer. Fixed memory, configurable pre-roll, independent speech/silence budgets.
    /// Silence produces a candidate, not a semantic decision. Resume/gaps invalidate candidate revisions.
    /// </summary>
    public sealed class MNNRealtimeUtteranceBuffer
    {
        private readonly float[] _audio;
        private readonly float[] _preRoll;
        private readonly int _minimumSpeech;
        private readonly int _silence;
        private int _length, _speech, _silent, _preCount;
        private long _revision;
        public long Revision => _revision;
        public int SilenceSamples => _silent;
        /// <summary>Any speech has entered the current turn, including speech below the acceptance threshold.</summary>
        public bool HasSpeechStarted => _speech > 0;
        public bool HasSpeech => _speech >= _minimumSpeech;
        public bool IsCandidate => HasSpeech && (_silent >= _silence || _length == _audio.Length);
        public bool IsFull => _length == _audio.Length;
        public MNNRealtimeUtteranceBuffer(int minimumSpeechMilliseconds = 140, int silenceMilliseconds = 550, int maximumSeconds = 30, int preRollMilliseconds = 96)
        {
            if (minimumSpeechMilliseconds < 20 || minimumSpeechMilliseconds > 2000 || silenceMilliseconds < 100 || silenceMilliseconds > 5000 || maximumSeconds < 2 || maximumSeconds > 120)
                throw new ArgumentOutOfRangeException(nameof(maximumSeconds));
            if (preRollMilliseconds < 0 || preRollMilliseconds > 2000) throw new ArgumentOutOfRangeException(nameof(preRollMilliseconds));
            _preRoll = new float[((preRollMilliseconds * 16 + 511) / 512) * 512];
            _minimumSpeech = minimumSpeechMilliseconds * 16;
            _silence = silenceMilliseconds * 16;
            _audio = new float[maximumSeconds * 16000];
        }
        public void Append(float[] frame, bool speech, bool discontinuity = false)
        {
            if (frame == null || frame.Length < 512) throw new ArgumentException("Supply 512 mono samples at 16 kHz.");
            if (discontinuity) Reset();
            if (speech)
            {
                if (_silent > 0 || _speech == 0) ++_revision;
                _silent = 0;
                if (_length == 0 && _preCount > 0)
                {
                    Array.Copy(_preRoll, 0, _audio, 0, _preCount);
                    _length = _preCount;
                }
                _speech += 512;
            }
            else if (_length > 0) _silent += 512;
            if (_length > 0 || speech)
            {
                int copy = Math.Min(512, _audio.Length - _length);
                Array.Copy(frame, 0, _audio, _length, copy);
                _length += copy;
                // Discard isolated clicks/noise that never met the speech budget.
                if (!HasSpeech && _silent >= _silence) Reset();
            }
            else if (_preRoll.Length > 0)
            {
                if (_preCount == _preRoll.Length) { Array.Copy(_preRoll, 512, _preRoll, 0, _preRoll.Length - 512); _preCount -= 512; }
                Array.Copy(frame, 0, _preRoll, _preCount, 512);
                _preCount += 512;
            }
        }
        public float[] Snapshot()
        {
            if (!HasSpeech) throw new InvalidOperationException("No confirmed speech.");
            var copy = new float[_length];
            Array.Copy(_audio, copy, _length);
            return copy;
        }
        public void Reset() { _length = _speech = _silent = _preCount = 0; ++_revision; }
    }
}
