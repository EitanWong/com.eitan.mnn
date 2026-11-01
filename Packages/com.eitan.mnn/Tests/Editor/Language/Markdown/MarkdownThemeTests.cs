using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests.Markdown
{
    public sealed class MarkdownThemeTests
    {
        [TestCase("# Heading `code` [link](https://example.com)")]
        [TestCase("Text `code` [**link** `code`](https://example.com) ~~deleted~~")]
        [TestCase("<https://example.com> ![inline](https://example.com/a.png)")]
        [TestCase("![block](https://example.com/a.png)")]
        [TestCase("[![nested](https://example.com/a.png)](https://example.com)")]
        [TestCase("> quote `code`\n> ~~deleted~~ [link](https://example.com)")]
        [TestCase("- `code`\n- [link](https://example.com)\n- ~~deleted~~")]
        [TestCase("1. `code`\n2. [link](https://example.com)")]
        [TestCase("```cs\nvar x = \"<b>literal</b>\";\n```")]
        [TestCase("    indented code")]
        [TestCase("---")]
        public void LightThemeRecolorsEveryInlineAndBlockPathWithoutChangingContent(string source)
        {
            string dark = MNNMarkdownRenderer.Render(source).RichText;
            string expected = dark.Replace("#8AB4FF", "#154B8B").Replace("#C9D4EA", "#24344D")
                .Replace("#A8B5D0", "#3D4E65").Replace("#D4B0FF", "#6938A0")
                .Replace("#A8D5A2", "#23613C").Replace("#F0C38E", "#7A4016");
            Assert.AreNotEqual(dark, expected, "The fixture must exercise a colored element.");
            Assert.AreEqual(expected, MNNMarkdownRenderer.Render(source, 20, MNNMarkdownTheme.Light).RichText);
            Assert.AreEqual(expected, MNNMarkdownRenderer.ToRichText(source, MNNMarkdownTheme.Light));
            var document = MNNMarkdownDocument.Parse(source);
            Assert.AreEqual(expected, MNNMarkdownRenderer.Render(document, 20, MNNMarkdownTheme.Light).RichText);
            Assert.AreEqual(expected, string.Join("\n\n", document.Blocks.Select(block =>
                MNNMarkdownRenderer.ToRichText(block, MNNMarkdownTheme.Light))));
        }

        [TestCase(MNNMarkdownTheme.Dark, "#8AB4FF", "#C9D4EA", "#A8B5D0", .24f)]
        [TestCase(MNNMarkdownTheme.Light, "#154B8B", "#24344D", "#3D4E65", .80f)]
        public void PaletteMeetsNormalTextContrastOnBoundedGlassAndEditorSurfaces(
            MNNMarkdownTheme theme, string link, string code, string muted, float glassBound)
        {
            string rich = MNNMarkdownRenderer.Render("`code` [link](https://example.com) ~~muted~~", 20, theme).RichText;
            foreach (string hex in new[] { link, code, muted })
            {
                StringAssert.Contains("<color=" + hex + ">", rich);
                Assert.IsTrue(ColorUtility.TryParseHtmlString(hex, out Color foreground));
                foreach (Color background in new[] { new Color(glassBound, glassBound, glassBound),
                    theme == MNNMarkdownTheme.Dark ? new Color(.19f, .19f, .19f) : new Color(.955f, .955f, .955f) })
                    Assert.GreaterOrEqual(Contrast(foreground, background), 4.5f, hex);
            }
        }

        [TestCase(MNNMarkdownTheme.Dark)]
        [TestCase(MNNMarkdownTheme.Light)]
        public void SlicesPreserveThemedTagsImageOffsetsAndVisibleCharacters(MNNMarkdownTheme theme)
        {
            string source = "> [![diagram](https://example.com/a.png)](https://example.com) `code`\n\n" +
                "```\n" + new string('x', 2200) + "\n```";
            var result = MNNMarkdownRenderer.Render(source, 20, theme);
            var parts = MNNMarkdownRenderer.RenderSlices(source, 90, 20, theme);
            Assert.Greater(parts.Count, 20);
            Assert.AreEqual(1, parts.Sum(part => part.ImageCount));
            var imagePart = parts.Single(part => part.ImageCount > 0);
            var image = imagePart.Images[0];
            string placeholder = imagePart.RichText.Substring(image.RichTextStart, image.RichTextLength);
            StringAssert.Contains("[Image: diagram]", placeholder);
            StringAssert.Contains(theme == MNNMarkdownTheme.Dark ? "#8AB4FF" : "#154B8B", placeholder);
            string Strip(string text) => System.Text.RegularExpressions.Regex.Replace(text, "</?(?:b|i|color|size)(?:=[^>]+)?>", "");
            Assert.AreEqual(Strip(result.RichText), string.Concat(parts.Select(part => Strip(part.RichText))));
            foreach (var part in parts.Skip(1))
                StringAssert.Contains(theme == MNNMarkdownTheme.Dark ? "#C9D4EA" : "#24344D", part.RichText);
        }

        [Test]
        public void ConcurrentConsumersDoNotShareMutableThemeState()
        {
            Parallel.For(0, 64, index =>
            {
                bool light = index % 2 == 0;
                string rich = MNNMarkdownRenderer.Render("`code`", 20,
                    light ? MNNMarkdownTheme.Light : MNNMarkdownTheme.Dark).RichText;
                Assert.AreEqual(light ? "<color=#24344D>code</color>" : "<color=#C9D4EA>code</color>", rich);
            });
            Assert.AreEqual("<color=#C9D4EA>code</color>", MNNMarkdownRenderer.ToRichText("`code`"));
        }

        [Test]
        public void InvalidThemeIsRejectedAtTheRenderBoundary()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => MNNMarkdownRenderer.Render("text", 20, (MNNMarkdownTheme)99));
        }

        private static float Contrast(Color a, Color b)
        {
            a = a.linear;
            b = b.linear;
            float first = .2126f * a.r + .7152f * a.g + .0722f * a.b;
            float second = .2126f * b.r + .7152f * b.g + .0722f * b.b;
            return (Mathf.Max(first, second) + .05f) / (Mathf.Min(first, second) + .05f);
        }
    }
}
