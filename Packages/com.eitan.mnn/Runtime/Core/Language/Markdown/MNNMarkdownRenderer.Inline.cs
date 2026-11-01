using System;
using System.Collections.Generic;
using System.Text;

namespace MNN.Unity
{
    public static partial class MNNMarkdownRenderer
    {
        private static readonly char[] InlineMarkers = { '*', '_', '~', '`', '[', '!', '\\', '&', '<', '\n' };
        private static readonly char[] AutolinkWhitespace = { ' ', '\t', '\n', '<' };
        private struct InlinePart
        {
            internal string Text;
            internal Delimiter Marker;
            internal string ImageAlt, ImageUrl;
            internal List<MNNMarkdownImage> NestedImages;
            internal int NestedOffset;
        }

        private sealed class Delimiter
        {
            internal char Character;
            internal int Length, Remaining, Previous, Next;
            internal bool CanOpen, CanClose;
            internal List<string> Opening, Closing;
        }

        private static string FormatInline(string source, int depth, List<MNNMarkdownImage> images, MNNMarkdownPalette palette)
        {
            if (string.IsNullOrEmpty(source)) return string.Empty;
            if (depth > 8) return Escape(source);
            if (source.IndexOfAny(InlineMarkers) < 0) return source;
            Dictionary<int, int> codeCloses = FindCodeClosers(source);
            Dictionary<int, int> brackets = FindBracketPairs(source, codeCloses);
            var parts = new List<InlinePart>();
            var delimiters = new List<Delimiter>();
            var literal = new StringBuilder();
            int nextAngleClose = -1;
            bool noMoreAngleCloses = false;
            for (int i = 0; i < source.Length;)
            {
                char value = source[i];
                if (value == '\\' && i + 1 < source.Length && source[i + 1] == '\n')
                {
                    literal.Append('\n');
                    i += 2;
                    continue;
                }
                if (value == '\n')
                {
                    int end = literal.Length;
                    while (end > 0 && literal[end - 1] == ' ') --end;
                    if (literal.Length - end >= 2) literal.Length = end;
                }
                if (value == '\\' && i + 1 < source.Length && MNNMarkdownSyntax.IsPunctuation(source[i + 1]))
                {
                    AppendEscaped(literal, source[i + 1]);
                    i += 2;
                    continue;
                }
                if (value == '`')
                {
                    int length = RunLength(source, i);
                    if (codeCloses != null && codeCloses.TryGetValue(i, out int close))
                    {
                        string code = source.Substring(i + length, close - i - length).Replace('\n', ' ');
                        if (code.Length >= 2 && code[0] == ' ' && code[code.Length - 1] == ' ' && code.Trim(' ').Length > 0)
                            code = code.Substring(1, code.Length - 2);
                        FlushInline(parts, literal);
                        parts.Add(new InlinePart { Text = "<color=" + palette.Code + ">" + Escape(code) + "</color>" });
                        i = close + length;
                    }
                    else
                    {
                        literal.Append('`', length);
                        i += length;
                    }
                    continue;
                }
                int bracket = value == '!' ? i + 1 : i;
                if ((value == '!' || value == '[') && brackets != null && brackets.TryGetValue(bracket, out int labelEnd) &&
                    MNNMarkdownSyntax.TryReadLink(source, i, out int next, out string label,
                        out string destination, out bool image, labelEnd))
                {
                    FlushInline(parts, literal);
                    if (image)
                    {
                        string alt = MNNMarkdownSyntax.Unescape(label);
                        parts.Add(new InlinePart { Text = ImagePlaceholder(alt, palette),
                            ImageAlt = alt, ImageUrl = images == null ? null : destination });
                    }
                    else
                    {
                        var nestedImages = images != null && label.IndexOf("![", StringComparison.Ordinal) >= 0
                            ? new List<MNNMarkdownImage>() : null;
                        string prefix = "<color=" + palette.Link + ">";
                        string rendered = FormatInline(label, depth + 1, nestedImages, palette);
                        parts.Add(new InlinePart { Text = prefix + rendered + "</color>",
                            NestedImages = nestedImages, NestedOffset = prefix.Length });
                    }
                    i = next;
                    continue;
                }
                if (value == '<' && !noMoreAngleCloses)
                {
                    if (nextAngleClose <= i) nextAngleClose = source.IndexOf('>', i + 1);
                    noMoreAngleCloses = nextAngleClose < 0;
                    int nestedOpen = source.IndexOf('<', i + 1);
                    if (nextAngleClose > i + 1 && (nestedOpen < 0 || nestedOpen > nextAngleClose))
                    {
                        string candidate = source.Substring(i + 1, nextAngleClose - i - 1);
                        if (candidate.IndexOfAny(AutolinkWhitespace) < 0 && MNNMarkdownSyntax.IsSafeLink(candidate))
                        {
                            FlushInline(parts, literal);
                            parts.Add(new InlinePart { Text = "<color=" + palette.Link + ">" + Escape(candidate) + "</color>" });
                            i = nextAngleClose + 1;
                            continue;
                        }
                    }
                }
                if (value == '&' && TryDecodeEntity(source, i, out string entity, out int afterEntity))
                {
                    AppendEscaped(literal, entity);
                    i = afterEntity;
                    continue;
                }
                if (value == '*' || value == '_' || value == '~')
                {
                    int length = RunLength(source, i);
                    if (value == '~' && length < 2) { literal.Append(value); ++i; continue; }
                    FlushInline(parts, literal);
                    Delimiter marker = CreateDelimiter(source, i, length, delimiters.Count);
                    if (delimiters.Count > 0) delimiters[delimiters.Count - 1].Next = delimiters.Count;
                    delimiters.Add(marker);
                    parts.Add(new InlinePart { Marker = marker });
                    i += length;
                    continue;
                }
                AppendEscaped(literal, value);
                ++i;
            }
            FlushInline(parts, literal);
            PairDelimiters(delimiters, palette);
            var output = new StringBuilder(source.Length + 16);
            foreach (InlinePart part in parts)
            {
                if (part.Marker == null)
                {
                    if (part.ImageUrl != null) images.Add(new MNNMarkdownImage(part.ImageAlt, part.ImageUrl,
                        output.Length, part.Text.Length));
                    if (part.NestedImages != null)
                        foreach (var image in part.NestedImages)
                            images.Add(new MNNMarkdownImage(image.Alt, image.Url,
                                output.Length + part.NestedOffset + image.RichTextStart, image.RichTextLength));
                    output.Append(part.Text);
                    continue;
                }
                Delimiter marker = part.Marker;
                if (marker.Closing != null) foreach (string tag in marker.Closing) output.Append(tag);
                output.Append(marker.Character, marker.Remaining);
                if (marker.Opening != null)
                    for (int i = marker.Opening.Count - 1; i >= 0; --i) output.Append(marker.Opening[i]);
            }
            return output.ToString();
        }

        private static void FlushInline(List<InlinePart> parts, StringBuilder literal)
        {
            if (literal.Length == 0) return;
            parts.Add(new InlinePart { Text = literal.ToString() });
            literal.Clear();
        }

        private static Delimiter CreateDelimiter(string source, int start, int length, int index)
        {
            char before = start == 0 ? '\n' : source[start - 1];
            char after = start + length == source.Length ? '\n' : source[start + length];
            bool beforePunctuation = char.IsPunctuation(before) || char.IsSymbol(before);
            bool afterPunctuation = char.IsPunctuation(after) || char.IsSymbol(after);
            bool left = !char.IsWhiteSpace(after) && (!afterPunctuation || char.IsWhiteSpace(before) || beforePunctuation);
            bool right = !char.IsWhiteSpace(before) && (!beforePunctuation || char.IsWhiteSpace(after) || afterPunctuation);
            bool underscore = source[start] == '_';
            return new Delimiter { Character = source[start], Length = length, Remaining = length,
                CanOpen = left && (!underscore || !right || beforePunctuation),
                CanClose = right && (!underscore || !left || afterPunctuation), Previous = index - 1, Next = -1 };
        }

        // Delimiter stack with a lower search bound for each marker/modulo class. Code and
        // links are already opaque parts, so closers cannot consume markers inside them.
        private static void PairDelimiters(List<Delimiter> markers, MNNMarkdownPalette palette)
        {
            var bottoms = new int[18];
            for (int i = 0; i < bottoms.Length; ++i) bottoms[i] = -1;
            int current = markers.Count == 0 ? -1 : 0;
            while (current >= 0)
            {
                Delimiter close = markers[current];
                if (!close.CanClose || close.Character == '~' && close.Remaining < 2)
                { current = close.Next; continue; }
                int kind = close.Character == '*' ? 0 : close.Character == '_' ? 1 : 2;
                int key = kind * 6 + (close.CanOpen ? 3 : 0) + close.Length % 3;
                int opener = close.Previous;
                bool oddMatch = false;
                while (opener >= 0 && opener > bottoms[key])
                {
                    Delimiter open = markers[opener];
                    if (open.Character == close.Character && open.CanOpen &&
                        (open.Character != '~' || open.Remaining >= 2))
                    {
                        bool odd = open.Character != '~' && (close.CanOpen || open.CanClose) &&
                            (open.Length + close.Length) % 3 == 0 && (open.Length % 3 != 0 || close.Length % 3 != 0);
                        if (!odd) break;
                        oddMatch = true;
                    }
                    opener = open.Previous;
                }
                if (opener < 0 || opener <= bottoms[key])
                {
                    if (!oddMatch) bottoms[key] = close.Previous;
                    int next = close.Next;
                    if (!close.CanOpen) RemoveDelimiter(markers, current);
                    current = next;
                    continue;
                }
                Delimiter opening = markers[opener];
                int used = opening.Remaining >= 2 && close.Remaining >= 2 ? 2 : 1;
                if (opening.Opening == null) opening.Opening = new List<string>();
                if (close.Closing == null) close.Closing = new List<string>();
                opening.Opening.Add(close.Character == '~' ? "<color=" + palette.Muted + ">" : used == 2 ? "<b>" : "<i>");
                close.Closing.Add(close.Character == '~' ? "</color>" : used == 2 ? "</b>" : "</i>");
                opening.Remaining -= used;
                close.Remaining -= used;
                int between = opening.Next;
                while (between != current)
                {
                    int next = markers[between].Next;
                    RemoveDelimiter(markers, between);
                    between = next;
                }
                if (opening.Remaining == 0) RemoveDelimiter(markers, opener);
                if (close.Remaining == 0)
                {
                    int next = close.Next;
                    RemoveDelimiter(markers, current);
                    current = next;
                }
            }
        }

        private static void RemoveDelimiter(List<Delimiter> markers, int index)
        {
            Delimiter marker = markers[index];
            if (marker.Previous >= 0) markers[marker.Previous].Next = marker.Next;
            if (marker.Next >= 0) markers[marker.Next].Previous = marker.Previous;
        }

        private static Dictionary<int, int> FindCodeClosers(string source)
        {
            if (source.IndexOf('`') < 0) return null;
            var next = new Dictionary<int, int>();
            var last = new Dictionary<int, int>();
            for (int i = 0; i < source.Length;)
            {
                if (source[i] != '`') { ++i; continue; }
                int length = RunLength(source, i);
                if (last.TryGetValue(length, out int previous)) next[previous] = i;
                last[length] = i;
                i += length;
            }
            return next;
        }

        private static Dictionary<int, int> FindBracketPairs(string source, Dictionary<int, int> codeCloses)
        {
            if (source.IndexOf('[') < 0) return null;
            var pairs = new Dictionary<int, int>();
            var stack = new Stack<int>();
            for (int i = 0; i < source.Length; ++i)
            {
                if (source[i] == '\\' && i + 1 < source.Length && MNNMarkdownSyntax.IsPunctuation(source[i + 1])) { ++i; continue; }
                if (source[i] == '`' && codeCloses != null && codeCloses.TryGetValue(i, out int close))
                { i = close + RunLength(source, i) - 1; continue; }
                if (source[i] == '[') stack.Push(i);
                else if (source[i] == ']' && stack.Count > 0) pairs[stack.Pop()] = i;
            }
            return pairs;
        }

        private static int RunLength(string source, int start)
        {
            int end = start + 1;
            while (end < source.Length && source[end] == source[start]) ++end;
            return end - start;
        }

        private static bool TryDecodeEntity(string source, int start, out string value, out int next)
        {
            value = null;
            next = start;
            int end = source.IndexOf(';', start + 1, Math.Min(32, source.Length - start - 1));
            if (end < 0) return false;
            string encoded = source.Substring(start, end - start + 1);
            value = MNNMarkdownSyntax.DecodeEntities(encoded);
            if (value == encoded) return false;
            next = end + 1;
            return true;
        }

        private static string ImagePlaceholder(string alt, MNNMarkdownPalette palette) => "<color=" + palette.Link + ">[Image" +
            (string.IsNullOrEmpty(alt) ? "" : ": " + Escape(alt)) + "]</color>";
    }
}
