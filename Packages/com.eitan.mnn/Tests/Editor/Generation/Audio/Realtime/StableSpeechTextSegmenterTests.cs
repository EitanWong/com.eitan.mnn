using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class StableSpeechTextSegmenterTests
    {
        [Test]
        public void SentenceIsEmittedAsSoonAsFollowingWhitespaceMakesItStable()
        {
            var segmenter = new MNNStableSpeechTextSegmenter();
            var segments = new List<string>();
            segmenter.Append("Hello.", value => Add(segments, value));
            Assert.AreEqual(0, segments.Count);
            segmenter.Append(" ", value => Add(segments, value));
            CollectionAssert.AreEqual(new[] { "Hello." }, segments);
            segmenter.Append("Next", value => Add(segments, value));
            Assert.True(segmenter.Complete(value => Add(segments, value)));
            CollectionAssert.AreEqual(new[] { "Hello.", "Next" }, segments);
        }

        [Test]
        public void DecimalAndCommonAbbreviationDoNotCreateFalseSentenceBoundaries()
        {
            var segmenter = new MNNStableSpeechTextSegmenter();
            var segments = new List<string>();
            segmenter.Append("Dr. Smith paid 3.", value => Add(segments, value));
            segmenter.Append("14 dollars. Next.", value => Add(segments, value));
            Assert.AreEqual(1, segments.Count);
            Assert.AreEqual("Dr. Smith paid 3.14 dollars.", segments[0]);
            segmenter.Complete(value => Add(segments, value));
            CollectionAssert.AreEqual(new[] { "Dr. Smith paid 3.14 dollars.", "Next." }, segments);
        }

        [Test]
        public void ClosingQuotesRemainAttachedToSentence()
        {
            var segmenter = new MNNStableSpeechTextSegmenter();
            var segments = new List<string>();
            segmenter.Append("She said \"go!\" then left.", value => Add(segments, value));
            CollectionAssert.AreEqual(new[] { "She said \"go!\"" }, segments);
            segmenter.Complete(value => Add(segments, value));
            CollectionAssert.AreEqual(new[] { "She said \"go!\"", "then left." }, segments);
        }

        [Test]
        public void SoftLimitPrefersWhitespaceAndHardLimitNeverSplitsSurrogatePair()
        {
            var spaced = new MNNStableSpeechTextSegmenter(maximumCharacters: 12);
            var segments = new List<string>();
            spaced.Append("one two three four", value => Add(segments, value));
            Assert.AreEqual("one two", segments[0]);
            spaced.Complete(value => Add(segments, value));
            CollectionAssert.AreEqual(new[] { "one two", "three four" }, segments);

            var unspaced = new MNNStableSpeechTextSegmenter(maximumCharacters: 8);
            segments.Clear();
            unspaced.Append("1234567\ud83d", value => Add(segments, value));
            Assert.AreEqual(0, segments.Count);
            unspaced.Append("\ude00x", value => Add(segments, value));
            Assert.AreEqual(1, segments.Count);
            Assert.AreEqual("1234567", segments[0]);
            unspaced.Complete(value => Add(segments, value));
            CollectionAssert.AreEqual(new[] { "1234567", "\ud83d\ude00x" }, segments);
        }

        [Test]
        public void TerminalCjkPunctuationAtCurrentStreamEndIsEmittedImmediately()
        {
            var segmenter = new MNNStableSpeechTextSegmenter();
            var segments = new List<string>();
            segmenter.Append("你好。", value => Add(segments, value));
            CollectionAssert.AreEqual(new[] { "你好。" }, segments);
            segmenter.Append("世界", value => Add(segments, value));
            segmenter.Complete(value => Add(segments, value));
            CollectionAssert.AreEqual(new[] { "你好。", "世界" }, segments);
        }

        [Test]
        public void CjkCommaRemainsAttachedUntilTheNextPhraseBegins()
        {
            var segmenter = new MNNStableSpeechTextSegmenter();
            var segments = new List<string>();
            segmenter.Append("你好，", value => Add(segments, value));
            Assert.AreEqual(0, segments.Count);
            segmenter.Append("世界", value => Add(segments, value));
            CollectionAssert.AreEqual(new[] { "你好，" }, segments);
        }

        [Test]
        public void ConsumerBackpressureCanBeRetriedWithoutLosingStableSegment()
        {
            var segmenter = new MNNStableSpeechTextSegmenter();
            Assert.False(segmenter.Append("Stable. ", _ => false));
            Assert.AreEqual(8, segmenter.PendingCharacters);
            Assert.Throws<InvalidOperationException>(() => segmenter.Append("ignored", _ => true));
            var segments = new List<string>();
            Assert.True(segmenter.DrainStableSegments(value => Add(segments, value)));
            CollectionAssert.AreEqual(new[] { "Stable." }, segments);
            Assert.True(segmenter.Append("Next. ", value => Add(segments, value)));
        }

        [Test]
        public void CompletionIsIdempotentAndRejectsFurtherInput()
        {
            var segmenter = new MNNStableSpeechTextSegmenter();
            var segments = new List<string>();
            segmenter.Append("unfinished", value => Add(segments, value));
            Assert.True(segmenter.Complete(value => Add(segments, value)));
            Assert.True(segmenter.Complete(value => Add(segments, value)));
            Assert.AreEqual(1, segments.Count);
            Assert.AreEqual("unfinished", segments[0]);
            Assert.Throws<InvalidOperationException>(() => segmenter.Append("late", value => Add(segments, value)));
        }

        [Test]
        public void InvalidConfigurationAndNullCallbacksAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MNNStableSpeechTextSegmenter(7));
            var segmenter = new MNNStableSpeechTextSegmenter();
            Assert.Throws<ArgumentNullException>(() => segmenter.Append("text", null));
            Assert.Throws<ArgumentNullException>(() => segmenter.Complete(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => segmenter.Append(
                new string('x', MNNStableSpeechTextSegmenter.MaximumInputDeltaCharacters + 1), _ => true));
            Assert.AreEqual(0, segmenter.PendingCharacters);
        }

        private static bool Add(List<string> output, string segment)
        {
            output.Add(segment);
            return true;
        }
    }
}
