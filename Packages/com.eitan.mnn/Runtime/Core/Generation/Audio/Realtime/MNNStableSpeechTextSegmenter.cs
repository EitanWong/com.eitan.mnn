using System;
using System.Text;

namespace MNN.Unity
{
    /// <summary>Turns append-only spoken text into immutable, bounded TTS segments with explicit consumer backpressure.</summary>
    public sealed class MNNStableSpeechTextSegmenter
    {
        public const int MaximumInputDeltaCharacters = 4096;
        private static readonly string[] Abbreviations =
        {
            "Mr.", "Mrs.", "Ms.", "Dr.", "Prof.", "Sr.", "Jr.", "St.", "vs.", "etc.", "e.g.", "i.e."
        };

        private readonly StringBuilder _pending = new StringBuilder();
        private readonly int _maximumCharacters;
        private bool _completed;
        private bool _emitting;
        private bool _blockedOnConsumer;

        public MNNStableSpeechTextSegmenter(int maximumCharacters = 180)
        {
            if (maximumCharacters < 8 || maximumCharacters > 4096)
                throw new ArgumentOutOfRangeException(nameof(maximumCharacters));
            _maximumCharacters = maximumCharacters;
        }

        public int PendingCharacters => _pending.Length;
        public bool IsCompleted => _completed;

        /// <summary>Append a model text delta and synchronously emit each newly stable segment once.</summary>
        public bool Append(string textDelta, Func<string, bool> emitStableSegment)
        {
            if (textDelta == null) throw new ArgumentNullException(nameof(textDelta));
            if (emitStableSegment == null) throw new ArgumentNullException(nameof(emitStableSegment));
            if (textDelta.Length > MaximumInputDeltaCharacters)
                throw new ArgumentOutOfRangeException(nameof(textDelta), "Split large model text deltas before appending.");
            if (_completed) throw new InvalidOperationException("The text stream is complete.");
            if (_blockedOnConsumer) throw new InvalidOperationException("Drain pending segments before appending more model text.");
            if (_emitting) throw new InvalidOperationException("Text segment callbacks cannot re-enter the segmenter.");
            _pending.Append(textDelta);
            _emitting = true;
            try
            {
                _blockedOnConsumer = !EmitAvailable(emitStableSegment, flush: false);
                return !_blockedOnConsumer;
            }
            finally { _emitting = false; }
        }

        /// <summary>Retry pending stable segments without appending another model delta.</summary>
        public bool DrainStableSegments(Func<string, bool> emitStableSegment)
        {
            if (emitStableSegment == null) throw new ArgumentNullException(nameof(emitStableSegment));
            if (_completed) return true;
            if (_emitting) throw new InvalidOperationException("Text segment callbacks cannot re-enter the segmenter.");
            _emitting = true;
            try
            {
                _blockedOnConsumer = !EmitAvailable(emitStableSegment, flush: false);
                return !_blockedOnConsumer;
            }
            finally { _emitting = false; }
        }

        /// <summary>Emit the remaining stable text. Returns false when the consumer applies backpressure.</summary>
        public bool Complete(Func<string, bool> emitStableSegment)
        {
            if (emitStableSegment == null) throw new ArgumentNullException(nameof(emitStableSegment));
            if (_completed) return true;
            if (_emitting) throw new InvalidOperationException("Text segment callbacks cannot re-enter the segmenter.");
            _emitting = true;
            try
            {
                if (!EmitAvailable(emitStableSegment, flush: true))
                {
                    _blockedOnConsumer = true;
                    return false;
                }
                _blockedOnConsumer = false;
                _completed = true;
                return true;
            }
            finally { _emitting = false; }
        }

        private bool EmitAvailable(Func<string, bool> emit, bool flush)
        {
            while (_pending.Length > 0)
            {
                int boundary = FindSentenceBoundary(flush);
                if (boundary < 0 && _pending.Length > _maximumCharacters)
                    boundary = FindSoftBoundary();
                if (boundary < 0)
                    break;

                if (!EmitPrefix(boundary, emit))
                    return false;
            }

            if (flush && _pending.Length > 0)
                return EmitPrefix(_pending.Length, emit);
            return true;
        }

        private int FindSentenceBoundary(bool flush)
        {
            for (int i = 0; i < _pending.Length; ++i)
            {
                char current = _pending[i];
                if (!IsSentencePunctuation(current))
                    continue;
                if (current == '.' && IsNonTerminalPeriod(i))
                    continue;

                int end = i + 1;
                while (end < _pending.Length && IsClosingQuoteOrBracket(_pending[end]))
                    ++end;
                if (end == _pending.Length)
                {
                    if (flush)
                        return end;
                    // A terminal CJK mark is self-delimiting. Waiting for another token
                    // delays speech until generation EOS on short single-sentence replies.
                    if (IsTerminalWidePunctuation(current))
                        return end;
                    continue;
                }
                if (char.IsWhiteSpace(_pending[end]) || IsWideSentencePunctuation(current))
                    return end;
            }
            return -1;
        }

        private bool IsNonTerminalPeriod(int index)
        {
            if (index > 0 && char.IsDigit(_pending[index - 1]) &&
                index + 1 < _pending.Length && char.IsDigit(_pending[index + 1]))
                return true;

            int start = index;
            while (start > 0 && !char.IsWhiteSpace(_pending[start - 1]))
                --start;
            int length = index - start + 1;
            if (length < 2 || length > 8)
                return false;

            for (int i = 0; i < Abbreviations.Length; ++i)
            {
                string abbreviation = Abbreviations[i];
                if (abbreviation.Length != length)
                    continue;
                bool equal = true;
                for (int j = 0; j < length; ++j)
                    if (char.ToLowerInvariant(_pending[start + j]) != char.ToLowerInvariant(abbreviation[j]))
                    {
                        equal = false;
                        break;
                    }
                if (equal)
                    return true;
            }
            return false;
        }

        private int FindSoftBoundary()
        {
            int limit = Math.Min(_maximumCharacters, _pending.Length - 1);
            for (int i = limit; i > 0; --i)
            {
                if (!char.IsWhiteSpace(_pending[i]))
                    continue;
                int boundary = i;
                while (boundary > 0 && char.IsWhiteSpace(_pending[boundary - 1]))
                    --boundary;
                if (boundary > 0)
                    return boundary;
            }

            int hardBoundary = _maximumCharacters;
            if (char.IsHighSurrogate(_pending[hardBoundary - 1]) && char.IsLowSurrogate(_pending[hardBoundary]))
                --hardBoundary;
            if (_pending[hardBoundary - 1] == '\r' && _pending[hardBoundary] == '\n')
                --hardBoundary;
            return hardBoundary;
        }

        private bool EmitPrefix(int characterCount, Func<string, bool> emit)
        {
            int consumed = Math.Min(characterCount, _pending.Length);
            string segment = _pending.ToString(0, consumed).Trim();
            if (segment.Length > 0 && !emit(segment))
                return false;
            _pending.Remove(0, consumed);
            while (_pending.Length > 0 && char.IsWhiteSpace(_pending[0]))
                _pending.Remove(0, 1);
            return true;
        }

        private static bool IsSentencePunctuation(char value) =>
            value == '.' || value == '!' || value == '?' || value == ',' || value == ';' || value == ':' ||
            value == '\u3002' || value == '\uff01' || value == '\uff1f' || value == '\u061f' ||
            value == '\uff0c' || value == '\u3001' || value == '\uff1b' || value == '\uff1a';

        private static bool IsWideSentencePunctuation(char value) =>
            value == '\u3002' || value == '\uff01' || value == '\uff1f' || value == '\u061f' ||
            value == '\uff0c' || value == '\u3001' || value == '\uff1b' || value == '\uff1a';

        private static bool IsTerminalWidePunctuation(char value) =>
            value == '\u3002' || value == '\uff01' || value == '\uff1f' || value == '\u061f';

        private static bool IsClosingQuoteOrBracket(char value) =>
            value == '\'' || value == '"' || value == ')' || value == ']' || value == '}' ||
            value == '\u2019' || value == '\u201d' || value == '\u3009' || value == '\u300b' ||
            value == '\u300d' || value == '\u300f' || value == '\u3011';
    }
}
