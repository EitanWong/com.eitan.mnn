using System;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class MarkdownSliceTests
    {
        private static string Visible(string text) => Regex.Replace(text,
            @"</?(?:b|i|color|size)(?:=[^>]*)?>", "").Replace("\u200B", "");

        [Test]
        public void ImageOffsetsPointAtTheActualMarkerAcrossBlocksQuotesAndNestedLinks()
        {
            var result = MNNMarkdownRenderer.Render("# **Heading**\n\n> quote ![first](https://example.com/a.png)\n" +
                "> [![second](https://example.com/b.png)](https://example.com)\n\n" +
                "![third](https://example.com/c.png)");
            Assert.AreEqual(3, result.ImageCount);
            foreach (var image in result.Images)
            {
                string marker = result.RichText.Substring(image.RichTextStart, image.RichTextLength);
                Assert.AreEqual("[Image: " + image.Alt + "]", Visible(marker));
            }
        }

        [Test]
        public void FormattingAndImageMetadataSurviveTinySlices()
        {
            const string source = "**ab😀c** ![one](https://example.com/one.png) tail ![two](https://example.com/two.png)";
            var complete = MNNMarkdownRenderer.Render(source);
            var parts = MNNMarkdownRenderer.RenderSlices(source, 4);
            Assert.Greater(parts.Count, 4);
            Assert.AreEqual(Visible(complete.RichText), string.Concat(parts.Select(part => Visible(part.RichText))));
            CollectionAssert.AreEqual(complete.Images.Select(image => image.Url), parts.SelectMany(part => part.Images).Select(image => image.Url));
            int position = 0;
            foreach (var part in parts)
            {
                string visible = Visible(part.RichText);
                Assert.LessOrEqual(visible.Length, 4);
                Assert.IsFalse(char.IsLowSurrogate(visible[0]));
                Assert.IsFalse(char.IsHighSurrogate(visible[visible.Length - 1]));
                Assert.AreEqual(position, part.CharacterOffset);
                position += visible.Length;
            }
        }

        [Test]
        public void LookalikeLinkLabelDoesNotStealActualImagesSlice()
        {
            string source = "[[Image: cat]](https://example.com) " + new string('x', 40) +
                " ![cat](https://example.com/cat.png)";
            var parts = MNNMarkdownRenderer.RenderSlices(source, 16);
            Assert.IsEmpty(parts[0].Images);
            Assert.AreEqual(1, parts.Sum(part => part.ImageCount));
            Assert.AreEqual("https://example.com/cat.png", parts.Single(part => part.ImageCount == 1).Images[0].Url);
        }

        [Test]
        public void ExactBoundaryDoesNotCreateAnEmptyTrailingSlice()
        {
            var parts = MNNMarkdownRenderer.RenderSlices("**abcd**", 4);
            Assert.AreEqual(1, parts.Count);
            Assert.AreEqual("<b>abcd</b>", parts[0].RichText);
        }

        [Test]
        public void PublicArgumentContractsAreExplicit()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => MNNMarkdownRenderer.RenderSlices("x", 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => MNNMarkdownRenderer.Render("x", 0));
            Assert.Throws<ArgumentNullException>(() => MNNMarkdownRenderer.Render((MNNMarkdownDocument)null));
            Assert.Throws<ArgumentNullException>(() => MNNMarkdownRenderer.ToRichText((MNNMarkdownBlock)null));
        }
    }
}
