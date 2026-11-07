using System;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public class EnglishKeywordTextTests
    {
        [TestCase("Siri", "▁S I RI @Siri")]
        [TestCase("Hey Siri", "▁HE Y ▁S I RI @Hey Siri")]
        [TestCase("hello world", "▁HE LL O ▁WORLD @hello world")]
        public void TypedEnglishMatchesPublishedSentencePieceEncoding(string phrase, string expected)
        {
            Assert.AreEqual(expected, MNNKeywordText.Prepare(phrase, MNNKeywordLanguage.English));
            Assert.AreEqual(expected, MNNKeywordText.Prepare(phrase));
        }

        [Test]
        public void WrongLanguageAndMixedPhraseAreRejected()
        {
            Assert.Throws<ArgumentException>(() => MNNKeywordText.Prepare("Siri", MNNKeywordLanguage.Chinese));
            Assert.Throws<ArgumentException>(() => MNNKeywordText.Prepare("你好", MNNKeywordLanguage.English));
            Assert.Throws<ArgumentException>(() => MNNKeywordText.Prepare("你好 Siri"));
        }

        [Test]
        public void CaseInsensitiveDuplicatesAreRejected()
        {
            Assert.Throws<ArgumentException>(() => MNNKeywordText.Prepare("Siri\nsiri", MNNKeywordLanguage.English));
        }
    }
}
