using System;
using System.Text;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class MNNStreamingTextTests
    {
        [TestCase("Hello world")]
        [TestCase("你好，Unity 😀 e\u0301!")]
        [TestCase("a\0b")]
        public void EveryUtf8SplitPreservesCumulativeText(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            for (int split = 0; split <= bytes.Length; ++split)
            {
                var decoder = new MNNStreamingText();
                var first = new byte[split];
                var second = new byte[bytes.Length - split];
                Array.Copy(bytes, first, split);
                Array.Copy(bytes, split, second, 0, second.Length);
                string prefix = decoder.Append(first);
                if (prefix != null)
                {
                    Assert.IsTrue(text.StartsWith(prefix, StringComparison.Ordinal));
                    Assert.AreEqual(-1, prefix.IndexOf('\ufffd'));
                }
                Assert.AreEqual(text, decoder.Append(second) ?? prefix);
            }
        }

        [Test]
        public void ByteAtATimeWithholdsIncompleteScalarsAndEmptyUpdates()
        {
            var decoder = new MNNStreamingText();
            Assert.IsNull(decoder.Append(Array.Empty<byte>()));
            string latest = null;
            foreach (byte value in Encoding.UTF8.GetBytes("你😀好"))
            {
                string update = decoder.Append(new[] { value });
                if (update != null)
                {
                    Assert.AreEqual(-1, update.IndexOf('\ufffd'));
                    latest = update;
                }
            }
            Assert.AreEqual("你😀好", latest);
            Assert.IsNull(decoder.Append(Array.Empty<byte>()));
        }

        [TestCase("Hello world")]
        [TestCase("你好，Unity 😀 e\u0301!")]
        [TestCase("a\0b")]
        public void DeltaModePreservesEveryUtf8SplitAndIgnoresUnusedBufferBytes(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            for (int split = 0; split <= bytes.Length; ++split)
            {
                var decoder = new MNNStreamingText(deltas: true);
                var joined = new StringBuilder();
                var buffer = new byte[bytes.Length + 16];
                for (int i = 0; i < buffer.Length; ++i) buffer[i] = 120;
                Array.Copy(bytes, 0, buffer, 0, split);
                string first = decoder.Append(buffer, split);
                if (first != null) joined.Append(first);
                Array.Copy(bytes, split, buffer, 0, bytes.Length - split);
                string second = decoder.Append(buffer, bytes.Length - split);
                if (second != null) joined.Append(second);
                Assert.AreEqual(text, joined.ToString(), "split=" + split);
                Assert.IsNull(decoder.Append(buffer, 0));
            }
        }

        [Test]
        public void DeltaModeWithholdsPartialScalarsAcrossReusedBufferUpdates()
        {
            var decoder = new MNNStreamingText(deltas: true);
            var buffer = new byte[8];
            var joined = new StringBuilder();
            int updates = 0;
            foreach (byte value in Encoding.UTF8.GetBytes("你😀好"))
            {
                buffer[0] = value;
                string delta = decoder.Append(buffer, 1);
                if (delta == null) continue;
                Assert.AreEqual(-1, delta.IndexOf('\ufffd'));
                joined.Append(delta);
                ++updates;
            }
            Assert.AreEqual("你😀好", joined.ToString());
            Assert.AreEqual(3, updates);
        }
    }
}
