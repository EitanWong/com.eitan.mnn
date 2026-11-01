using System;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace MNN.Unity
{
    /// <summary>Per-response speech filter. Feed model deltas before sentence segmentation.</summary>
    public sealed class MNNSpeechTextCleaner
    {
        private static readonly string[] UrlPrefixes = { "https://", "http://", "www.", "mailto:" };
        private static readonly Regex LinePrefix = new Regex(@"^(?:\s*[-*_]{3,}\s*(?:\r?\n|$)|\s*[#>]+\s*|\s*[-+*]\s+|\s*\d+[.)]\s+|\s*\[[ xX]\]\s*)");
        private static readonly Regex CodeStart = new Regex(@"^\s*(?:(?:using|import|from|const|let|var|class|public|private|function|def|sudo|npm|pip|git|curl)\s|\$\s)");
        private static readonly Regex CodeLine = new Regex(@"^\s*(?:(?:using|import)\s+[\w.]+;|from\s+\w+\s+import\s|(?:const|let|var)\s+\w+\s*=|(?:public|private)\s+(?:static\s+)?\w+\s+\w+\s*[(={;]|(?:function|def|class)\s+\w+\s*[:({]|(?:sudo|npm|pip|git|curl)\s+(?:install|run|clone|status|https?://)\b|\$\s+)");
        private string _pending = "";
        private string _skipUntil;
        private bool _skipUrl;
        private int _braceDepth;
        private bool _braceQuoted;
        private bool _braceEscape;
        private bool _lineStart = true;
        private bool _completed;

        /// <summary>Returns plain spoken text, or empty text when the input contains no speech.</summary>
        public static string Clean(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var cleaner = new MNNSpeechTextCleaner();
            string spoken = cleaner.Append(text) + cleaner.Complete();
            spoken = Regex.Replace(spoken, @"\s+", " ").Trim();
            return HasSpeech(spoken) ? spoken : "";
        }

        /// <summary>Returns newly stable speech. Retains incomplete markup until a later delta or Complete.</summary>
        public string Append(string delta)
        {
            if (_completed) throw new InvalidOperationException("The speech text stream is complete.");
            if (delta == null) throw new ArgumentNullException(nameof(delta));
            _pending = Regex.Replace(_pending + delta, @"&(?:#[xX][0-9a-fA-F]+|#\d+|[A-Za-z]+);",
                match => WebUtility.HtmlDecode(match.Value));
            return Drain(false);
        }

        /// <summary>Flushes remaining prose and discards unclosed code, tags and links. Call once per response.</summary>
        public string Complete()
        {
            if (_completed) return "";
            _completed = true;
            return Drain(true);
        }

        private string Drain(bool final)
        {
            var output = new StringBuilder();
            int i = 0;
            while (i < _pending.Length)
            {
                if (_skipUntil != null)
                {
                    int close = _pending.IndexOf(_skipUntil, i, StringComparison.OrdinalIgnoreCase);
                    if (close < 0)
                    {
                        // Retain only enough suffix to recognize a closing delimiter split across deltas.
                        i = final ? _pending.Length : Math.Max(i, _pending.Length - _skipUntil.Length + 1);
                        break;
                    }
                    i = close + _skipUntil.Length;
                    _skipUntil = null;
                    output.Append(' ');
                    continue;
                }
                char c = _pending[i];
                if (_braceDepth > 0)
                {
                    if (_braceEscape) _braceEscape = false;
                    else if (_braceQuoted && c == '\\') _braceEscape = true;
                    else if (c == '"') _braceQuoted = !_braceQuoted;
                    else if (!_braceQuoted && c == '{') ++_braceDepth;
                    else if (!_braceQuoted && c == '}' && --_braceDepth == 0) output.Append(' ');
                    ++i;
                    continue;
                }
                if (_skipUrl)
                {
                    if (!char.IsWhiteSpace(c) && "，。！？；：、<>\"'）)".IndexOf(c) < 0) { ++i; continue; }
                    _skipUrl = false;
                }
                if (c == '\r' || c == '\n')
                {
                    _lineStart = true;
                    output.Append(' ');
                    ++i;
                    continue;
                }
                if (_lineStart)
                {
                    // Wait for the first word/marker to become unambiguous, including split list numbers.
                    string rest = _pending.Substring(i);
                    int indentation = 0;
                    while (indentation < rest.Length && (rest[indentation] == ' ' || rest[indentation] == '\t')) ++indentation;
                    if (indentation == rest.Length && !final) break;
                    if (indentation >= 4 || rest.Substring(0, indentation).IndexOf('\t') >= 0)
                    {
                        _skipUntil = "\n";
                        continue;
                    }
                    if (!final && (!Regex.IsMatch(rest, @"\s|[\p{P}\p{S}]") ||
                        Regex.IsMatch(rest, @"^\s*(?:[#>+*\-]+|\d+[.)]?|\[[ xX]?\]?)\s*$"))) break;
                    if (CodeStart.IsMatch(rest))
                    {
                        int lineEnd = rest.IndexOf('\n');
                        if (lineEnd < 0 && !final) break;
                        string line = lineEnd < 0 ? rest : rest.Substring(0, lineEnd);
                        if (CodeLine.IsMatch(line))
                        {
                            i += line.Length;
                            continue;
                        }
                    }
                    var prefix = LinePrefix.Match(rest);
                    if (prefix.Success) { i += prefix.Length; continue; }
                    if (char.IsWhiteSpace(c)) { ++i; continue; }
                    _lineStart = false;
                }
                if (c == '{')
                {
                    _braceDepth = 1;
                    ++i;
                    continue;
                }
                if (c == '`' || c == '~')
                {
                    int end = i + 1;
                    while (end < _pending.Length && _pending[end] == c) ++end;
                    if (end == _pending.Length && !final) break;
                    int count = end - i;
                    if (c == '`' || count >= 3) _skipUntil = new string(c, count);
                    else output.Append(' ');
                    i = end;
                    continue;
                }
                if (c == '\\')
                {
                    if (i + 1 == _pending.Length && !final) break;
                    if (i + 1 < _pending.Length && (_pending[i + 1] == '(' || _pending[i + 1] == '['))
                    {
                        _skipUntil = _pending[i + 1] == '(' ? @"\)" : @"\]";
                        i += 2;
                        continue;
                    }
                }
                if (c == '$')
                {
                    if (i + 1 == _pending.Length && !final) break;
                    if (i + 1 < _pending.Length && _pending[i + 1] == '$')
                    {
                        _skipUntil = "$$";
                        i += 2;
                        continue;
                    }
                    if (i + 1 < _pending.Length && !char.IsDigit(_pending[i + 1]) && !char.IsWhiteSpace(_pending[i + 1]))
                    {
                        _skipUntil = "$";
                        ++i;
                        continue;
                    }
                }
                if (c == '<')
                {
                    // A comparison is not a tag; discard only its reserved bracket.
                    if (i + 1 == _pending.Length && !final) break;
                    if (i + 1 < _pending.Length && (char.IsLetter(_pending[i + 1]) || "/!|".IndexOf(_pending[i + 1]) >= 0))
                    {
                        int end = _pending.IndexOf('>', i + 1);
                        if (end < 0) { if (final) i = _pending.Length; break; }
                        string tag = _pending.Substring(i + 1, end - i - 1).Trim().ToLowerInvariant();
                        foreach (string hidden in new[] { "script", "style", "think", "analysis" })
                            if (tag == hidden || tag.StartsWith(hidden + " ", StringComparison.Ordinal))
                                _skipUntil = "</" + hidden + ">";
                        if (tag.StartsWith("!--", StringComparison.Ordinal)) _skipUntil = "-->";
                        // A complete comment may already end at this greater-than sign.
                        if (_skipUntil == "-->" && tag.EndsWith("--", StringComparison.Ordinal)) _skipUntil = null;
                        i = end + 1;
                        output.Append(' ');
                        continue;
                    }
                }
                if (c == '!' && i + 1 == _pending.Length && !final) break;
                if (c == '[' || c == '!' && i + 1 < _pending.Length && _pending[i + 1] == '[')
                {
                    bool image = c == '!';
                    int start = i + (image ? 2 : 1);
                    int end = _pending.IndexOf(']', start);
                    if (end < 0) { if (final) i = _pending.Length; break; }
                    int after = end + 1;
                    if (after == _pending.Length && !final) break;
                    if (after < _pending.Length && (_pending[after] == '(' || _pending[after] == '['))
                    {
                        char open = _pending[after];
                        char close = open == '(' ? ')' : ']';
                        int depth = 1;
                        int linkEnd = after + 1;
                        for (; linkEnd < _pending.Length && depth > 0; ++linkEnd)
                        {
                            if (_pending[linkEnd] == open) ++depth;
                            if (_pending[linkEnd] == close) --depth;
                        }
                        if (depth > 0) { if (final) i = _pending.Length; break; }
                        after = linkEnd;
                    }
                    else if (after < _pending.Length && _pending[after] == ':')
                    {
                        _skipUntil = "\n";
                        i = after + 1;
                        continue;
                    }
                    if (!image && (start == end || _pending[start] != '^'))
                        output.Append(Clean(_pending.Substring(start, end - start)));
                    output.Append(' ');
                    i = after;
                    continue;
                }
                bool url = false;
                int remaining = _pending.Length - i;
                foreach (string prefix in UrlPrefixes)
                {
                    if (remaining >= prefix.Length && string.Compare(_pending, i, prefix, 0, prefix.Length, StringComparison.OrdinalIgnoreCase) == 0)
                    { url = true; break; }
                    if (!final && remaining < prefix.Length && string.Compare(_pending, i, prefix, 0, remaining, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        _pending = _pending.Substring(i);
                        return output.ToString();
                    }
                }
                if (url) { _skipUrl = true; output.Append(' '); continue; }
                if (c == '&')
                {
                    int end = _pending.IndexOf(';', i + 1);
                    if (end < 0 && !final && _pending.Length - i < 16) break;
                    if (end > i && end - i < 16)
                    {
                        string entity = _pending.Substring(i, end - i + 1);
                        string decoded = WebUtility.HtmlDecode(entity);
                        if (decoded != entity)
                        {
                            _pending = _pending.Remove(i, entity.Length).Insert(i, decoded);
                            continue;
                        }
                    }
                }
                // Hold a split Unicode surrogate until the next delta arrives.
                if (char.IsHighSurrogate(c) && i + 1 == _pending.Length && !final) break;
                int length = char.IsHighSurrogate(c) && i + 1 < _pending.Length && char.IsLowSurrogate(_pending[i + 1]) ? 2 : 1;
                AppendReadable(output, _pending.Substring(i, length));
                i += length;
            }
            _pending = final ? "" : _pending.Substring(i);
            return output.ToString();
        }

        private static bool HasSpeech(string text)
        {
            for (int i = 0; i < text.Length; ++i)
                if (char.IsLetterOrDigit(text, i)) return true;
            return false;
        }

        private static void AppendReadable(StringBuilder output, string text)
        {
            for (int i = 0; i < text.Length; ++i)
            {
                UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(text, i);
                int length = char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]) ? 2 : 1;
                if (category == UnicodeCategory.UppercaseLetter || category == UnicodeCategory.LowercaseLetter ||
                    category == UnicodeCategory.TitlecaseLetter || category == UnicodeCategory.ModifierLetter ||
                    category == UnicodeCategory.OtherLetter || category == UnicodeCategory.DecimalDigitNumber ||
                    category == UnicodeCategory.NonSpacingMark || category == UnicodeCategory.SpacingCombiningMark ||
                    ".,!?;:'\"-–—…。，！？；：、‘’“”（）()".IndexOf(text[i]) >= 0)
                    output.Append(text, i, length);
                else output.Append(' ');
                i += length - 1;
            }
        }
    }
}
