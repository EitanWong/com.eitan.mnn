using MNN.Unity.Editor.UI;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class StudioMarkdownThemeTests
    {
        [TestCase(MNNMarkdownTheme.Dark, "#D4B0FF")]
        [TestCase(MNNMarkdownTheme.Light, "#6938A0")]
        public void StudioHighlightsCodeWithTheSharedRendererAndRetainsCopyableSource(MNNMarkdownTheme theme, string keyword)
        {
            const string code = "return \"<b>literal</b>\"; // note";
            var markdown = new MNNStudioMarkdown("```cs\n" + code + "\n```");
            markdown.SetTheme(theme);
            var block = markdown.Blocks[0];
            Assert.AreEqual(code, block.Text, "Copy code uses the original source.");
            Assert.AreEqual(MNNMarkdownRenderer.ToRichText("```cs\n" + code + "\n```", theme), block.RichText);
            StringAssert.Contains("<color=" + keyword + ">return</color>", block.RichText);
            StringAssert.DoesNotContain("<b>", block.RichText, "Literal source tags must not execute.");
            markdown.SetTheme(theme);
            Assert.AreSame(block, markdown.Blocks[0], "Repaint reuses highlighted code.");
        }

        [TestCase(MNNMarkdownTheme.Dark, "#C9D4EA", "#8AB4FF")]
        [TestCase(MNNMarkdownTheme.Light, "#24344D", "#154B8B")]
        public void ExistingStudioBlocksFollowSkinWhileCopiedCodeStaysLiteral(MNNMarkdownTheme theme, string code, string link)
        {
            var markdown = new MNNStudioMarkdown("# Heading `inline`\n\n[link](https://example.com)\n\n```cs\n<b>literal</b>\n```");
            markdown.SetTheme(theme == MNNMarkdownTheme.Dark ? MNNMarkdownTheme.Light : MNNMarkdownTheme.Dark);
            markdown.SetTheme(theme);
            Assert.AreEqual(3, markdown.Blocks.Count);
            StringAssert.Contains("<color=" + code + ">inline</color>", markdown.Blocks[0].Text);
            StringAssert.Contains("<color=" + link + ">link</color>", markdown.Blocks[1].Text);
            Assert.IsTrue(markdown.Blocks[2].Code);
            Assert.AreEqual("cs", markdown.Blocks[2].Language);
            Assert.AreEqual("<b>literal</b>", markdown.Blocks[2].Text);
            var unchanged = markdown.Blocks[0];
            markdown.SetTheme(theme);
            Assert.AreSame(unchanged, markdown.Blocks[0], "Repaint must reuse themed blocks.");
        }
    }
}
