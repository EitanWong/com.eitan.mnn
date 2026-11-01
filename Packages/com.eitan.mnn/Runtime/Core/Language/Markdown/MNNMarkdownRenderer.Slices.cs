using System;
using System.Collections.Generic;
using System.Text;

namespace MNN.Unity
{
    public static partial class MNNMarkdownRenderer
    {
        /// <summary>
        /// Parses the complete source before dividing formatted text into bounded UI slices.
        /// Tags are balanced per slice; images belong to the slice containing their marker.
        /// The limit counts UTF-16 text units, excluding generated formatting tags.
        /// </summary>
        public static IReadOnlyList<MNNMarkdownRenderResult> RenderSlices(string markdown,
            int maxCharacters = 1024, int baseFontSize = 20) =>
            RenderSlices(markdown, maxCharacters, baseFontSize, MNNMarkdownTheme.Dark);

        /// <summary>Renders bounded slices using the caller's surface theme.</summary>
        public static IReadOnlyList<MNNMarkdownRenderResult> RenderSlices(string markdown,
            int maxCharacters, int baseFontSize, MNNMarkdownTheme theme)
        {
            if (maxCharacters < 2) throw new ArgumentOutOfRangeException(nameof(maxCharacters));
            MNNMarkdownRenderResult result = Render(markdown, baseFontSize, theme);
            if (result.RichText.Length == 0) return new[] { result };
            var slices = new List<MNNMarkdownRenderResult>();
            var activeTags = new List<string>();
            string text = result.RichText;
            int position = 0, imageIndex = 0, characterOffset = 0;
            while (position < text.Length)
            {
                int start = position, characters = 0, lines = 0;
                var output = new StringBuilder();
                foreach (string tag in activeTags) output.Append(tag);
                int prefixLength = output.Length;
                while (position < text.Length)
                {
                    bool tag = TryReadRichTag(text, position, out int afterTag, out bool closing);
                    if ((characters >= maxCharacters || lines >= 32) && (!tag || !closing)) break;
                    if (tag)
                    {
                        string value = text.Substring(position, afterTag - position);
                        output.Append(value);
                        if (closing) activeTags.RemoveAt(activeTags.Count - 1);
                        else activeTags.Add(value);
                        position = afterTag;
                        continue;
                    }
                    int length = position + 1 < text.Length &&
                        (char.IsHighSurrogate(text[position]) && char.IsLowSurrogate(text[position + 1]) ||
                         text[position] == '\r' && text[position + 1] == '\n' ||
                         text[position] == '<' && text[position + 1] == '\u200B') ? 2 : 1;
                    if (characters > 0 && characters + length > maxCharacters) break;
                    output.Append(text, position, length);
                    if (text[position + length - 1] == '\n') ++lines;
                    position += length;
                    characters += length;
                }
                for (int i = activeTags.Count - 1; i >= 0; --i) AppendClosingTag(output, activeTags[i]);
                List<MNNMarkdownImage> images = null;
                while (imageIndex < result.Images.Count && result.Images[imageIndex].RichTextStart < position)
                {
                    MNNMarkdownImage image = result.Images[imageIndex++];
                    if (images == null) images = new List<MNNMarkdownImage>();
                    images.Add(new MNNMarkdownImage(image.Alt, image.Url,
                        prefixLength + image.RichTextStart - start,
                        Math.Min(image.RichTextLength, position - image.RichTextStart)));
                }
                slices.Add(new MNNMarkdownRenderResult(result.Document, output.ToString(), images, characterOffset));
                characterOffset += characters;
            }
            return slices;
        }

        private static bool TryReadRichTag(string text, int start, out int next, out bool closing)
        {
            next = start;
            closing = false;
            if (text[start] != '<') return false;
            int name = start + 1;
            if (name < text.Length && text[name] == '/') { closing = true; ++name; }
            if (name >= text.Length) return false;
            // Literal input always has a zero-width separator after '<'.
            if (text[name] != 'b' && text[name] != 'i' && text[name] != 'c' && text[name] != 's') return false;
            int end = text.IndexOf('>', name);
            if (end < 0) return false;
            string value = text.Substring(name, end - name);
            if (value != "b" && value != "i" && value != "color" && value != "size" &&
                !value.StartsWith("color=", StringComparison.Ordinal) && !value.StartsWith("size=", StringComparison.Ordinal))
                return false;
            next = end + 1;
            return true;
        }

        private static void AppendClosingTag(StringBuilder output, string open)
        {
            int end = open.IndexOf('=');
            if (end < 0) end = open.Length - 1;
            output.Append("</").Append(open, 1, end - 1).Append('>');
        }
    }
}
