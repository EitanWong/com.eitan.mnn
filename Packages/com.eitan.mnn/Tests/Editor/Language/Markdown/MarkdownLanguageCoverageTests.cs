using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;

namespace MNN.Unity.Tests.Markdown
{
    public sealed class MarkdownLanguageCoverageTests
    {
        private const string Keyword = "#D4B0FF", Literal = "#A8D5A2", Comment = "#A8B5D0", Number = "#F0C38E", Property = "#8AB4FF";
        private static readonly string[] Languages = { "cs", "cpp", "js", "python", "json", "bash", "sql", "java", "kotlin",
            "swift", "go", "rust", "dart", "lua", "ruby", "php", "r", "html", "css", "yaml", "toml" };

        private static string Escape(string value) => value.Replace("<", "<\u200B");
        private static string Visible(string value) => Regex.Replace(value, @"</?color(?:=#[0-9A-Fa-f]{6})?>", "").Replace("<\u200B", "<");
        private static string Token(string value, string color) => "<color=" + color + ">" + Escape(value) + "</color>";

        [TestCase("java", "public int value = 42; // note", "public", "// note")]
        [TestCase("kotlin", "val value = 42 // note", "val", "// note")]
        [TestCase("swift", "let value = 42 // note", "let", "// note")]
        [TestCase("go", "func value() { return 42 } // note", "func", "// note")]
        [TestCase("rust", "fn value() { return 42; } // note", "fn", "// note")]
        [TestCase("dart", "final value = 42; // note", "final", "// note")]
        [TestCase("lua", "local value = 42 -- note", "local", "-- note")]
        [TestCase("ruby", "def value; return 42; end # note", "def", "# note")]
        [TestCase("php", "function value() { return 42; } # note", "function", "# note")]
        [TestCase("r", "function() { return(42) } # note", "function", "# note")]
        [TestCase("yaml", "value: true\ncount: 42 # note", "true", "# note")]
        [TestCase("toml", "value = true\ncount = 42 # note", "true", "# note")]
        public void NewLanguageProfilesHighlightRepresentativeTokens(string language, string code, string keyword, string comment)
        {
            var result = MNNMarkdownRenderer.Render("```" + language + "\n" + code + "\n```");
            Assert.AreEqual(code, Visible(result.RichText));
            StringAssert.Contains(Token(keyword, Keyword), result.RichText);
            StringAssert.Contains(Token("42", Number), result.RichText);
            StringAssert.Contains(Token(comment, Comment), result.RichText);
            Assert.IsEmpty(result.Images);
        }

        [TestCase("java", "JAVA")]
        [TestCase("kotlin", "kt")]
        [TestCase("kotlin", "kts")]
        [TestCase("rust", "rs")]
        [TestCase("go", "golang")]
        [TestCase("ruby", "rb")]
        [TestCase("php", "PHP")]
        [TestCase("r", "R")]
        [TestCase("html", "xml")]
        [TestCase("html", "xhtml")]
        [TestCase("html", "svg")]
        [TestCase("css", "CSS")]
        [TestCase("yaml", "yml")]
        public void AliasesAndInfoMetadataSelectTheSameProfile(string canonical, string alias)
        {
            const string code = "return true 42 \"text\" # note <item name=\"x\"/>";
            Assert.AreEqual(MNNMarkdownRenderer.ToCodeRichText(code, canonical),
                MNNMarkdownRenderer.ToCodeRichText(code, "  " + alias + " title=\"file\""));
        }

        [TestCase("java", "\"\"\"\nreturn \"quoted\" 42 // text\n\"\"\"")]
        [TestCase("kotlin", "\"\"\"return \\\"quoted\"\n42 // text\"\"\"")]
        [TestCase("swift", "#\"return \\" + "\"quoted\" 42 // text\"#")]
        [TestCase("swift", "##\"\"\"return \"quoted\"\n42 // text\"\"\"##")]
        [TestCase("go", "`return \\42\n// text`")]
        [TestCase("rust", "r###\"return \"quoted\" #\n42 // text\"###")]
        [TestCase("rust", "br#\"return \\" + "\"quoted\" 42 // text\"#")]
        [TestCase("rust", "b'\\n'")]
        [TestCase("rust", "'😀'")]
        [TestCase("dart", "r'''return \\42\n// text'''")]
        [TestCase("dart", "\"\"\"return \"quoted\"\n42 // text\"\"\"")]
        [TestCase("lua", "[==[return \"quoted\" ]=]\n42 -- text]==]")]
        [TestCase("ruby", "`return 42 # text`")]
        [TestCase("php", "\"return 42 // text\"")]
        [TestCase("r", "`return 42 # text`")]
        [TestCase("yaml", "'it''s # true 42'")]
        [TestCase("toml", "'''return \\42\n# text'''")]
        [TestCase("toml", "\"\"\"true \"quoted\"\n42 # text\"\"\"")]
        [TestCase("swift", "#\"return \\#\"quoted\\#\" 42\"#")]
        [TestCase("rust", "'\\u{1F600}'")]
        [TestCase("rust", "'\\x41'")]
        public void LanguageSpecificStringsRemainOpaque(string language, string literal)
        {
            Assert.AreEqual("<color=#C9D4EA>" + Token(literal, Literal) + "</color>",
                MNNMarkdownRenderer.ToCodeRichText(literal, language));
        }

        [TestCase("rust")]
        [TestCase("kotlin")]
        [TestCase("swift")]
        [TestCase("dart")]
        public void NestedCommentsConsumeAllLevelsBeforeResumingCode(string language)
        {
            const string comment = "/* outer /* nested \" */ return 42 */";
            string rich = MNNMarkdownRenderer.ToCodeRichText(comment + " return 9", language);
            StringAssert.Contains(Token(comment, Comment), rich);
            StringAssert.Contains(Token("return", Keyword), rich);
            StringAssert.Contains(Token("9", Number), rich);
        }

        [TestCase("lua", "--[=[ outer ]==] return 42 ]=]")]
        [TestCase("ruby", "=begin\nreturn 42 # nested\n=end")]
        public void LongCommentsDoNotExposeTheirContentsAsCode(string language, string comment)
        {
            string rich = MNNMarkdownRenderer.ToCodeRichText(comment + "\nreturn 9", language);
            StringAssert.Contains(Token(comment, Comment), rich);
            StringAssert.Contains(Token("return", Keyword), rich);
        }

        [Test]
        public void RustLifetimesRawIdentifiersAndCharactersHaveDistinctBoundaries()
        {
            const string code = "fn f<'a>(x: &'a str) { let r#type = 'x'; return x; }";
            string rich = MNNMarkdownRenderer.ToCodeRichText(code, "rs");
            StringAssert.Contains("f<'a>(x: &'a str)".Replace("<", "<\u200B"), rich);
            StringAssert.Contains("r#type", rich);
            StringAssert.Contains(Token("'x'", Literal), rich);
            StringAssert.Contains(Token("return", Keyword), rich);
            Assert.AreEqual(code, Visible(rich));
        }

        [TestCase("kotlin")]
        [TestCase("swift")]
        public void EscapedIdentifierNamesAreNotKeywordsOrStrings(string language)
        {
            Assert.AreEqual("<color=#C9D4EA>`return`</color>", MNNMarkdownRenderer.ToCodeRichText("`return`", language));
        }

        [TestCase("cpp", "0x1.8p+2")]
        [TestCase("java", "0x1.ffp-3D")]
        [TestCase("go", "0x1.8p+2")]
        [TestCase("swift", "0xC.3p0")]
        [TestCase("rust", "1_000u64")]
        [TestCase("rust", "3.5f32")]
        [TestCase("java", "42L")]
        [TestCase("kotlin", "42UL")]
        [TestCase("r", "3.5i")]
        [TestCase("css", "12.5rem")]
        [TestCase("css", "#a1b2c3")]
        [TestCase("css", "-12px")]
        [TestCase("css", "+.5em")]
        [TestCase("toml", "-1.5e+2")]
        public void ExtendedNumericFormsStayInOneColoredToken(string language, string literal)
        {
            Assert.AreEqual("<color=#C9D4EA>" + Token(literal, Number) + "</color>", MNNMarkdownRenderer.ToCodeRichText(literal, language));
        }

        [TestCase("cpp")]
        [TestCase("rust")]
        [TestCase("kotlin")]
        [TestCase("swift")]
        [TestCase("lua")]
        public void RangeAndConcatenationDotsAreNotPartOfTheNextNumber(string language)
        {
            string rich = MNNMarkdownRenderer.ToCodeRichText("1..2", language);
            Assert.AreEqual("<color=#C9D4EA>" + Token("1", Number) + ".." + Token("2", Number) + "</color>", rich);
        }

        [Test]
        public void MarkupDistinguishesTagsAttributeStringsCommentsAndLiteralText()
        {
            const string code = "<item n=\"return 42\">text 42 &amp;</item><!-- return 42 --> <![CDATA[<b>42</b>]]>";
            string rich = MNNMarkdownRenderer.ToCodeRichText(code, "xml");
            Assert.AreEqual(code, Visible(rich));
            StringAssert.Contains(Token("<item", Keyword), rich);
            StringAssert.Contains(Token("\"return 42\"", Literal), rich);
            StringAssert.Contains("text 42 &amp;", rich);
            StringAssert.Contains(Token("<!-- return 42 -->", Comment), rich);
            StringAssert.Contains(Token("<![CDATA[<b>42</b>]]>", Literal), rich);
            StringAssert.DoesNotContain("<b>", rich);
        }

        [Test]
        public void CssSelectorsAndPropertiesDoNotEnableSlashComments()
        {
            const string code = "#main { color: #abcdef; width: 12px; content: \"<b>42</b>\"; /* note */ }";
            string rich = MNNMarkdownRenderer.ToCodeRichText(code, "css");
            StringAssert.Contains("#main", rich);
            StringAssert.Contains(Token("color", Property), rich);
            StringAssert.Contains(Token("#abcdef", Number), rich);
            StringAssert.Contains(Token("12px", Number), rich);
            StringAssert.Contains(Token("/* note */", Comment), rich);
            Assert.AreEqual(code, Visible(rich));
        }

        [Test]
        public void YamlBlockScalarsAndHashInsidePlainWordsPreserveBoundaries()
        {
            const string scalar = "|-\n  true # text\n  nested: 42\n";
            string rich = MNNMarkdownRenderer.ToCodeRichText("body: " + scalar + "next: true\nurl: abc#word # note", "yaml");
            StringAssert.Contains(Token(scalar, Literal), rich);
            StringAssert.Contains(Token("next", Property), rich);
            StringAssert.Contains(Token("true", Keyword), rich);
            StringAssert.Contains("abc#word " + Token("# note", Comment), rich);
        }

        [Test]
        public void YamlQuotesInsidePlainScalarsDoNotHideTheFollowingComment()
        {
            const string code = "title: can't # note\nurl: https://host/path#part\nfoo:bar";
            string rich = MNNMarkdownRenderer.ToCodeRichText(code, "yaml");
            StringAssert.Contains("can't " + Token("# note", Comment), rich);
            StringAssert.DoesNotContain(Token("foo", Property), rich);
            StringAssert.DoesNotContain(Token("https", Property), rich);
            StringAssert.DoesNotContain(Token("#part", Comment), rich);
            Assert.AreEqual(code, Visible(rich));
        }

        [Test]
        public void YamlHashAfterPunctuationInsideAWordIsNotAComment()
        {
            string rich = MNNMarkdownRenderer.ToCodeRichText("title: abc;#word # note", "yaml");
            StringAssert.Contains("abc;#word " + Token("# note", Comment), rich);
        }

        [TestCase("\"\"\"text\"\"\"\"")]
        [TestCase("\"\"\"text\"\"\"\"\"")]
        [TestCase("'''text''''")]
        [TestCase("'''text'''''")]
        public void TomlMultilineClosingQuoteRunsIncludeOneOrTwoContentQuotes(string literal)
        {
            string rich = MNNMarkdownRenderer.ToCodeRichText(literal + " # tail\ntrue", "toml");
            StringAssert.Contains(Token(literal, Literal), rich);
            StringAssert.Contains(Token("# tail", Comment), rich);
            StringAssert.Contains(Token("true", Keyword), rich);
        }

        [TestCase("body: |2-\r\n  true # text\r\nnext: false", "|2-\r\n  true # text\r\n")]
        [TestCase("body: >+\n\n    quoted: 42\n\nnext: false", ">+\n\n    quoted: 42\n\n")]
        public void YamlScalarIndentationAndLineEndingsStopAtTheNextKey(string code, string scalar)
        {
            string rich = MNNMarkdownRenderer.ToCodeRichText(code, "yml");
            StringAssert.Contains(Token(scalar, Literal), rich);
            StringAssert.Contains(Token("next", Property), rich);
            StringAssert.Contains(Token("false", Keyword), rich);
            Assert.AreEqual(code, Visible(rich));
        }

        [Test]
        public void YamlMalformedScalarHeadersDoNotRescanTheirEntireLine()
        {
            string code = new string('|', 200000);
            var timer = System.Diagnostics.Stopwatch.StartNew();
            string rich = MNNMarkdownRenderer.ToCodeRichText(code, "yaml");
            timer.Stop();
            Assert.AreEqual(code, Visible(rich));
            Assert.Less(timer.Elapsed.TotalSeconds, 2d, "A malformed YAML indicator must not rescan its line prefix.");
        }

        [TestCase("php", "RETURN", "#D4B0FF")]
        [TestCase("r", "return.value", null)]
        [TestCase("go", "deferred", null)]
        [TestCase("java", "returnValue", null)]
        public void KeywordCaseAndIdentifierBoundariesFollowTheLanguage(string language, string source, string color)
        {
            Assert.AreEqual("<color=#C9D4EA>" + (color == null ? source : Token(source, color)) + "</color>",
                MNNMarkdownRenderer.ToCodeRichText(source, language));
        }

        [Test]
        public void MarkupAttributeDelimitersRemainInsideTheirQuotedValue()
        {
            const string literal = "\"</color> > <!-- 42 -->\"";
            string code = "<svg:title xml:lang=" + literal + ">42</svg:title>";
            string rich = MNNMarkdownRenderer.ToCodeRichText(code, "svg");
            StringAssert.Contains(Token(literal, Literal), rich);
            StringAssert.Contains(Token("xml:lang", Property), rich);
            StringAssert.Contains(">42", Visible(rich));
            Assert.AreEqual(code, Visible(rich));
            AssertBalanced(rich);
        }

        [TestCase("js")]
        [TestCase("python")]
        [TestCase("lua")]
        public void EscapedCrLfIsOneContinuationInsideAString(string language)
        {
            const string literal = "\"return\\\r\n42 // # text\"";
            string rich = MNNMarkdownRenderer.ToCodeRichText(literal, language);
            Assert.AreEqual("<color=#C9D4EA>" + Token(literal, Literal) + "</color>", rich);
        }

        [TestCase("rust")]
        [TestCase("kotlin")]
        [TestCase("swift")]
        [TestCase("dart")]
        public void DeeplyNestedCommentsUseBoundedStackSpace(string language)
        {
            string comment = string.Concat(Enumerable.Repeat("/*", 4096)) + "return 42" + string.Concat(Enumerable.Repeat("*/", 4096));
            string rich = MNNMarkdownRenderer.ToCodeRichText(comment + " return", language);
            StringAssert.Contains(Token(comment, Comment), rich);
            StringAssert.EndsWith(" " + Token("return", Keyword) + "</color>", rich);
        }

        [TestCaseSource(nameof(Languages))]
        public void ParallelConsumersKeepLanguageStateAndThemeLocal(string language)
        {
            const string code = "return true 42 \"text\" <item name='x'/>";
            Parallel.For(0, 16, index =>
            {
                bool light = index % 2 == 0;
                string rich = MNNMarkdownRenderer.ToCodeRichText(code, language, light ? MNNMarkdownTheme.Light : MNNMarkdownTheme.Dark);
                StringAssert.StartsWith(light ? "<color=#24344D>" : "<color=#C9D4EA>", rich);
                Assert.AreEqual(code, Visible(rich));
                Assert.Greater(Regex.Matches(rich, "<color=").Count, 1);
                AssertBalanced(rich);
            });
        }

        [TestCaseSource(nameof(Languages))]
        public void EveryLanguagePreservesAllStreamingPrefixesAndRejectsCodeImages(string language)
        {
            const string code = "r##\"<b>😀\"## /* x /* y */ */\r\n'\\'' [=[![x](file:///x)]=] # note";
            for (int length = 0; length <= code.Length; ++length)
            {
                string prefix = code.Substring(0, length);
                string rich = MNNMarkdownRenderer.ToCodeRichText(prefix, language);
                Assert.AreEqual(prefix, Visible(rich), language + ":" + length);
                AssertBalanced(rich);
            }
            Assert.IsEmpty(MNNMarkdownRenderer.Render("```" + language + "\n" + code + "\n```").Images);
        }

        [TestCaseSource(nameof(Languages))]
        public void SeededMalformedInputsPreserveLiteralTextAndOnlyGenerateColorTags(string language)
        {
            var random = new Random(1729);
            const string alphabet = "a0_\"'`/#=*[]{}<>!\r\n\t\\\0\u200B\u0301\uD83D\uDE00";
            for (int sample = 0; sample < 64; ++sample)
            {
                var source = new StringBuilder();
                for (int i = 0; i < 160; ++i) source.Append(alphabet[random.Next(alphabet.Length)]);
                string code = source.ToString();
                string rich = MNNMarkdownRenderer.ToCodeRichText(code, language,
                    sample % 2 == 0 ? MNNMarkdownTheme.Dark : MNNMarkdownTheme.Light);
                Assert.AreEqual(code.Replace('\0', '\uFFFD'), Visible(rich), language + ":" + sample);
                AssertBalanced(rich);
                StringAssert.DoesNotContain("<", Regex.Replace(rich, @"</?color(?:=#[0-9A-Fa-f]{6})?>", "").Replace("<\u200B", ""));
            }
        }

        [TestCaseSource(nameof(Languages))]
        public void AdversarialPrefixesHaveABoundedRenderingBudget(string language)
        {
            foreach (string code in new[] { new string('"', 200000), new string('#', 200000), "r" + new string('#', 200000),
                "[" + new string('=', 200000), "/*" + new string('*', 200000) })
            {
                var timer = System.Diagnostics.Stopwatch.StartNew();
                string rich = MNNMarkdownRenderer.ToCodeRichText(code, language);
                timer.Stop();
                Assert.Less(timer.Elapsed.TotalSeconds, 8d, language + ": scanner repeated a suffix");
                Assert.AreEqual(code, Visible(rich));
            }
        }

        [TestCaseSource(nameof(Languages))]
        public void BothThemesAndSlicesPreserveExactlyTheSameCode(string language)
        {
            string code = "return true 42 \"<b>😀\"; // note\n" + new string('x', 512);
            string source = "```" + language + "\n" + code + "\n```";
            foreach (var theme in new[] { MNNMarkdownTheme.Dark, MNNMarkdownTheme.Light })
            {
                var slices = MNNMarkdownRenderer.RenderSlices(source, 17, 20, theme);
                Assert.AreEqual(code, string.Concat(slices.Select(slice => Visible(slice.RichText))));
                foreach (var slice in slices) AssertBalanced(slice.RichText);
            }
        }

        private static void AssertBalanced(string rich)
        {
            int depth = 0;
            foreach (Match match in Regex.Matches(rich, @"</?color(?:=#[0-9A-Fa-f]{6})?>"))
            {
                depth += match.Value[1] == '/' ? -1 : 1;
                Assert.GreaterOrEqual(depth, 0);
            }
            Assert.AreEqual(0, depth);
        }
    }
}
