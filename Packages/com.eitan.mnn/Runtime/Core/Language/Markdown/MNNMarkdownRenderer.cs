using System;
using System.Collections.Generic;
using System.Text;

namespace MNN.Unity
{
    /// <summary>Converts Markdown inline syntax into Unity's safe subset of rich text.</summary>
    public static partial class MNNMarkdownRenderer
    {
        public static MNNMarkdownRenderResult Render(string markdown) => Render(MNNMarkdownDocument.Parse(markdown));

        public static MNNMarkdownRenderResult Render(string markdown, int baseFontSize) =>
            Render(MNNMarkdownDocument.Parse(markdown), baseFontSize);

        public static MNNMarkdownRenderResult Render(MNNMarkdownDocument document) => Render(document, 20);

        public static MNNMarkdownRenderResult Render(string markdown, int baseFontSize, MNNMarkdownTheme theme) =>
            Render(MNNMarkdownDocument.Parse(markdown), baseFontSize, theme);

        public static MNNMarkdownRenderResult Render(MNNMarkdownDocument document, int baseFontSize) =>
            Render(document, baseFontSize, MNNMarkdownTheme.Dark);

        public static MNNMarkdownRenderResult Render(MNNMarkdownDocument document, int baseFontSize, MNNMarkdownTheme theme)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (baseFontSize <= 0) throw new ArgumentOutOfRangeException(nameof(baseFontSize));
            MNNMarkdownPalette palette = MNNMarkdownPalette.For(theme);
            var images = HasImageMarker(document) ? new List<MNNMarkdownImage>() : null;
            var output = new StringBuilder();
            for (int i = 0; i < document.Blocks.Count; ++i)
            {
                if (i > 0) output.Append("\n\n");
                AppendBlock(output, document.Blocks[i], palette, images, baseFontSize);
            }
            return new MNNMarkdownRenderResult(document, output.ToString(), images);
        }

        private static bool HasImageMarker(MNNMarkdownDocument document)
        {
            for (int i = 0; i < document.Blocks.Count; ++i)
            {
                var block = document.Blocks[i];
                if (block.Type == MNNMarkdownBlockType.Image ||
                    block.Text.IndexOf("![", StringComparison.Ordinal) >= 0) return true;
                for (int j = 0; j < block.Items.Count; ++j)
                    if (block.Items[j].Text.IndexOf("![", StringComparison.Ordinal) >= 0) return true;
            }
            return false;
        }

        public static string ToRichText(string markdown)
        {
            return Render(markdown).RichText;
        }

        public static string ToRichText(string markdown, MNNMarkdownTheme theme) => Render(markdown, 20, theme).RichText;

        public static string ToRichText(MNNMarkdownBlock block) => ToRichText(block, MNNMarkdownTheme.Dark);

        public static string ToRichText(MNNMarkdownBlock block, MNNMarkdownTheme theme)
        {
            var output = new StringBuilder();
            AppendBlock(output, block, MNNMarkdownPalette.For(theme));
            return output.ToString();
        }

        public static bool IsSafeImageUrl(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri)) return false;
            return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeFile;
        }

        public static IReadOnlyList<MNNMarkdownImage> ExtractImages(string markdown)
        {
            return Render(markdown).Images;
        }

        private static void AppendBlock(StringBuilder output, MNNMarkdownBlock block, MNNMarkdownPalette palette,
            List<MNNMarkdownImage> images = null, int baseFontSize = 20)
        {
            if (block == null) throw new ArgumentNullException(nameof(block));
            switch (block.Type)
            {
                case MNNMarkdownBlockType.Heading:
                    output.Append("<b><size=").Append((int)Math.Round(baseFontSize * (1.5 - block.HeadingLevel * .1))).Append(">");
                    AppendInline(output, block.Text, palette, images);
                    output.Append("</size></b>");
                    break;
                case MNNMarkdownBlockType.Quote:
                    AppendInline(output, block.Text, palette, images, "<color=" + palette.Muted + ">│ </color>");
                    break;
                case MNNMarkdownBlockType.UnorderedList:
                    AppendList(output, block, false, palette, images);
                    break;
                case MNNMarkdownBlockType.OrderedList:
                    AppendList(output, block, true, palette, images);
                    break;
                case MNNMarkdownBlockType.Code:
                    AppendCode(output, block.Text, block.Language, palette);
                    break;
                case MNNMarkdownBlockType.Image:
                    string placeholder = ImagePlaceholder(block.ImageAlt, palette);
                    images?.Add(new MNNMarkdownImage(block.ImageAlt, block.ImageUrl, output.Length, placeholder.Length));
                    output.Append(placeholder);
                    break;
                case MNNMarkdownBlockType.ThematicBreak:
                    output.Append("<color=").Append(palette.Muted).Append(">────────</color>");
                    break;
                default:
                    AppendInline(output, block.Text, palette, images);
                    break;
            }
        }

        private static void AppendList(StringBuilder output, MNNMarkdownBlock block, bool ordered, MNNMarkdownPalette palette,
            List<MNNMarkdownImage> images)
        {
            var counters = ordered ? new Dictionary<int, int>() : null;
            for (int i = 0; i < block.Items.Count; ++i)
            {
                if (i > 0) output.Append('\n');
                var item = block.Items[i];
                output.Append(new string(' ', item.Level * 2));
                if (ordered)
                {
                    if (!counters.TryGetValue(item.Level, out int number)) number = item.Level == block.Items[0].Level ? block.OrderedStart : 1;
                    output.Append(number).Append(". ");
                    counters[item.Level] = number + 1;
                }
                else output.Append("• ");
                AppendInline(output, item.Text, palette, images);
            }
        }

        private static void AppendInline(StringBuilder output, string source, MNNMarkdownPalette palette, List<MNNMarkdownImage> images,
            string linePrefix = null)
        {
            var localImages = images == null ? null : new List<MNNMarkdownImage>();
            string rich = FormatInline(source, 0, localImages, palette);
            if (linePrefix != null)
            {
                int imageIndex = 0;
                output.Append(linePrefix);
                for (int i = 0; i < rich.Length; ++i)
                {
                    if (localImages != null && imageIndex < localImages.Count && localImages[imageIndex].RichTextStart == i)
                    {
                        var image = localImages[imageIndex++];
                        int length = image.RichTextLength;
                        for (int j = image.RichTextStart; j < image.RichTextStart + image.RichTextLength; ++j)
                            if (rich[j] == '\n') length += linePrefix.Length;
                        images.Add(new MNNMarkdownImage(image.Alt, image.Url, output.Length, length));
                    }
                    output.Append(rich[i]);
                    if (rich[i] == '\n') output.Append(linePrefix);
                }
                return;
            }
            if (localImages != null)
                foreach (var image in localImages)
                    images.Add(new MNNMarkdownImage(image.Alt, image.Url,
                        output.Length + image.RichTextStart, image.RichTextLength));
            output.Append(rich);
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            if (value.IndexOf('<') < 0) return value;
            var output = new StringBuilder(value.Length + 8);
            AppendEscaped(output, value);
            return output.ToString();
        }

        private static void AppendEscaped(StringBuilder output, string value)
        {
            if (string.IsNullOrEmpty(value)) return;
            for (int i = 0; i < value.Length; ++i) AppendEscaped(output, value[i]);
        }

        private static void AppendEscaped(StringBuilder output, char value)
        {
            switch (value)
            {
                // Legacy uGUI/IMGUI does not decode HTML entities or support <noparse>.
                // A zero-width separator preserves the visible '<' while preventing tag injection.
                case '<': output.Append("<\u200B"); break;
                default: output.Append(value); break;
            }
        }
    }
}
