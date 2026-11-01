using System;
using System.Collections.Generic;
using System.Text;

namespace MNN.Unity
{
    /// <summary>
    /// Pure C# Markdown document. Parsing is deliberately separate from Unity UI and MNN inference,
    /// so the same AST is usable by Editor previews, uGUI and non-visual tooling.
    /// </summary>
    public sealed class MNNMarkdownDocument
    {
        private readonly List<MNNMarkdownBlock> _blocks;

        public IReadOnlyList<MNNMarkdownBlock> Blocks => _blocks;

        private MNNMarkdownDocument(List<MNNMarkdownBlock> blocks)
        {
            _blocks = blocks;
        }

        public static MNNMarkdownDocument Parse(string source)
        {
            string input = source ?? string.Empty;
            if (input.IndexOf('\0') >= 0) input = input.Replace('\0', '\uFFFD');
            if (input.Length == 0) return new MNNMarkdownDocument(new List<MNNMarkdownBlock>());
            if (string.IsNullOrWhiteSpace(input)) return new MNNMarkdownDocument(new List<MNNMarkdownBlock>());
            if (input.IndexOf('\r') < 0 && input.IndexOf('\n') < 0 && !LooksLikeBlockStart(input))
            {
                var singleLine = new List<MNNMarkdownBlock>(1)
                {
                    new MNNMarkdownBlock(MNNMarkdownBlockType.Paragraph, input)
                };
                return new MNNMarkdownDocument(singleLine);
            }

            string normalized = input.Replace("\r\n", "\n").Replace('\r', '\n');
            string[] lines = normalized.Split('\n');
            var blocks = new List<MNNMarkdownBlock>();
            var paragraph = new StringBuilder();
            int index = 0;

            while (index < lines.Length)
            {
                string line = lines[index];
                if (string.IsNullOrWhiteSpace(line))
                {
                    FlushParagraph(blocks, paragraph);
                    ++index;
                    continue;
                }

                if (TryReadFence(line, out string fence, out string language))
                {
                    int openingIndent = Math.Min(3, LeadingSpaces(line));
                    FlushParagraph(blocks, paragraph);
                    ++index;
                    var code = new StringBuilder();
                    bool firstCodeLine = true;
                    bool closed = false;
                    while (index < lines.Length)
                    {
                        if (IsClosingFence(lines[index], fence))
                        {
                            closed = true;
                            ++index;
                            break;
                        }

                        if (!firstCodeLine) code.Append('\n');
                        firstCodeLine = false;
                        string content = lines[index++];
                        code.Append(content, Math.Min(openingIndent, LeadingSpaces(content)),
                            content.Length - Math.Min(openingIndent, LeadingSpaces(content)));
                    }

                    blocks.Add(new MNNMarkdownBlock(MNNMarkdownBlockType.Code, code.ToString(), language: language));
                    if (!closed && index >= lines.Length) break;
                    continue;
                }

                if (paragraph.Length > 0 && TryReadSetext(line, out int setextLevel))
                {
                    blocks.Add(new MNNMarkdownBlock(MNNMarkdownBlockType.Heading,
                        paragraph.ToString().TrimEnd(), setextLevel));
                    paragraph.Clear();
                    ++index;
                    continue;
                }

                if (paragraph.Length == 0 && IsIndentedCode(line))
                {
                    var code = new StringBuilder();
                    bool first = true;
                    while (index < lines.Length && (IsIndentedCode(lines[index]) || string.IsNullOrWhiteSpace(lines[index])))
                    {
                        if (!first) code.Append('\n');
                        first = false;
                        string current = lines[index++];
                        code.Append(current.Length > 0 && current[0] == '\t' ? current.Substring(1) :
                            current.Length >= 4 ? current.Substring(4) : string.Empty);
                    }
                    blocks.Add(new MNNMarkdownBlock(MNNMarkdownBlockType.Code, code.ToString().TrimEnd('\n')));
                    continue;
                }

                if (TryReadHeading(line, out int level, out string heading))
                {
                    FlushParagraph(blocks, paragraph);
                    blocks.Add(new MNNMarkdownBlock(MNNMarkdownBlockType.Heading, heading, level));
                    ++index;
                    continue;
                }

                if (IsThematicBreak(line))
                {
                    FlushParagraph(blocks, paragraph);
                    blocks.Add(new MNNMarkdownBlock(MNNMarkdownBlockType.ThematicBreak));
                    ++index;
                    continue;
                }

                if (TryReadImage(line, out string alt, out string url))
                {
                    FlushParagraph(blocks, paragraph);
                    blocks.Add(new MNNMarkdownBlock(MNNMarkdownBlockType.Image,
                        imageAlt: alt, imageUrl: url));
                    ++index;
                    continue;
                }

                if (TryReadQuote(line, out string quote))
                {
                    FlushParagraph(blocks, paragraph);
                    var quoteLines = new StringBuilder(quote);
                    ++index;
                    while (index < lines.Length && TryReadQuote(lines[index], out string nextQuote))
                    {
                        quoteLines.Append('\n').Append(nextQuote);
                        ++index;
                    }

                    blocks.Add(new MNNMarkdownBlock(MNNMarkdownBlockType.Quote, quoteLines.ToString()));
                    continue;
                }

                if (TryReadListItem(line, out bool ordered, out int listLevel, out string item, out int startNumber))
                {
                    FlushParagraph(blocks, paragraph);
                    var items = new List<MNNMarkdownListItem>();
                    var itemText = new StringBuilder(item);
                    ++index;
                    while (index < lines.Length)
                    {
                        if (TryReadListItem(lines[index], out bool nextOrdered, out int nextLevel,
                            out string nextItem, out _) && nextOrdered == ordered && !IsThematicBreak(lines[index]))
                        {
                            items.Add(new MNNMarkdownListItem(itemText.ToString(), listLevel));
                            itemText.Clear().Append(nextItem);
                            listLevel = nextLevel;
                            ++index;
                        }
                        else if (!string.IsNullOrWhiteSpace(lines[index]) &&
                            LeadingSpaces(lines[index]) >= listLevel * 2 + 2 && !IsBlockStart(lines[index]))
                            itemText.Append('\n').Append(lines[index++].Substring(listLevel * 2 + 2));
                        else break;
                    }
                    items.Add(new MNNMarkdownListItem(itemText.ToString(), listLevel));
                    blocks.Add(new MNNMarkdownBlock(ordered ? MNNMarkdownBlockType.OrderedList :
                        MNNMarkdownBlockType.UnorderedList, items: items, orderedStart: startNumber));
                    continue;
                }

                if (paragraph.Length > 0) paragraph.Append('\n');
                paragraph.Append(line);
                ++index;
                while (index < lines.Length && !string.IsNullOrWhiteSpace(lines[index]) &&
                    !IsBlockStart(lines[index]))
                {
                    paragraph.Append('\n').Append(lines[index++]);
                }
            }

            FlushParagraph(blocks, paragraph);
            return new MNNMarkdownDocument(blocks);
        }

        private static bool LooksLikeBlockStart(string line)
        {
            if (IsIndentedCode(line)) return true;
            int index = 0;
            while (index < line.Length && index < 3 && line[index] == ' ') ++index;
            if (index >= line.Length) return false;
            char first = line[index];
            return first == '#' || first == '>' || first == '`' || first == '~' || first == '!' ||
                first == '-' || first == '+' || first == '*' || first == '_' || char.IsDigit(first);
        }

        private static void FlushParagraph(List<MNNMarkdownBlock> blocks, StringBuilder paragraph)
        {
            if (paragraph.Length == 0) return;
            blocks.Add(new MNNMarkdownBlock(MNNMarkdownBlockType.Paragraph, paragraph.ToString().TrimEnd()));
            paragraph.Clear();
        }

        private static bool IsBlockStart(string line)
        {
            return TryReadFence(line, out _, out _) || TryReadHeading(line, out _, out _) ||
                IsThematicBreak(line) || TryReadImage(line, out _, out _) || TryReadQuote(line, out _) ||
                TryReadSetext(line, out _) ||
                TryReadListItem(line, out bool ordered, out _, out string item, out int start) &&
                    item.Length > 0 && (!ordered || start == 1);
        }

        private static bool TryReadFence(string line, out string fence, out string language)
        {
            string trimmed = TrimUpToThreeSpaces(line);
            fence = null;
            language = string.Empty;
            if (trimmed.Length < 3 || trimmed[0] != '`' && trimmed[0] != '~') return false;
            int length = 1;
            while (length < trimmed.Length && trimmed[length] == trimmed[0]) ++length;
            if (length < 3) return false;
            language = trimmed.Substring(length).Trim();
            if (trimmed[0] == '`' && language.IndexOf('`') >= 0) return false;
            fence = trimmed.Substring(0, length);
            return true;
        }

        private static bool IsClosingFence(string line, string fence)
        {
            string trimmed = TrimUpToThreeSpaces(line);
            int length = 0;
            while (length < trimmed.Length && trimmed[length] == fence[0]) ++length;
            return length >= fence.Length && string.IsNullOrWhiteSpace(trimmed.Substring(length));
        }

        private static bool TryReadHeading(string line, out int level, out string text)
        {
            string trimmed = TrimUpToThreeSpaces(line);
            level = 0;
            text = string.Empty;
            while (level < trimmed.Length && level < 6 && trimmed[level] == '#') ++level;
            if (level == 0 || level < trimmed.Length && trimmed[level] != ' ' && trimmed[level] != '\t')
            {
                level = 0;
                return false;
            }

            text = trimmed.Substring(level).Trim();
            int end = text.Length;
            while (end > 0 && text[end - 1] == '#') --end;
            if (end < text.Length && (end == 0 || char.IsWhiteSpace(text[end - 1])))
                text = text.Substring(0, end).TrimEnd();
            return true;
        }

        private static bool IsThematicBreak(string line)
        {
            if (IsIndentedCode(line)) return false;
            int start = 0;
            while (start < line.Length && start < 3 && line[start] == ' ') ++start;
            if (start >= line.Length) return false;
            char marker = line[start];
            if (marker != '*' && marker != '-' && marker != '_') return false;
            int count = 0;
            for (int i = start; i < line.Length; ++i)
            {
                char value = line[i];
                if (value == ' ' || value == '\t') continue;
                if (value != marker) return false;
                ++count;
            }
            return count >= 3;
        }

        private static bool TryReadQuote(string line, out string text)
        {
            string trimmed = TrimUpToThreeSpaces(line);
            if (!trimmed.StartsWith(">", StringComparison.Ordinal))
            {
                text = string.Empty;
                return false;
            }

            text = trimmed.Length > 1 && (trimmed[1] == ' ' || trimmed[1] == '\t')
                ? trimmed.Substring(2) : trimmed.Substring(1);
            return true;
        }

        private static bool TryReadListItem(string line, out bool ordered, out int level, out string text,
            out int startNumber)
        {
            int spaces = 0;
            while (spaces < line.Length && spaces < 12 && line[spaces] == ' ') ++spaces;
            string trimmed = line.Substring(spaces);
            level = spaces / 2;
            ordered = false;
            startNumber = 1;
            text = string.Empty;
            int markerLength = 0;
            if (trimmed.Length >= 1 && (trimmed[0] == '-' || trimmed[0] == '*' || trimmed[0] == '+') &&
                (trimmed.Length == 1 || trimmed[1] == ' ' || trimmed[1] == '\t')) markerLength = 1;
            else
            {
                int digit = 0;
                while (digit < trimmed.Length && trimmed[digit] >= '0' && trimmed[digit] <= '9') ++digit;
                if (digit > 0 && digit <= 9 && digit < trimmed.Length &&
                    (trimmed[digit] == '.' || trimmed[digit] == ')') &&
                    (digit + 1 == trimmed.Length || trimmed[digit + 1] == ' ' || trimmed[digit + 1] == '\t'))
                {
                    ordered = true;
                    markerLength = digit + 1;
                    startNumber = int.Parse(trimmed.Substring(0, digit), System.Globalization.CultureInfo.InvariantCulture);
                }
            }

            if (markerLength == 0) return false;
            text = trimmed.Substring(markerLength).TrimStart(' ', '\t');
            return true;
        }

        private static bool TryReadImage(string line, out string alt, out string url)
        {
            string trimmed = TrimUpToThreeSpaces(line).Trim();
            alt = url = string.Empty;
            if (!trimmed.StartsWith("![", StringComparison.Ordinal) ||
                !MNNMarkdownSyntax.TryReadLink(trimmed, 0, out int next, out string label, out url, out _) ||
                next != trimmed.Length) return false;
            alt = MNNMarkdownSyntax.Unescape(label);
            return true;
        }

        private static bool IsIndentedCode(string line) => line.StartsWith("    ", StringComparison.Ordinal) ||
            line.Length > 0 && line[0] == '\t';

        private static int LeadingSpaces(string line)
        {
            int count = 0;
            while (count < line.Length && line[count] == ' ') ++count;
            return count;
        }

        private static bool TryReadSetext(string line, out int level)
        {
            level = 0;
            if (IsIndentedCode(line)) return false;
            string trimmed = TrimUpToThreeSpaces(line).TrimEnd();
            if (trimmed.Length == 0 || trimmed[0] != '=' && trimmed[0] != '-') return false;
            for (int i = 1; i < trimmed.Length; ++i) if (trimmed[i] != trimmed[0]) return false;
            level = trimmed[0] == '=' ? 1 : 2;
            return true;
        }

        private static string TrimUpToThreeSpaces(string line)
        {
            int count = 0;
            while (count < line.Length && count < 3 && line[count] == ' ') ++count;
            return line.Substring(count);
        }
    }

}
