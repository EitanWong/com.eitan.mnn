using System;

namespace MNN.Unity
{
    public static partial class MNNMarkdownRenderer
    {
        private static int ReadBlockCodeComment(string code, int start, bool nested)
        {
            if (!nested) return CodeDelimitedEnd(code, start + 2, "*/");
            int depth = 1, position = start + 2;
            while (position + 1 < code.Length)
            {
                if (code[position] == '/' && code[position + 1] == '*') { ++depth; position += 2; }
                else if (code[position] == '*' && code[position + 1] == '/')
                {
                    position += 2;
                    if (--depth == 0) return position;
                }
                else ++position;
            }
            return code.Length;
        }

        private static int ReadSpecialCodeString(string code, int start, MNNMarkdownCodeKind kind)
        {
            switch (kind)
            {
                case MNNMarkdownCodeKind.Lua: return ReadLuaLongCode(code, start);
                case MNNMarkdownCodeKind.Rust: return ReadRustCodeString(code, start);
                case MNNMarkdownCodeKind.Swift:
                    if (code[start] != '#') return start;
                    int hashes = CodeRun(code, start, '#');
                    int quote = start + hashes;
                    if (quote >= code.Length || code[quote] != '"') return start;
                    int delimiter = quote + 2 < code.Length && code[quote + 1] == '"' && code[quote + 2] == '"' ? 3 : 1;
                    return ReadHashQuotedCode(code, quote + delimiter, delimiter, hashes, true, delimiter > 1);
                default: return start;
            }
        }

        private static int ReadRustCodeString(string code, int start)
        {
            int prefix = start;
            if (code[prefix] == 'b' || code[prefix] == 'c') ++prefix;
            if (prefix < code.Length && code[prefix] == 'r')
            {
                int quote = prefix + 1;
                while (quote < code.Length && code[quote] == '#') ++quote;
                if (quote < code.Length && code[quote] == '"')
                    return ReadHashQuotedCode(code, quote + 1, 1, quote - prefix - 1, false, true);
            }
            if (prefix < code.Length && code[prefix] == '"')
                return ReadQuotedCode(code, prefix + 1, '"', 1, false, true, true);
            if (prefix >= code.Length || code[prefix] != '\'') return start;
            int end = prefix + 1;
            if (end >= code.Length || code[end] == '\r' || code[end] == '\n') return start;
            if (code[end] == '\\' && end + 1 < code.Length)
            {
                char escape = code[end + 1];
                end += 2;
                if (escape == 'x') end += 2;
                else if (escape == 'u' && end < code.Length && code[end] == '{')
                {
                    ++end;
                    while (end < code.Length && IsCodeBaseDigit(code[end], 16)) ++end;
                    if (end < code.Length && code[end] == '}') ++end;
                }
            }
            else end += end + 1 < code.Length && char.IsHighSurrogate(code[end]) && char.IsLowSurrogate(code[end + 1]) ? 2 : 1;
            return end < code.Length && code[end] == '\'' ? end + 1 : start;
        }

        private static int ReadHashQuotedCode(string code, int position, int quotes, int hashes, bool escapes, bool multiline)
        {
            while (position < code.Length)
            {
                if (!multiline && (code[position] == '\n' || code[position] == '\r')) return position;
                if (escapes && code[position] == '\\' && CodeMarkerCount(code, position + 1, '#', hashes) == hashes)
                { position = CodeEscapeEnd(code, position + hashes + 1); continue; }
                if (code[position] == '"')
                {
                    int run = CodeMarkerCount(code, position, '"', quotes);
                    if (run == quotes && CodeMarkerCount(code, position + quotes, '#', hashes) == hashes)
                        return position + quotes + hashes;
                    position += run;
                }
                else ++position;
            }
            return position;
        }

        private static int CodeMarkerCount(string code, int start, char marker, int limit)
        {
            int length = 0;
            while (length < limit && start + length < code.Length && code[start + length] == marker) ++length;
            return length;
        }

        private static int CodeEscapeEnd(string code, int escaped)
        {
            if (escaped >= code.Length) return code.Length;
            return escaped + (code[escaped] == '\r' && escaped + 1 < code.Length && code[escaped + 1] == '\n' ? 2 : 1);
        }

        private static int ReadLuaLongCode(string code, int start)
        {
            if (start >= code.Length || code[start] != '[') return start;
            int open = start + 1;
            while (open < code.Length && code[open] == '=') ++open;
            if (open >= code.Length || code[open] != '[') return start;
            int level = open - start - 1;
            int position = open + 1;
            while (position < code.Length)
            {
                if (code[position++] != ']') continue;
                int count = CodeMarkerCount(code, position, '=', level);
                if (count == level && position + count < code.Length && code[position + count] == ']') return position + count + 1;
                position += count;
            }
            return code.Length;
        }

        private static int ReadLanguageCodeToken(string code, int start, MNNMarkdownCodeLanguage language,
            MNNMarkdownPalette palette, out string color)
        {
            var kind = language.Kind;
            color = null;
            char value = code[start];
            if (value == '`' && language.Has(MNNMarkdownCodeFeatures.EscapedIdentifiers))
                return ReadQuotedCode(code, start + 1, '`', 1, false, false, false);
            if (kind == MNNMarkdownCodeKind.Swift && value == '#') return start + CodeRun(code, start, '#');
            if (kind == MNNMarkdownCodeKind.Rust)
            {
                int name = value == '\'' ? start + 1 :
                    value == 'r' && start + 1 < code.Length && code[start + 1] == '#' ? start + 2 : start;
                if (name > start && name < code.Length && IsCodeIdentifier(code[name]))
                {
                    while (name < code.Length && IsCodeIdentifierPart(code[name])) ++name;
                    return name;
                }
            }
            if (kind == MNNMarkdownCodeKind.Yaml && (value == '|' || value == '>'))
            {
                int end = ReadYamlCodeScalar(code, start);
                if (end > start) { color = palette.SyntaxString; return end; }
            }
            if (kind == MNNMarkdownCodeKind.Css && value == '#')
            {
                int end = start + 1;
                while (end < code.Length && IsCodeBaseDigit(code[end], 16)) ++end;
                int length = end - start - 1;
                if ((length == 3 || length == 4 || length == 6 || length == 8) &&
                    (end == code.Length || !IsLanguageIdentifierPart(code[end], kind)))
                { color = palette.SyntaxNumber; return end; }
            }
            return start;
        }

        private static int ReadRubyBlockComment(string code, int start)
        {
            if (start > 0 && code[start - 1] != '\n' && code[start - 1] != '\r' || !CodeWordAt(code, start, "=begin")) return start;
            int line = CodeNextLine(code, CodeLineEnd(code, start));
            while (line < code.Length)
            {
                int end = CodeLineEnd(code, line);
                if (CodeWordAt(code, line, "=end")) return end;
                line = CodeNextLine(code, end);
            }
            return code.Length;
        }

        private static bool CodeWordAt(string code, int start, string word) => start + word.Length <= code.Length &&
            string.CompareOrdinal(code, start, word, 0, word.Length) == 0 &&
            (start + word.Length == code.Length || char.IsWhiteSpace(code[start + word.Length]));

        private static int ReadYamlCodeScalar(string code, int start)
        {
            int header = start + 1, explicitIndent = 0;
            for (int count = 0; count < 2 && header < code.Length; ++count)
            {
                char marker = code[header];
                if (marker >= '1' && marker <= '9') explicitIndent = marker - '0';
                else if (marker != '+' && marker != '-') break;
                ++header;
            }
            while (header < code.Length && (code[header] == ' ' || code[header] == '\t')) ++header;
            if (header < code.Length && code[header] != '#' && code[header] != '\n' && code[header] != '\r') return start;
            // Reject malformed headers before looking behind: only one candidate per line can pass.
            int lineStart = start;
            while (lineStart > 0 && code[lineStart - 1] != '\r' && code[lineStart - 1] != '\n') --lineStart;
            int before = start - 1;
            while (before >= lineStart && (code[before] == ' ' || code[before] == '\t')) --before;
            if (before >= lineStart && code[before] != ':' && code[before] != '-') return start;
            int baseIndent = 0;
            while (lineStart + baseIndent < start && code[lineStart + baseIndent] == ' ') ++baseIndent;
            int required = baseIndent + Math.Max(1, explicitIndent);
            bool inferred = explicitIndent > 0;
            int line = CodeNextLine(code, CodeLineEnd(code, header));
            while (line < code.Length)
            {
                int end = CodeLineEnd(code, line), content = line;
                while (content < end && code[content] == ' ') ++content;
                if (content < end)
                {
                    if (content - line < required) break;
                    if (!inferred) { required = content - line; inferred = true; }
                }
                line = CodeNextLine(code, end);
            }
            return line;
        }

        private static int CodeNextLine(string code, int end)
        {
            if (end < code.Length && code[end] == '\r') ++end;
            if (end < code.Length && code[end] == '\n') ++end;
            return end;
        }

        private static int ReadMarkupCode(string code, int start, MNNMarkdownPalette palette, ref bool inTag, out string color)
        {
            color = null;
            if (CodeStartsAt(code, start, "<!--")) { color = palette.Muted; return CodeDelimitedEnd(code, start + 4, "-->"); }
            if (CodeStartsAt(code, start, "<![CDATA[")) { color = palette.SyntaxString; return CodeDelimitedEnd(code, start + 9, "]]>"); }
            char value = code[start];
            if (value == '<' && start + 1 < code.Length && (IsCodeIdentifier(code[start + 1]) || "/!?:".IndexOf(code[start + 1]) >= 0))
            {
                inTag = true;
                color = palette.SyntaxKeyword;
                int end = start + 1;
                while (end < code.Length && (IsMarkupName(code[end]) || "/!?".IndexOf(code[end]) >= 0)) ++end;
                return end;
            }
            if (!inTag) return start + 1;
            if (value == '"' || value == '\'')
            { color = palette.SyntaxString; return ReadQuotedCode(code, start + 1, value, 1, false, true, false); }
            if (value == '>' || (value == '/' || value == '?') && start + 1 < code.Length && code[start + 1] == '>')
            { inTag = false; color = palette.SyntaxKeyword; return start + (value == '>' ? 1 : 2); }
            if (!IsMarkupName(value)) return start + 1;
            int nameEnd = start + 1;
            while (nameEnd < code.Length && IsMarkupName(code[nameEnd])) ++nameEnd;
            if (NextCodeCharacter(code, nameEnd) == '=') color = palette.Link;
            return nameEnd;
        }

        private static bool IsMarkupName(char value) => IsCodeIdentifierPart(value) || value == ':' || value == '-' || value == '.';

        private static bool CodeStartsAt(string code, int start, string text) => start + text.Length <= code.Length &&
            string.CompareOrdinal(code, start, text, 0, text.Length) == 0;

        private static int CodeDelimitedEnd(string code, int start, string delimiter)
        {
            int end = code.IndexOf(delimiter, start, StringComparison.Ordinal);
            return end < 0 ? code.Length : end + delimiter.Length;
        }
    }
}
