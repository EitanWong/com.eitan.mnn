using System;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class MarkdownCornerCaseTests
    {
        private const string Code = "<color=#C9D4EA>";
        private static string Visible(string rich) => Regex.Replace(rich,
            @"</?(?:b|i|s|u|color|size)(?:=[^>]*)?>", "").Replace("\u200B", "");

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" \t\r\n")]
        public void EmptyInputHasNoBlocksOrImages(string source)
        {
            var result = MNNMarkdownRenderer.Render(source);
            Assert.IsEmpty(result.Document.Blocks);
            Assert.IsEmpty(result.RichText);
            Assert.IsEmpty(result.Images);
        }

        [TestCase("#", "", 1)]
        [TestCase("######", "", 6)]
        [TestCase("# title ###", "title", 1)]
        [TestCase("# title#", "title#", 1)]
        [TestCase("## title ###tail", "title ###tail", 2)]
        public void AtxHeadingsHonorEmptyContentAndClosingHashRuns(string source, string text, int level)
        {
            var block = MNNMarkdownDocument.Parse(source).Blocks[0];
            Assert.AreEqual(MNNMarkdownBlockType.Heading, block.Type);
            Assert.AreEqual(level, block.HeadingLevel);
            Assert.AreEqual(text, block.Text);
        }

        [TestCase("    # literal")]
        [TestCase("    > literal")]
        [TestCase("    ---")]
        [TestCase("    ```")]
        public void FourSpaceIndentDoesNotStartAnUnindentedConstruct(string source)
        {
            Assert.AreEqual(MNNMarkdownBlockType.Code, MNNMarkdownDocument.Parse(source).Blocks[0].Type);
            Assert.AreEqual(source.Substring(4), MNNMarkdownDocument.Parse(source).Blocks[0].Text);
        }

        [Test]
        public void FenceClosingRunMustBeAtLeastAsLongAsOpeningRun()
        {
            var blocks = MNNMarkdownDocument.Parse("````csharp\na\n```\nb\n`````\nafter").Blocks;
            Assert.AreEqual(2, blocks.Count);
            Assert.AreEqual("a\n```\nb", blocks[0].Text);
            Assert.AreEqual("csharp", blocks[0].Language);
            Assert.AreEqual("after", blocks[1].Text);
        }

        [Test]
        public void FencePreservesLeadingAndTrailingBlankLines()
        {
            Assert.AreEqual("\n\nx\n", MNNMarkdownDocument.Parse("```\n\n\nx\n\n```").Blocks[0].Text);
            Assert.AreEqual("x\n", MNNMarkdownDocument.Parse("```\nx\n").Blocks[0].Text);
        }

        [Test]
        public void FencedCodeRemovesOnlyTheOpeningFenceIndent()
        {
            Assert.AreEqual("code\n  extra", MNNMarkdownDocument.Parse("  ```\n  code\n    extra\n  ```").Blocks[0].Text);
        }

        [Test]
        public void BacktickInInfoStringDoesNotOpenFence()
        {
            Assert.AreNotEqual(MNNMarkdownBlockType.Code,
                MNNMarkdownDocument.Parse("```a`b\ntext").Blocks[0].Type);
        }

        [TestCase("Title\n===", 1)]
        [TestCase("Title\n---", 2)]
        public void SetextHeadingIsNotConfusedWithThematicBreak(string source, int level)
        {
            var blocks = MNNMarkdownDocument.Parse(source).Blocks;
            Assert.AreEqual(1, blocks.Count);
            Assert.AreEqual(MNNMarkdownBlockType.Heading, blocks[0].Type);
            Assert.AreEqual(level, blocks[0].HeadingLevel);
            Assert.AreEqual("Title", blocks[0].Text);
        }

        [Test]
        public void OrderedListKeepsItsStartNumber()
        {
            Assert.AreEqual("42. first\n43. second", Visible(MNNMarkdownRenderer.ToRichText("42. first\n43. second")));
        }

        [TestCase("١. literal")]
        [TestCase("1234567890. literal")]
        public void OrderedMarkerRequiresOneToNineAsciiDigits(string source)
        {
            Assert.AreEqual(MNNMarkdownBlockType.Paragraph, MNNMarkdownDocument.Parse(source).Blocks[0].Type);
        }

        [Test]
        public void EmptyAndContinuedListItemsRemainInTheList()
        {
            var blocks = MNNMarkdownDocument.Parse("- \n- alpha\n  continuation\n- beta").Blocks;
            Assert.AreEqual(1, blocks.Count);
            Assert.AreEqual(3, blocks[0].Items.Count);
            Assert.AreEqual("", blocks[0].Items[0].Text);
            Assert.AreEqual("alpha\ncontinuation", blocks[0].Items[1].Text);
        }

        [Test]
        public void QuoteMarkerAppearsOnEachLine()
        {
            Assert.AreEqual("│ first\n│ second", Visible(MNNMarkdownRenderer.ToRichText("> first\n> second")));
        }

        [Test]
        public void EmphasisCanSpanQuoteLines()
        {
            string rich = MNNMarkdownRenderer.ToRichText("> *first\n> second*");
            StringAssert.Contains("<i>", rich);
            Assert.AreEqual("│ first\n│ second", Visible(rich));
        }

        [TestCase("snake__case__name")]
        [TestCase("snake_case_name")]
        [TestCase("a * spaced *")]
        [TestCase("** spaced **")]
        [TestCase("_ spaced _")]
        [TestCase("foo_bar_baz")]
        [TestCase("``x`")]
        public void LiteralDelimiterRunsStayLiteral(string source)
        {
            Assert.AreEqual(source, MNNMarkdownRenderer.ToRichText(source));
        }

        [TestCase("***word***", "<i><b>word</b></i>")]
        [TestCase("**a *b* c**", "<b>a <i>b</i> c</b>")]
        [TestCase("*a **b** c*", "<i>a <b>b</b> c</i>")]
        [TestCase("*a \\* b*", "<i>a * b</i>")]
        [TestCase("**a \\*\\* b**", "<b>a ** b</b>")]
        [TestCase("foo *\\**", "foo <i>*</i>")]
        [TestCase("foo **\\***", "foo <b>*</b>")]
        [TestCase("***foo** bar*", "<i><b>foo</b> bar</i>")]
        [TestCase("***foo* bar**", "<b><i>foo</i> bar</b>")]
        [TestCase("***foo**", "*<b>foo</b>")]
        [TestCase("****foo*", "***<i>foo</i>")]
        public void EmphasisRespectsNestingAndEscapedClosers(string source, string expected)
        {
            Assert.AreEqual(expected, MNNMarkdownRenderer.ToRichText(source));
        }

        [TestCase("`a``b`", "a``b")]
        [TestCase("``a`b``", "a`b")]
        [TestCase("`  a  `", " a ")]
        [TestCase("`   `", "   ")]
        [TestCase("`a\nb`", "a b")]
        public void CodeSpansMatchExactRunsAndNormalizeOnlyOnePaddingSpace(string source, string expected)
        {
            Assert.AreEqual(Code + expected + "</color>", MNNMarkdownRenderer.ToRichText(source));
        }

        [Test]
        public void EmphasisCannotCloseInsideCodeSpan()
        {
            Assert.AreEqual("<i>a " + Code + "*</color> b</i>", MNNMarkdownRenderer.ToRichText("*a `*` b*"));
        }

        [TestCase("![a](<https://example.com/a b.png> \"title\")", "a", "https://example.com/a b.png")]
        [TestCase("![a](https://example.com/a\\(b\\).png)", "a", "https://example.com/a(b).png")]
        [TestCase("![a\\]b](https://example.com/x.png)", "a]b", "https://example.com/x.png")]
        [TestCase("![a [b] c](https://example.com/x.png)", "a [b] c", "https://example.com/x.png")]
        [TestCase("![a](https://example.com/a.png \"a ) title\")", "a", "https://example.com/a.png")]
        public void StandaloneAndInlineImagesUseIdenticalDestinationRules(string token, string alt, string url)
        {
            foreach (string source in new[] { token, "before " + token + " after" })
            {
                var result = MNNMarkdownRenderer.Render(source);
                Assert.AreEqual(1, result.ImageCount, source);
                Assert.AreEqual(alt, result.Images[0].Alt);
                Assert.AreEqual(url, result.Images[0].Url);
            }
        }

        [TestCase("![a](https://example.com/a.png garbage)")]
        [TestCase("![a](https://example.com/a.png \"unfinished)")]
        [TestCase("`![a](https://example.com/a.png)`")]
        [TestCase("\\![a](https://example.com/a.png)")]
        [TestCase("```\n![a](https://example.com/a.png)\n```")]
        [TestCase("![a](javascript:alert(1))")]
        [TestCase("![a](data:image/png;base64,AA)")]
        [TestCase("![a](https://example.com/a.png\n\n\"title\")")]
        public void InvalidEscapedAndCodeImagesNeverTriggerDownloads(string source)
        {
            Assert.IsEmpty(MNNMarkdownRenderer.ExtractImages(source));
        }

        [Test]
        public void LinkLabelCanContainBalancedBrackets()
        {
            Assert.AreEqual("a [b] c", Visible(MNNMarkdownRenderer.ToRichText("[a [b] c](https://example.com)")));
        }

        [TestCase("&amp; &lt; &gt; &#65; &#x1F600;", "& < > A 😀")]
        [TestCase("&#0;", "\uFFFD")]
        [TestCase("\\$ \\& \\=", "$ & =")]
        public void TextEntitiesAndAsciiPunctuationEscapesRenderAsVisibleCharacters(string source, string expected)
        {
            Assert.AreEqual(expected, Visible(MNNMarkdownRenderer.ToRichText(source)));
        }

        [Test]
        public void CodeDoesNotDecodeEntitiesOrInterpretImageSyntax()
        {
            Assert.AreEqual("&amp; ![a](https://example.com/a.png)", Visible(MNNMarkdownRenderer.ToRichText(
                "`&amp; ![a](https://example.com/a.png)`")));
        }

        [TestCase("a\\\nb", "a\nb")]
        [TestCase("a  \nb", "a\nb")]
        [TestCase("a\0b", "a\uFFFDb")]
        public void HardBreaksAndNullCharactersHaveDefinedVisibleOutput(string source, string expected)
        {
            Assert.AreEqual(expected, Visible(MNNMarkdownRenderer.ToRichText(source)));
        }

        [Test]
        public void EveryStreamingPrefixProducesBalancedFormatting()
        {
            const string source = "# 中文😀\n\n***nested `code`*** and [a [b]](https://example.com/a(b))\n\n" +
                "![a\\]b](<https://example.com/a b.png> \"title\")\n\n````cs\n\n**literal**\n```\n````";
            for (int length = 0; length <= source.Length; ++length)
                AssertBalanced(MNNMarkdownRenderer.Render(source.Substring(0, length)).RichText);
        }

        [Test]
        public void DeterministicMalformedInputsPreserveBalancedSafeOutput()
        {
            var random = new Random(7391);
            string[] pieces = { "*", "__", "***", "`", "``", "![", "](", ")", "\\", "<b>",
                "&amp;", "\n", "中文😀", "# ", "https://example.com/", "a", " " };
            for (int sample = 0; sample < 500; ++sample)
            {
                var source = new StringBuilder();
                for (int i = 0; i < 30; ++i) source.Append(pieces[random.Next(pieces.Length)]);
                var result = MNNMarkdownRenderer.Render(source.ToString());
                AssertBalanced(result.RichText);
                foreach (var image in result.Images) Assert.IsTrue(MNNMarkdownRenderer.IsSafeImageUrl(image.Url));
            }
        }

        [TestCase('[')]
        [TestCase('<')]
        [Timeout(10000)]
        public void LongUnterminatedTokensRemainLiteral(char marker)
        {
            string source = new string(marker, 50000);
            var result = MNNMarkdownRenderer.Render(source);
            Assert.AreEqual(source, Visible(result.RichText));
            Assert.IsEmpty(result.Images);
        }

        private static void AssertBalanced(string text)
        {
            var stack = new System.Collections.Generic.Stack<string>();
            foreach (Match match in Regex.Matches(text, @"<(/?)(b|i|s|u|color|size)(?:=[^>]*)?>"))
            {
                if (match.Groups[1].Length == 0) stack.Push(match.Groups[2].Value);
                else
                {
                    Assert.IsNotEmpty(stack, text);
                    Assert.AreEqual(stack.Pop(), match.Groups[2].Value, text);
                }
            }
            Assert.IsEmpty(stack, text);
        }
    }
}
