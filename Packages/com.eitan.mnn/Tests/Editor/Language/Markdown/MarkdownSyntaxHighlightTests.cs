using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests.Markdown
{
    public sealed class MarkdownSyntaxHighlightTests
    {
        private static string Visible(string rich) => Regex.Replace(rich, @"</?(?:color|b|i|size)(?:=[^>]*)?>", "").Replace("\u200B", "");

        [TestCase("cs", "return 42; // note", "return", "42", "// note")]
        [TestCase("csharp", "return 42; // note", "return", "42", "// note")]
        [TestCase("C#", "return 42; // note", "return", "42", "// note")]
        [TestCase("cpp", "return 42; // note", "return", "42", "// note")]
        [TestCase("c", "return 42; // note", "return", "42", "// note")]
        [TestCase("javascript", "return 42; // note", "return", "42", "// note")]
        [TestCase("ts", "return 42; // note", "return", "42", "// note")]
        [TestCase("py", "return 42 # note", "return", "42", "# note")]
        [TestCase("bash", "if true; then echo 42; fi # note", "if", "42", "# note")]
        [TestCase("sql", "SELECT 42; -- note", "SELECT", "42", "-- note")]
        public void RecognizedFenceLanguagesColorKeywordsNumbersAndComments(string language, string code,
            string keyword, string number, string comment)
        {
            string rich = MNNMarkdownRenderer.ToRichText("```" + language + "\n" + code + "\n```");
            StringAssert.Contains("<color=#D4B0FF>" + keyword + "</color>", rich);
            StringAssert.Contains("<color=#F0C38E>" + number + "</color>", rich);
            StringAssert.Contains("<color=#A8B5D0>" + comment + "</color>", rich);
            Assert.AreEqual(code, Visible(rich));
        }

        [Test]
        public void JsonDistinguishesPropertyNamesStringsLiteralsAndNumbers()
        {
            const string code = "{\"name\": \"return 42 // text\", \"ready\": true, \"value\": -1.2e+3}";
            string rich = MNNMarkdownRenderer.ToRichText("```json\n" + code + "\n```");
            StringAssert.Contains("<color=#8AB4FF>\"name\"</color>", rich);
            StringAssert.Contains("<color=#A8D5A2>\"return 42 // text\"</color>", rich);
            StringAssert.Contains("<color=#D4B0FF>true</color>", rich);
            StringAssert.Contains("<color=#F0C38E>-1.2e+3</color>", rich);
            Assert.AreEqual(code, Visible(rich));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("text")]
        [TestCase("unrecognized-language")]
        public void UnknownOrMissingLanguageRetainsReadableLiteralCode(string language)
        {
            const string code = "return 42; <b>literal</b> ![image](https://example.com/a.png)";
            var result = MNNMarkdownRenderer.Render("```" + language + "\n" + code + "\n```");
            Assert.AreEqual(code, Visible(result.RichText));
            Assert.AreEqual(1, Regex.Matches(result.RichText, "<color=").Count);
            Assert.IsEmpty(result.Images);
        }

        [Test]
        public void KeywordsInsideIdentifiersDoNotReceiveKeywordColor()
        {
            const string code = "returnValue = stringify42 + diff;";
            string rich = MNNMarkdownRenderer.ToRichText("```cs\n" + code + "\n```");
            Assert.AreEqual("<color=#C9D4EA>" + code + "</color>", rich);
        }

        [TestCase("cs", "\"return 42 // text\"")]
        [TestCase("cs", "@\"return \"\"quoted\"\"\n42 // text\"")]
        [TestCase("cs", "$@\"return {value}\n42 // text\"")]
        [TestCase("cs", "\"\"\"return \"quoted\"\n42 // text\"\"\"")]
        [TestCase("cs", "'\\''")]
        [TestCase("cpp", "u8R\"tag(return \"quoted\" /*42*/)tag\"")]
        [TestCase("py", "r\"\"\"return 'quoted'\n42 # text\"\"\"")]
        [TestCase("py", "f\"return {value} 42 # text\"")]
        [TestCase("js", "`return ${value}\n42 // text`")]
        [TestCase("js", "\"return \\\"quoted\\\" 42 // text\"")]
        [TestCase("bash", "'return \\42 # text'")]
        [TestCase("sql", "'return O''Reilly 42 -- text'")]
        public void StringContentsAreOpaqueToCommentsNumbersAndKeywords(string language, string literal)
        {
            string rich = MNNMarkdownRenderer.ToCodeRichText(literal, language);
            Assert.AreEqual("<color=#C9D4EA><color=#A8D5A2>" + literal + "</color></color>", rich);
            Assert.AreEqual(literal, Visible(rich));
        }

        [TestCase("cs", "/* return \"quoted\"\n42 */")]
        [TestCase("js", "// return \"quoted\" 42")]
        [TestCase("python", "# return \"quoted\" 42")]
        [TestCase("bash", "# return \"quoted\" 42")]
        [TestCase("sql", "-- return \"quoted\" 42")]
        public void CommentContentsAreOpaqueAndLexingResumesOnTheNextToken(string language, string comment)
        {
            string rich = MNNMarkdownRenderer.ToCodeRichText(comment + "\nreturn 3", language);
            StringAssert.Contains("<color=#A8B5D0>" + comment + "</color>", rich);
            StringAssert.Contains("<color=#D4B0FF>return</color>", rich);
            StringAssert.Contains("<color=#F0C38E>3</color>", rich);
        }

        [TestCase("cs", "0xDEAD")]
        [TestCase("cs", "0b1010")]
        [TestCase("cs", "1_024")]
        [TestCase("cs", ".5")]
        [TestCase("cs", "1e-3")]
        [TestCase("cs", "42UL")]
        [TestCase("cs", "12.0m")]
        [TestCase("js", "42n")]
        [TestCase("python", "3.5j")]
        [TestCase("json", "-1.2e+3")]
        [TestCase("cpp", "1'000")]
        [TestCase("cpp", "0xFF'AB")]
        public void NumericLiteralsRemainOneToken(string language, string literal)
        {
            Assert.AreEqual("<color=#C9D4EA><color=#F0C38E>" + literal + "</color></color>",
                MNNMarkdownRenderer.ToCodeRichText(literal, language));
        }

        [Test]
        public void ShellHashInsideAnUnquotedWordDoesNotStartAComment()
        {
            const string code = "echo abc#word # comment";
            string rich = MNNMarkdownRenderer.ToCodeRichText(code, "bash");
            StringAssert.Contains("echo abc#word <color=#A8B5D0># comment</color>", rich);
        }

        [Test]
        public void UnicodeContinuationCharactersKeepIdentifiersWhole()
        {
            const string code = "return\u0301 @class";
            Assert.AreEqual("<color=#C9D4EA>" + code + "</color>", MNNMarkdownRenderer.ToCodeRichText(code, "cs"));
        }

        [Test]
        public void EscapedCSharpIdentifiersAndLanguageInfoAreHandledAtTheirBoundaries()
        {
            string rich = MNNMarkdownRenderer.ToCodeRichText("@class = 1; return @class;", " CSharp title=\"x.cs\"");
            StringAssert.DoesNotContain("<color=#D4B0FF>class</color>", rich);
            StringAssert.Contains("<color=#D4B0FF>return</color>", rich);
            Assert.AreEqual("returnValue diff stringify42", Visible(MNNMarkdownRenderer.ToCodeRichText("returnValue diff stringify42", "cs")));
        }

        [Test]
        public void LiteralCodeTagsImagesEntitiesWhitespaceAndNullInputStaySafe()
        {
            const string code = "return \"<color=red>x</color> &amp; ![x](https://example.com/a.png)\";\r\n\t// 😀\0";
            string rich = MNNMarkdownRenderer.ToCodeRichText(code, "cs");
            Assert.AreEqual(code.Replace('\0', '\uFFFD'), Visible(rich));
            StringAssert.DoesNotContain("<color=red>", rich);
            Assert.Throws<System.ArgumentNullException>(() => MNNMarkdownRenderer.ToCodeRichText(null, "cs"));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => MNNMarkdownRenderer.ToCodeRichText("x", "cs", (MNNMarkdownTheme)99));
        }

        [TestCase("cs", "var x = @\"a \"\"b\"\"\nreturn 42\"; /* tail")]
        [TestCase("cpp", "auto x = R\"tag(a \"b\" //42)tag\"; /* tail")]
        [TestCase("python", "def f():\n    return r\"\"\"a\n42 # text\"\"\" # tail")]
        [TestCase("js", "const s = `a ${x}\n42 // text`; /* tail")]
        [TestCase("json", "{\"escaped\\\"key\": true, \"value\": -1.2e+3}")]
        public void EveryStreamingPrefixPreservesCharactersAndBalancedColors(string language, string code)
        {
            for (int length = 0; length <= code.Length; ++length)
            {
                string prefix = code.Substring(0, length);
                string rich = MNNMarkdownRenderer.ToCodeRichText(prefix, language);
                Assert.AreEqual(prefix, Visible(rich), length.ToString());
                AssertBalanced(rich);
            }
        }

        [TestCase(MNNMarkdownTheme.Dark)]
        [TestCase(MNNMarkdownTheme.Light)]
        public void ColorStateAndImagesSurviveMessageSlicing(MNNMarkdownTheme theme)
        {
            string code = "/* " + new string('x', 1100) + " return 42 */\nreturn \"" + new string('y', 1100) + "\";";
            string source = "```cs\n" + code + "\n```\n\n![actual](https://example.com/a.png)";
            var complete = MNNMarkdownRenderer.Render(source, 20, theme);
            var slices = MNNMarkdownRenderer.RenderSlices(source, 120, 20, theme);
            Assert.AreEqual(Visible(complete.RichText), string.Concat(slices.Select(slice => Visible(slice.RichText))));
            Assert.AreEqual(1, slices.Sum(slice => slice.ImageCount));
            foreach (var slice in slices) AssertBalanced(slice.RichText);
            StringAssert.Contains(theme == MNNMarkdownTheme.Dark ? "#A8B5D0" : "#3D4E65", slices[1].RichText);
            Assert.IsEmpty(slices[1].Images);
            var imageSlice = slices.Single(slice => slice.ImageCount > 0);
            var image = imageSlice.Images[0];
            Assert.AreEqual("[Image: actual]", Visible(imageSlice.RichText.Substring(image.RichTextStart, image.RichTextLength)));
        }

        [TestCase(MNNMarkdownTheme.Dark)]
        [TestCase(MNNMarkdownTheme.Light)]
        public void AllSyntaxColorsRemainReadableOnChatAndGlassSurfaces(MNNMarkdownTheme theme)
        {
            string rich = MNNMarkdownRenderer.ToCodeRichText("return \"text\" + 42; // note", "cs", theme) +
                MNNMarkdownRenderer.ToCodeRichText("{\"key\": null}", "json", theme);
            var colors = Regex.Matches(rich, "<color=(#[0-9A-Fa-f]{6})>").Cast<Match>().Select(m => m.Groups[1].Value).Distinct().ToArray();
            Assert.GreaterOrEqual(colors.Length, 6);
            var backgrounds = theme == MNNMarkdownTheme.Dark
                ? new[] { new Color(.18f, .29f, .34f), new Color(.24f, .24f, .24f) }
                : new[] { new Color(.72f, .83f, .89f), new Color(.80f, .80f, .80f) };
            foreach (string hex in colors)
            {
                Assert.IsTrue(ColorUtility.TryParseHtmlString(hex, out Color color));
                foreach (var background in backgrounds)
                {
                    float a = Luminance(color), b = Luminance(background);
                    Assert.GreaterOrEqual((Mathf.Max(a, b) + .05f) / (Mathf.Min(a, b) + .05f), 4.5f, hex);
                }
            }
        }

        [Test]
        public void ConcurrentLanguageAndThemeConsumersStayIndependent()
        {
            Parallel.For(0, 32, index =>
            {
                bool light = index % 2 == 0;
                string rich = MNNMarkdownRenderer.ToCodeRichText("return 42", index % 3 == 0 ? "python" : "cs",
                    light ? MNNMarkdownTheme.Light : MNNMarkdownTheme.Dark);
                StringAssert.Contains(light ? "<color=#6938A0>return</color>" : "<color=#D4B0FF>return</color>", rich);
                Assert.AreEqual("return 42", Visible(rich));
            });
        }

        [Test]
        [Timeout(10000)]
        public void LongMalformedTokensAndIdentifiersFinishWithoutLosingSource()
        {
            foreach (string code in new[] { new string('a', 100000), "/*" + new string('*', 100000),
                "\"" + new string('\\', 100000), "R\"tag(" + new string(')', 100000) })
                Assert.AreEqual(code, Visible(MNNMarkdownRenderer.ToCodeRichText(code, "cpp")));
        }

        [TestCase("js")]
        [TestCase("python")]
        [TestCase("cs")]
        [TestCase("sql")]
        [Timeout(10000)]
        public void AdversarialQuoteRunsCompleteWithoutRepeatedSuffixScans(string language)
        {
            string code = new string('"', 400000);
            var timer = System.Diagnostics.Stopwatch.StartNew();
            string rich = MNNMarkdownRenderer.ToCodeRichText(code, language);
            timer.Stop();
            Assert.AreEqual(code, Visible(rich));
            Assert.Less(timer.Elapsed.TotalSeconds, 8d, "Quote-run scanning must stay within the stress-test budget.");
        }

        private static float Luminance(Color color)
        {
            color = color.linear;
            return .2126f * color.r + .7152f * color.g + .0722f * color.b;
        }

        private static void AssertBalanced(string rich)
        {
            int depth = 0;
            foreach (Match match in Regex.Matches(rich, "</?color(?:=[^>]*)?>"))
            {
                depth += match.Value[1] == '/' ? -1 : 1;
                Assert.GreaterOrEqual(depth, 0);
            }
            Assert.AreEqual(0, depth);
        }
    }
}
