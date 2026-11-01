using System;
using System.Globalization;
using System.Text;

namespace MNN.Unity
{
    public static partial class MNNMarkdownRenderer
    {
        /// <summary>Escapes literal code and applies lexical colors for a supported fence language. No Markdown is interpreted.</summary>
        public static string ToCodeRichText(string code, string language, MNNMarkdownTheme theme = MNNMarkdownTheme.Dark)
        {
            if (code == null) throw new ArgumentNullException(nameof(code));
            var output = new StringBuilder(code.Length + 32);
            AppendCode(output, code.Replace('\0', '\uFFFD'), language, MNNMarkdownPalette.For(theme));
            return output.ToString();
        }

        private static void AppendCode(StringBuilder output, string code, string info, MNNMarkdownPalette palette)
        {
            output.Append("<color=").Append(palette.Code).Append('>');
            var language = MNNMarkdownCodeLanguage.Find(info);
            int plainStart = 0;
            bool markupTag = false;
            if (language != null)
            {
                for (int position = 0; position < code.Length;)
                {
                    int end = ReadCodeToken(code, position, language, palette, ref markupTag, out string color);
                    if (color != null)
                    {
                        AppendCodeRange(output, code, plainStart, position);
                        output.Append("<color=").Append(color).Append('>');
                        AppendCodeRange(output, code, position, end);
                        output.Append("</color>");
                        plainStart = end;
                    }
                    position = end;
                }
            }
            AppendCodeRange(output, code, plainStart, code.Length);
            output.Append("</color>");
        }

        private static void AppendCodeRange(StringBuilder output, string source, int start, int end)
        {
            for (int i = start; i < end; ++i) AppendEscaped(output, source[i]);
        }

        private static int ReadCodeToken(string code, int start, MNNMarkdownCodeLanguage language,
            MNNMarkdownPalette palette, ref bool markupTag, out string color)
        {
            var kind = language.Kind;
            if (kind == MNNMarkdownCodeKind.Markup)
                return ReadMarkupCode(code, start, palette, ref markupTag, out color);
            int end = ReadCodeComment(code, start, language);
            if (end > start) { color = palette.Muted; return end; }
            end = ReadCodeString(code, start, language);
            if (end > start)
            {
                color = kind == MNNMarkdownCodeKind.Json && NextCodeCharacter(code, end) == ':'
                    ? palette.Link : palette.SyntaxString;
                return end;
            }
            char value = code[start];
            end = ReadLanguageCodeToken(code, start, language, palette, out color);
            if (end > start) return end;
            if ((kind == MNNMarkdownCodeKind.CSharp || kind == MNNMarkdownCodeKind.Cpp) &&
                value == '#' && IsCodeLineStart(code, start))
            {
                color = palette.SyntaxKeyword;
                return CodeLineEnd(code, start);
            }
            if (IsCodeNumberStart(code, start, kind))
            {
                color = palette.SyntaxNumber;
                return ReadCodeNumber(code, start, language);
            }
            if (IsCodeIdentifier(value) || value == '$' || value == '@' && kind == MNNMarkdownCodeKind.CSharp ||
                value == '.' && kind == MNNMarkdownCodeKind.R ||
                value == '-' && (kind == MNNMarkdownCodeKind.Css || kind == MNNMarkdownCodeKind.Yaml || kind == MNNMarkdownCodeKind.Toml))
            {
                end = start + 1;
                while (end < code.Length && IsLanguageIdentifierPart(code[end], kind)) ++end;
                color = IsCodeProperty(code, end, kind) ? palette.Link :
                    language.IsKeyword(code, start, end - start) ? palette.SyntaxKeyword : null;
                return end;
            }
            color = null;
            return start + 1;
        }

        private static int ReadCodeComment(string code, int start, MNNMarkdownCodeLanguage language)
        {
            var kind = language.Kind;
            char value = code[start];
            if (kind == MNNMarkdownCodeKind.Ruby && value == '=')
            {
                int ruby = ReadRubyBlockComment(code, start);
                if (ruby > start) return ruby;
            }
            bool hash = language.Has(MNNMarkdownCodeFeatures.HashComments) && value == '#';
            if (hash && language.Has(MNNMarkdownCodeFeatures.HashNeedsSpace) && start > 0 &&
                !char.IsWhiteSpace(code[start - 1]) &&
                !(kind == MNNMarkdownCodeKind.Shell && ";|&(){}".IndexOf(code[start - 1]) >= 0)) hash = false;
            bool dash = language.Has(MNNMarkdownCodeFeatures.DashComments);
            bool slash = language.Has(MNNMarkdownCodeFeatures.SlashComments);
            if (dash && value == '-' && start + 1 < code.Length && code[start + 1] == '-' && kind == MNNMarkdownCodeKind.Lua)
            {
                int lua = ReadLuaLongCode(code, start + 2);
                if (lua > start + 2) return lua;
            }
            if (hash || start + 1 < code.Length && (dash && value == '-' && code[start + 1] == '-' ||
                slash && value == '/' && code[start + 1] == '/')) return CodeLineEnd(code, start);
            if (language.Has(MNNMarkdownCodeFeatures.BlockComments) && value == '/' && start + 1 < code.Length && code[start + 1] == '*')
                return ReadBlockCodeComment(code, start, language.Has(MNNMarkdownCodeFeatures.NestedComments));
            return start;
        }

        private static int ReadCodeString(string code, int start, MNNMarkdownCodeLanguage language)
        {
            var kind = language.Kind;
            int special = ReadSpecialCodeString(code, start, kind);
            if (special > start) return special;
            int quoteAt = start;
            bool verbatim = false, raw = false;
            if (kind == MNNMarkdownCodeKind.CSharp)
            {
                while (quoteAt < code.Length && code[quoteAt] == '$') ++quoteAt;
                if (quoteAt < code.Length && code[quoteAt] == '@') { verbatim = true; ++quoteAt; }
                if (quoteAt < code.Length && code[quoteAt] == '$') ++quoteAt;
            }
            else if (kind == MNNMarkdownCodeKind.Python)
            {
                while (quoteAt < Math.Min(code.Length, start + 2) && "rRbBuUfF".IndexOf(code[quoteAt]) >= 0) ++quoteAt;
            }
            else if (kind == MNNMarkdownCodeKind.Cpp)
            {
                if (code[quoteAt] == 'u' && quoteAt + 1 < code.Length && code[quoteAt + 1] == '8') quoteAt += 2;
                else if (code[quoteAt] == 'u' || code[quoteAt] == 'U' || code[quoteAt] == 'L') ++quoteAt;
                int rawEnd = ReadCppRawString(code, quoteAt);
                if (rawEnd > quoteAt) return rawEnd;
            }
            else if (kind == MNNMarkdownCodeKind.Dart && code[start] == 'r') { ++quoteAt; raw = true; }
            if (quoteAt >= code.Length) return start;
            char quote = code[quoteAt];
            if (kind == MNNMarkdownCodeKind.Yaml && start > 0 && !char.IsWhiteSpace(code[start - 1]) &&
                "[{,:".IndexOf(code[start - 1]) < 0) return start;
            bool backtick = quote == '`' && language.Has(MNNMarkdownCodeFeatures.Backticks);
            if (quote != '"' && quote != '\'' && !backtick || language.Has(MNNMarkdownCodeFeatures.DoubleQuoteOnly) && quote != '"') return start;
            if (kind == MNNMarkdownCodeKind.Rust && quote == '\'') return start; // A lifetime is not an unfinished character literal.
            if (quoteAt > start && kind == MNNMarkdownCodeKind.CSharp && quote != '"') return start;
            int delimiter = 1;
            if (language.Has(MNNMarkdownCodeFeatures.TripleQuotes) && quoteAt + 2 < code.Length &&
                code[quoteAt + 1] == quote && code[quoteAt + 2] == quote) delimiter = 3;
            if (kind == MNNMarkdownCodeKind.CSharp && !verbatim && quote == '"')
            {
                int openingRun = CodeRun(code, quoteAt, quote);
                if (openingRun >= 3) delimiter = openingRun;
            }
            bool doubled = verbatim || language.Has(MNNMarkdownCodeFeatures.DoubledQuotes) &&
                (kind != MNNMarkdownCodeKind.Yaml || quote == '\'');
            bool multiline = delimiter > 1 || verbatim || backtick || language.Has(MNNMarkdownCodeFeatures.MultilineStrings);
            bool escapes = !verbatim && !raw && !doubled &&
                !(language.Has(MNNMarkdownCodeFeatures.LiteralSingleQuote) && quote == '\'') &&
                !(language.Has(MNNMarkdownCodeFeatures.LiteralBackticks) && backtick) &&
                !(language.Has(MNNMarkdownCodeFeatures.RawTripleQuotes) && delimiter > 1) &&
                !(kind == MNNMarkdownCodeKind.CSharp && delimiter > 1);
            int end = ReadQuotedCode(code, quoteAt + delimiter, quote, delimiter, doubled, multiline, escapes);
            // TOML allows one or two content quotes immediately before its triple-quote closer.
            if (kind == MNNMarkdownCodeKind.Toml && delimiter == 3) end += CodeMarkerCount(code, end, quote, 2);
            return end;
        }

        private static int ReadQuotedCode(string code, int position, char quote, int delimiter,
            bool doubled, bool multiline, bool escapes)
        {
            while (position < code.Length)
            {
                char value = code[position];
                if (!multiline && (value == '\n' || value == '\r')) return position;
                if (escapes && value == '\\' && position + 1 < code.Length)
                { position = CodeEscapeEnd(code, position + 1); continue; }
                if (value == quote)
                {
                    if (doubled && position + 1 < code.Length && code[position + 1] == quote)
                    { position += 2; continue; }
                    int run = 1;
                    while (run < delimiter && position + run < code.Length && code[position + run] == quote) ++run;
                    if (run == delimiter) return position + delimiter;
                    position += run;
                }
                else ++position;
            }
            return position;
        }

        private static int ReadCppRawString(string code, int start)
        {
            if (start + 1 >= code.Length || code[start] != 'R' || code[start + 1] != '"') return start;
            int open = start + 2;
            while (open < code.Length && open - start <= 18 && code[open] != '(' &&
                !char.IsWhiteSpace(code[open]) && code[open] != '\\' && code[open] != ')') ++open;
            if (open >= code.Length || code[open] != '(' || open - start > 18) return start;
            int length = open - start - 2;
            for (int i = open + 1; i < code.Length; ++i)
                if (code[i] == ')' && i + length + 1 < code.Length && code[i + length + 1] == '"' &&
                    string.CompareOrdinal(code, start + 2, code, i + 1, length) == 0) return i + length + 2;
            return code.Length;
        }

        private static int ReadCodeNumber(string code, int start, MNNMarkdownCodeLanguage language)
        {
            var kind = language.Kind;
            int position = start;
            if (code[position] == '-' || code[position] == '+') ++position;
            bool hexadecimal = position + 1 < code.Length && code[position] == '0' && (code[position + 1] == 'x' || code[position + 1] == 'X');
            bool binary = position + 1 < code.Length && code[position] == '0' && (code[position + 1] == 'b' || code[position + 1] == 'B');
            bool octal = position + 1 < code.Length && code[position] == '0' && (code[position + 1] == 'o' || code[position + 1] == 'O');
            if (hexadecimal || binary || octal)
            {
                position += 2;
                position = ReadCodeBaseDigits(code, position, kind, hexadecimal ? 16 : binary ? 2 : 8);
                if (hexadecimal && language.Has(MNNMarkdownCodeFeatures.HexFloat))
                {
                    if (position + 1 < code.Length && code[position] == '.' && IsCodeBaseDigit(code[position + 1], 16))
                        position = ReadCodeBaseDigits(code, position + 1, kind, 16);
                    position = ReadCodeExponent(code, position, 'p', kind);
                }
            }
            else
            {
                position = ReadCodeDigits(code, position, kind);
                if (position < code.Length && code[position] == '.' && position + 1 < code.Length && IsCodeDigit(code[position + 1]))
                    position = ReadCodeDigits(code, position + 1, kind);
                position = ReadCodeExponent(code, position, 'e', kind);
            }
            if (kind == MNNMarkdownCodeKind.Rust) return ReadRustNumberSuffix(code, position);
            if (kind == MNNMarkdownCodeKind.Css)
            {
                while (position < code.Length && (char.IsLetter(code[position]) || code[position] == '%')) ++position;
            }
            else if (kind == MNNMarkdownCodeKind.CSharp || kind == MNNMarkdownCodeKind.Cpp ||
                kind == MNNMarkdownCodeKind.Java || kind == MNNMarkdownCodeKind.Kotlin)
                while (position < code.Length && "uUlLfFdDmM".IndexOf(code[position]) >= 0) ++position;
            else if (position < code.Length && (kind == MNNMarkdownCodeKind.Python && (code[position] == 'j' || code[position] == 'J') ||
                kind == MNNMarkdownCodeKind.JavaScript && code[position] == 'n' ||
                (kind == MNNMarkdownCodeKind.Go || kind == MNNMarkdownCodeKind.R) && code[position] == 'i' ||
                kind == MNNMarkdownCodeKind.R && code[position] == 'L')) ++position;
            return position;
        }

        private static bool IsCodeNumberStart(string code, int start, MNNMarkdownCodeKind kind)
        {
            int position = start;
            bool signed = kind == MNNMarkdownCodeKind.Css || kind == MNNMarkdownCodeKind.Yaml || kind == MNNMarkdownCodeKind.Toml;
            if ((signed || kind == MNNMarkdownCodeKind.Json && code[position] == '-') &&
                (code[position] == '-' || code[position] == '+')) ++position;
            if (position >= code.Length) return false;
            return IsCodeDigit(code[position]) || code[position] == '.' && (position == 0 || code[position - 1] != '.') &&
                position + 1 < code.Length && IsCodeDigit(code[position + 1]);
        }

        private static int ReadCodeExponent(string code, int position, char marker, MNNMarkdownCodeKind kind)
        {
            if (position >= code.Length || char.ToLowerInvariant(code[position]) != marker) return position;
            int exponent = position + 1;
            if (exponent < code.Length && (code[exponent] == '+' || code[exponent] == '-')) ++exponent;
            return exponent < code.Length && IsCodeDigit(code[exponent]) ? ReadCodeDigits(code, exponent, kind) : position;
        }

        private static int ReadCodeBaseDigits(string code, int position, MNNMarkdownCodeKind kind, int numberBase)
        {
            while (position < code.Length && (code[position] == '_' || IsCodeBaseDigit(code[position], numberBase) ||
                IsCodeDigitSeparator(code, position, kind, numberBase == 16))) ++position;
            return position;
        }

        private static bool IsCodeBaseDigit(char value, int numberBase) =>
            value >= '0' && value <= '9' && value - '0' < numberBase ||
            numberBase == 16 && "abcdefABCDEF".IndexOf(value) >= 0;

        private static int ReadRustNumberSuffix(string code, int position)
        {
            int end = position;
            while (end < code.Length && IsCodeIdentifierPart(code[end])) ++end;
            int length = end - position;
            if (length == 0) return position;
            // No per-number array or substring allocation; a suffix must fill the remaining identifier.
            const string suffixes = "|u8|u16|u32|u64|u128|usize|i8|i16|i32|i64|i128|isize|f32|f64|";
            for (int i = 1; i + length < suffixes.Length; ++i)
                if (suffixes[i - 1] == '|' && suffixes[i + length] == '|' &&
                    string.CompareOrdinal(code, position, suffixes, i, length) == 0) return end;
            return position;
        }

        private static int ReadCodeDigits(string code, int position, MNNMarkdownCodeKind kind)
        {
            while (position < code.Length && (IsCodeDigit(code[position]) || code[position] == '_' ||
                IsCodeDigitSeparator(code, position, kind, false))) ++position;
            return position;
        }

        private static bool IsCodeDigitSeparator(string code, int position, MNNMarkdownCodeKind kind, bool hexadecimal)
        {
            if (kind != MNNMarkdownCodeKind.Cpp || code[position] != '\'' || position == 0 || position + 1 >= code.Length) return false;
            char before = code[position - 1], after = code[position + 1];
            return (IsCodeDigit(before) || hexadecimal && "abcdefABCDEF".IndexOf(before) >= 0) &&
                (IsCodeDigit(after) || hexadecimal && "abcdefABCDEF".IndexOf(after) >= 0);
        }

        private static bool IsCodeIdentifier(char value) => value == '_' || char.IsLetter(value);
        private static bool IsCodeDigit(char value) => value >= '0' && value <= '9';

        private static bool IsCodeIdentifierPart(char value)
        {
            if (IsCodeIdentifier(value) || char.IsDigit(value) || value == '$') return true;
            if (value < 128) return false;
            var category = char.GetUnicodeCategory(value);
            return category == UnicodeCategory.NonSpacingMark || category == UnicodeCategory.SpacingCombiningMark ||
                category == UnicodeCategory.ConnectorPunctuation || category == UnicodeCategory.Format;
        }

        private static bool IsLanguageIdentifierPart(char value, MNNMarkdownCodeKind kind) => IsCodeIdentifierPart(value) ||
            value == '.' && kind == MNNMarkdownCodeKind.R || value == '-' &&
            (kind == MNNMarkdownCodeKind.Css || kind == MNNMarkdownCodeKind.Yaml || kind == MNNMarkdownCodeKind.Toml);

        private static bool IsCodeProperty(string code, int end, MNNMarkdownCodeKind kind)
        {
            if (kind == MNNMarkdownCodeKind.Toml) return NextCodeCharacter(code, end) == '=';
            if (kind == MNNMarkdownCodeKind.Css) return NextCodeCharacter(code, end) == ':';
            if (kind != MNNMarkdownCodeKind.Yaml) return false;
            while (end < code.Length && (code[end] == ' ' || code[end] == '\t')) ++end;
            return end < code.Length && code[end] == ':' &&
                (end + 1 == code.Length || char.IsWhiteSpace(code[end + 1]) || "[]{},".IndexOf(code[end + 1]) >= 0);
        }

        private static char NextCodeCharacter(string code, int position)
        {
            while (position < code.Length && char.IsWhiteSpace(code[position])) ++position;
            return position < code.Length ? code[position] : '\0';
        }

        private static bool IsCodeLineStart(string code, int position)
        {
            while (--position >= 0 && code[position] != '\n' && code[position] != '\r')
                if (!char.IsWhiteSpace(code[position])) return false;
            return true;
        }

        private static int CodeLineEnd(string code, int position)
        {
            while (position < code.Length && code[position] != '\n' && code[position] != '\r') ++position;
            return position;
        }

        private static int CodeRun(string code, int start, char value)
        {
            int end = start + 1;
            while (end < code.Length && code[end] == value) ++end;
            return end - start;
        }
    }
}
