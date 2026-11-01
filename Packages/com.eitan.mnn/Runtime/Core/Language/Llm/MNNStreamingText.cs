using System;
using System.Text;

namespace MNN.Unity
{
    // Decode only new native bytes; preserve an incomplete UTF-8 scalar between
    // tokens. Delta mode never materializes cumulative snapshots.
    internal sealed class MNNStreamingText
    {
        private readonly Decoder _decoder = Encoding.UTF8.GetDecoder();
        private readonly StringBuilder _text;
        private char[] _characters = new char[64];

        internal MNNStreamingText(bool deltas = false)
        {
            if (!deltas)
                _text = new StringBuilder();
        }

        internal string Append(byte[] bytes) => Append(bytes, bytes.Length);

        internal string Append(byte[] bytes, int length)
        {
            int capacity = Encoding.UTF8.GetMaxCharCount(length);
            if (_characters.Length < capacity)
                Array.Resize(ref _characters, capacity);
            int count = _decoder.GetChars(bytes, 0, length, _characters, 0, false);
            if (count == 0)
                return null;
            if (_text == null)
                return new string(_characters, 0, count);
            _text.Append(_characters, 0, count);
            return _text.ToString();
        }
    }
}
