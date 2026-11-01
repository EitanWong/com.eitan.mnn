using System;
using System.Collections.Generic;
using System.Text;

namespace MNN.Unity
{
    /// <summary>Block constructs understood by the managed SiriAI Markdown pipeline.</summary>
    public enum MNNMarkdownBlockType
    {
        Paragraph,
        Heading,
        Quote,
        UnorderedList,
        OrderedList,
        Code,
        Image,
        ThematicBreak
    }

    /// <summary>A single item in an ordered or unordered Markdown list.</summary>
    public sealed class MNNMarkdownListItem
    {
        public string Text { get; }
        public int Level { get; }

        internal MNNMarkdownListItem(string text, int level)
        {
            Text = text ?? string.Empty;
            Level = level;
        }
    }

    /// <summary>Safe image destination extracted from a Markdown image token.</summary>
    public sealed class MNNMarkdownImage
    {
        public string Alt { get; }
        public string Url { get; }
        public int RichTextStart { get; }
        public int RichTextLength { get; }

        internal MNNMarkdownImage(string alt, string url, int richTextStart = 0, int richTextLength = 0)
        {
            Alt = alt ?? string.Empty;
            Url = url ?? string.Empty;
            RichTextStart = richTextStart;
            RichTextLength = richTextLength;
        }
    }

    /// <summary>
    /// The shared output of one Markdown pass. Editor and Player adapters can keep this
    /// object alongside their view model so parsing, Rich Text conversion and image
    /// discovery are performed once for the same source value.
    /// </summary>
    public sealed class MNNMarkdownRenderResult
    {
        public MNNMarkdownDocument Document { get; }
        public string RichText { get; }
        public IReadOnlyList<MNNMarkdownImage> Images { get; }
        public int ImageCount => Images.Count;
        /// <summary>UTF-16 text offset of a rendered slice, excluding generated rich-text tags.</summary>
        public int CharacterOffset { get; }

        internal MNNMarkdownRenderResult(MNNMarkdownDocument document, string richText,
            IReadOnlyList<MNNMarkdownImage> images, int characterOffset = 0)
        {
            Document = document;
            RichText = richText ?? string.Empty;
            Images = images ?? Array.Empty<MNNMarkdownImage>();
            CharacterOffset = characterOffset;
        }
    }

    /// <summary>Immutable block data shared by Editor preview and Player UI.</summary>
    public sealed class MNNMarkdownBlock
    {
        public MNNMarkdownBlockType Type { get; }
        public string Text { get; }
        public string Language { get; }
        public int HeadingLevel { get; }
        public string ImageAlt { get; }
        public string ImageUrl { get; }
        public IReadOnlyList<MNNMarkdownListItem> Items { get; }
        public int OrderedStart { get; }

        internal MNNMarkdownBlock(MNNMarkdownBlockType type, string text = "", int headingLevel = 0,
            string language = null, string imageAlt = null, string imageUrl = null,
            IReadOnlyList<MNNMarkdownListItem> items = null, int orderedStart = 1)
        {
            Type = type;
            Text = text ?? string.Empty;
            Language = language ?? string.Empty;
            HeadingLevel = headingLevel;
            ImageAlt = imageAlt ?? string.Empty;
            ImageUrl = imageUrl ?? string.Empty;
            Items = items ?? Array.Empty<MNNMarkdownListItem>();
            OrderedStart = orderedStart;
        }
    }

}
