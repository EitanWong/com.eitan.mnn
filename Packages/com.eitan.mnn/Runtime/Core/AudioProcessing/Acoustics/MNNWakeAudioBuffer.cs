using System;

namespace MNN.Unity
{
    /// <summary>Bounded command audio after a keyword; copied once when realtime listening starts.</summary>
    public sealed class MNNWakeAudioBuffer
    {
        private readonly object _sync = new object();
        private readonly float[] _ring = new float[512 * 62];
        private long _position, _revision, _keywordEnd = -1;
        public long Position { get { lock (_sync) return _position; } }
        public long Revision { get { lock (_sync) return _revision; } }
        public void Append(float[] frame)
            => Append(frame, 0, frame == null ? 0 : frame.Length);
        public void Append(float[] frame, int offset, int count)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            if (offset < 0 || count < 0 || offset > frame.Length - (long)count) throw new ArgumentOutOfRangeException(nameof(count));
            lock (_sync)
            {
                if (_position > long.MaxValue - count) throw new InvalidOperationException("Reset the wake stream before its cursor overflows.");
                for (int i = offset; i < offset + count; ++i) _ring[_position++ % _ring.Length] = frame[i];
            }
        }
        public void Mark(double endSeconds)
        {
            if (double.IsNaN(endSeconds) || double.IsInfinity(endSeconds) || endSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(endSeconds));
            lock (_sync)
                if (_keywordEnd < 0) _keywordEnd = endSeconds >= _position / 16000.0 ? _position : (long)Math.Ceiling(endSeconds * 16000);
        }
        public float[] Take()
        {
            lock (_sync)
            {
                if (_keywordEnd < 0) return null;
                long start = Math.Max(_keywordEnd, _position - _ring.Length);
                int count = (int)(_position - start);
                // Runtime consumes whole model frames; retain the immediate command onset.
                var result = new float[(count + 511) / 512 * 512];
                for (int i = 0; i < count; ++i) result[i] = _ring[(start + i) % _ring.Length];
                _keywordEnd = -1;
                return result;
            }
        }
        public MNNConversationWakeHandoff TakeHandoff()
        {
            lock (_sync)
            {
                if (_keywordEnd < 0) return null;
                long start = _keywordEnd / 512 * 512;
                long end = _position / 512 * 512;
                bool lost = start < _position - _ring.Length;
                var result = new float[lost ? 0 : (int)(end - start)];
                for (int i = 0; i < result.Length; ++i) result[i] = _ring[(start + i) % _ring.Length];
                _keywordEnd = -1;
                return new MNNConversationWakeHandoff(result, end, _revision, lost);
            }
        }
        public void Discard() { lock (_sync) _keywordEnd = -1; }
        public void Reset() { lock (_sync) { ++_revision; _position = 0; _keywordEnd = -1; } }
    }
}
