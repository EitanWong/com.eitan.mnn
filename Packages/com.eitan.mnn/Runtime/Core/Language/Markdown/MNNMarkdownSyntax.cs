using System;
using System.Net;
using System.Text;

namespace MNN.Unity
{
    /// <summary>Shared lexical rules for block and inline links; no UI or network operations.</summary>
    internal static class MNNMarkdownSyntax
    {
        internal static bool IsPunctuation(char value) => value >= '!' && value <= '/' ||
            value >= ':' && value <= '@' || value >= '[' && value <= '`' || value >= '{' && value <= '~';

        internal static string Unescape(string text)
        {
            if (text.IndexOf('\\') < 0 && text.IndexOf('&') < 0) return text;
            var output = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; ++i)
            {
                if (text[i] == '\\' && i + 1 < text.Length && IsPunctuation(text[i + 1])) ++i;
                output.Append(text[i]);
            }
            return DecodeEntities(output.ToString());
        }

        internal static string DecodeEntities(string text)
        {
            string decoded = WebUtility.HtmlDecode(text);
            return decoded.IndexOf('\0') >= 0 ? decoded.Replace('\0', '\uFFFD') : decoded;
        }

        internal static bool TryReadLink(string source, int start, out int next, out string label,
            out string destination, out bool image, int labelEndHint = -1)
        {
            next = start;
            label = destination = string.Empty;
            image = source[start] == '!';
            int open = image ? start + 1 : start;
            if (open >= source.Length || source[open] != '[') return false;
            int labelEnd = labelEndHint >= 0 ? labelEndHint : FindLabelEnd(source, open + 1);
            if (labelEnd < 0 || labelEnd + 1 >= source.Length || source[labelEnd + 1] != '(') return false;
            int position = labelEnd + 2;
            SkipWhitespace(source, ref position);
            if (!ReadDestination(source, ref position, out destination)) return false;
            int end = position;
            SkipWhitespace(source, ref position);
            if (position < source.Length && source[position] != ')')
            {
                if (position == end || !ReadTitle(source, ref position)) return false;
                SkipWhitespace(source, ref position);
            }
            if (position >= source.Length || source[position] != ')') return false;
            destination = Unescape(destination);
            if (image ? !MNNMarkdownRenderer.IsSafeImageUrl(destination) : !IsSafeLink(destination)) return false;
            label = source.Substring(open + 1, labelEnd - open - 1);
            next = position + 1;
            return true;
        }

        private static int FindLabelEnd(string source, int start)
        {
            int depth = 0;
            for (int i = start; i < source.Length; ++i)
            {
                if (source[i] == '\\' && i + 1 < source.Length && IsPunctuation(source[i + 1])) { ++i; continue; }
                if (source[i] == '[') ++depth;
                else if (source[i] == ']')
                {
                    if (depth == 0) return i;
                    --depth;
                }
            }
            return -1;
        }

        private static bool ReadDestination(string source, ref int position, out string destination)
        {
            destination = string.Empty;
            if (position >= source.Length) return false;
            if (source[position] == '<')
            {
                int start = ++position;
                while (position < source.Length && source[position] != '>')
                {
                    if (source[position] == '\n' || source[position] == '<') return false;
                    if (source[position] == '\\' && position + 1 < source.Length && IsPunctuation(source[position + 1])) ++position;
                    ++position;
                }
                if (position == source.Length) return false;
                destination = source.Substring(start, position++ - start);
                return true;
            }

            int beginning = position, depth = 0;
            while (position < source.Length)
            {
                char value = source[position];
                if (value == '\\' && position + 1 < source.Length && IsPunctuation(source[position + 1]))
                { position += 2; continue; }
                if (char.IsWhiteSpace(value) || value == ')' && depth == 0) break;
                if (value == '<' || char.IsControl(value)) return false;
                if (value == '(') ++depth;
                if (value == ')') --depth;
                ++position;
            }
            if (depth != 0) return false;
            destination = source.Substring(beginning, position - beginning);
            return true;
        }

        private static bool ReadTitle(string source, ref int position)
        {
            char open = source[position++];
            if (open != '"' && open != '\'' && open != '(') return false;
            char close = open == '(' ? ')' : open;
            while (position < source.Length)
            {
                char value = source[position++];
                if (value == '\\' && position < source.Length && IsPunctuation(source[position])) { ++position; continue; }
                if (value == close) return true;
                if (open == '(' && value == '(') return false;
            }
            return false;
        }

        private static void SkipWhitespace(string source, ref int position)
        {
            while (position < source.Length && char.IsWhiteSpace(source[position])) ++position;
        }

        internal static bool IsSafeLink(string destination)
        {
            if (string.IsNullOrEmpty(destination)) return false;
            if (!Uri.TryCreate(destination, UriKind.Absolute, out Uri uri)) return destination[0] == '#';
            return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps ||
                uri.Scheme == Uri.UriSchemeFile || uri.Scheme == Uri.UriSchemeMailto;
        }
    }
}
