using System;
using System.Collections;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MNN.Unity.Tests
{
    public sealed class SpeechTextCleanerTests
    {
        [TestCase(null, "")]
        [TestCase("", "")]
        [TestCase("Hello, world! 你好，世界！", "Hello, world! 你好，世界！")]
        [TestCase("## 标题\n- **第一项**\n2. _第二项_\n> 引用", "标题 第一项 第二项 引用")]
        [TestCase("<en>Hello.</en><|endoftext|>", "Hello.")]
        [TestCase("<ko>안녕하세요.</ko>", "안녕하세요.")]
        [TestCase("<b>Hello</b><i>world.</i>", "Hello world.")]
        [TestCase("<span class=\"voice\">正文</span>", "正文")]
        [TestCase("正文<script>alert('秘密');</script><style>body{color:red;}</style>结束。", "正文 结束。")]
        [TestCase("<think>不要朗读推理。</think>回答。", "回答。")]
        [TestCase("<!-- internal > hidden -->正文", "正文")]
        [TestCase("前文。\n```csharp\nDebug.Log(\"秘密\");\n```\n后文。", "前文。 后文。")]
        [TestCase("前文。~~~python\nprint('秘密')\n~~~后文。", "前文。 后文。")]
        [TestCase("Use `Console.WriteLine()` here.", "Use here.")]
        [TestCase("Use ``code with ` inside`` here.", "Use here.")]
        [TestCase("[帮助文档](https://example.com/a_(b))，继续。", "帮助文档 ，继续。")]
        [TestCase("[文档][ref]\n[ref]: https://example.com\n正文。", "文档 正文。")]
        [TestCase("![不要朗读图片地址](https://example.com/a.png)正文。", "正文。")]
        [TestCase("详情 https://example.com/a?x=1 和 www.example.com，请看。", "详情 和 ，请看。")]
        [TestCase("Tom &amp; Jerry&nbsp;你好。", "Tom Jerry 你好。")]
        [TestCase("&lt;en&gt;Hello.&lt;/en&gt;", "Hello.")]
        [TestCase("价格 $12.50，折扣 25%。", "价格 12.50，折扣 25 。")]
        [TestCase("公式 $x^2+y$ 和 \\(x=1\\) 已略去。", "公式 和 已略去。")]
        [TestCase("前文 $$x=1$$ 后文。", "前文 后文。")]
        [TestCase("2 < 3; 5 > 4.", "2 3; 5 4.")]
        [TestCase("😀🚀✅ # * _ | {} <> \u200b\0", "")]
        [TestCase("你好😀，世界🚀！", "你好 ，世界 ！")]
        [TestCase("前文。```python\n未结束的代码", "前文。")]
        [TestCase("正文 <script>未闭合的代码", "正文")]
        [TestCase("正文 [未闭合的链接](https://secret", "正文")]
        [TestCase("const secret = 42;\n正文。", "正文。")]
        [TestCase("import data from the file.", "import data from the file.")]
        [TestCase("return home safely.", "return home safely.")]
        [TestCase("正常---破折号\n---\n正文。", "正常---破折号 正文。")]
        [TestCase("正文\n    Debug.Log(\"秘密\");\n正文。", "正文 正文。")]
        [TestCase("正文\n\tprint('秘密')\n正文。", "正文 正文。")]
        [TestCase("正文 {\"secret\":{\"brace\":\"}\"}} 结束。", "正文 结束。")]
        [TestCase("\U00020000 café naïve", "\U00020000 café naïve")]
        public void CleanKeepsSpeechAndRemovesFormatting(string input, string expected)
        {
            Assert.AreEqual(expected, MNNSpeechTextCleaner.Clean(input));
            Assert.AreEqual(expected, MNNSpeechTextCleaner.Clean(expected), "Cleaning must be idempotent at the provider boundary.");
            if (input == null) return;

            // Every possible delta boundary must behave like a single complete reply.
            for (int split = 0; split <= input.Length; ++split)
            {
                var cleaner = new MNNSpeechTextCleaner();
                string spoken = cleaner.Append(input.Substring(0, split)) + cleaner.Append(input.Substring(split)) + cleaner.Complete();
                Assert.AreEqual(expected, Normalize(spoken), "Split at " + split);
            }
            var incremental = new MNNSpeechTextCleaner();
            var result = new StringBuilder();
            foreach (char character in input) result.Append(incremental.Append(character.ToString()));
            result.Append(incremental.Complete());
            Assert.AreEqual(expected, Normalize(result.ToString()), "One character per delta");
        }

        [Test]
        public void IncompleteCodeAndTagsNeverReachTheSpeechSegmenter()
        {
            var cleaner = new MNNSpeechTextCleaner();
            Assert.AreEqual("Hello. ", cleaner.Append("Hello. ```csharp\n"));
            Assert.AreEqual("", cleaner.Append("Debug.Log(\"秘密\");\n"));
            Assert.AreEqual("", cleaner.Append("``"));
            StringAssert.DoesNotContain("秘密", cleaner.Append("`\n<think>秘密</thi"));
            Assert.AreEqual(" 回答。", cleaner.Append("nk>回答。"));
            Assert.AreEqual("", cleaner.Complete());
        }

        [Test]
        public void CompletedCleanerCannotAcceptAnotherResponse()
        {
            var cleaner = new MNNSpeechTextCleaner();
            Assert.Throws<ArgumentNullException>(() => cleaner.Append(null));
            cleaner.Append("Hello");
            Assert.AreEqual("Hello", cleaner.Complete());
            Assert.AreEqual("", cleaner.Complete());
            Assert.Throws<InvalidOperationException>(() => cleaner.Append("next"));
        }

        [UnityTest]
        public IEnumerator BufferedLinkCanBeAppendedWithBoundedDeltasAndBackpressure() =>
            AsyncTest.Await(AppendBufferedLinkAsync());

        private static async Task AppendBufferedLinkAsync()
        {
            var output = new MNNRealtimeOutputPipeline(new MNNPcmFormat(16000, 1), framesPerBlock: 320);
            var response = output.BeginResponse();
            var channel = new MNNRealtimeSpeechTextChannel(output, response, capacity: 1, maximumSegmentCharacters: 120);
            var cleaner = new MNNSpeechTextCleaner();
            string label = new string('a', 4095) + "\U00020000" + new string('b', 1000);
            Assert.AreEqual("", cleaner.Append("[" + label));
            string speech = cleaner.Append("](https://example.com)") + cleaner.Complete();
            var received = new StringBuilder();
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
            {
                Task<bool> append = channel.AppendAsync(speech, timeout.Token);
                while (!append.IsCompleted)
                {
                    while (channel.TryRead(out var segment)) received.Append(segment.Text);
                    await Task.Delay(1, timeout.Token);
                }
                Assert.True(await append);
                while (!channel.Complete())
                    while (channel.TryRead(out var segment)) received.Append(segment.Text);
                while (channel.TryRead(out var segment)) received.Append(segment.Text);
            }
            Assert.AreEqual(label, received.ToString());
            Assert.Greater(channel.BackpressureCount, 0);
        }

        [UnityTest]
        public IEnumerator AsyncChannelAppendHonorsCancellationAndStaleResponses() =>
            AsyncTest.Await(AppendCancelledAndStaleResponsesAsync());

        private static async Task AppendCancelledAndStaleResponsesAsync()
        {
            var output = new MNNRealtimeOutputPipeline(new MNNPcmFormat(16000, 1), framesPerBlock: 320);
            var channel = new MNNRealtimeSpeechTextChannel(output, output.BeginResponse(), capacity: 1, maximumSegmentCharacters: 8);
            using (var cancellation = new CancellationTokenSource())
            {
                Task<bool> blocked = channel.AppendAsync(new string('a', 100), cancellation.Token);
                Assert.False(blocked.IsCompleted);
                cancellation.Cancel();
                try { await blocked; Assert.Fail("Expected cancellation while waiting for the consumer."); }
                catch (OperationCanceledException) { }
            }
            output.Interrupt();
            Assert.False(await channel.AppendAsync("late text"));
        }

        private static string Normalize(string text)
        {
            text = Regex.Replace(text, @"\s+", " ").Trim();
            for (int i = 0; i < text.Length; ++i)
                if (char.IsLetterOrDigit(text, i)) return text;
            return "";
        }
    }
}
