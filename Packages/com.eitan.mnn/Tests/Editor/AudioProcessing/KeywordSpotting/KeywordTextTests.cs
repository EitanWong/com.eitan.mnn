using System;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public class KeywordTextTests
    {
        private static string Prepare(string text) => MNNKeywordText.Prepare(text);

        [TestCase("小爱同学", "x iǎo ài t óng x ué @小爱同学")]
        [TestCase("你好问问", "n ǐ h ǎo w èn w èn @你好问问")]
        [TestCase("重庆银行", "ch óng q ìng y ín h áng @重庆银行")]
        [TestCase("女儿", "n ǚ ér @女儿")]
        [TestCase("文森特卡索", "w én s ēn t è k ǎ s uǒ @文森特卡索")]
        public void OrdinaryChineseTextBecomesModelInput(string text, string expected)
        {
            Assert.AreEqual(expected, Prepare(text));
        }

        [Test]
        public void MultipleLinesTrimWhitespaceAndPreserveLabels()
        {
            Assert.AreEqual("n ǐ h ǎo @你好\nx iǎo ài @小爱", Prepare(" 你好 \r\n\n 小爱 "));
        }

        [TestCase("hello assistant"), TestCase("你好 Siri"), TestCase("123"), TestCase("こんにちは")]
        public void UnsupportedLanguagesCannotSilentlyReachChineseModel(string text)
        {
            Assert.Throws<ArgumentException>(() => Prepare(text));
        }

        [TestCase(""), TestCase(" \n "), TestCase("你好\n你好"), TestCase("你好！"), TestCase("你好\0")]
        [TestCase("hello @world"), TestCase("你好@世界"), TestCase("x iǎo ài :NaN @小爱")]
        public void InvalidWakeWordsAreRejectedBeforeInference(string text)
        {
            Assert.Throws<ArgumentException>(() => Prepare(text));
        }

        [Test]
        public void ExistingTokenSpecificationsRemainCompatible()
        {
            const string legacy = "x iǎo ài t óng x ué :1.5 #0.35 @小爱同学";
            Assert.AreEqual(legacy, Prepare(legacy));
            Assert.AreEqual("小爱同学", MNNKeywordText.GetDisplayText(legacy));
            Assert.AreEqual("你好\n小爱", MNNKeywordText.GetDisplayText("你好\n小爱"));
            Assert.AreEqual("", MNNKeywordText.GetDisplayText(null));
        }

        [Test]
        public void ExplicitLanguageMustMatchTheAvailableModel()
        {
            Assert.AreEqual(Prepare("小爱同学"), MNNKeywordText.Prepare("小爱同学", MNNKeywordLanguage.Chinese));
            Assert.Throws<ArgumentException>(() => MNNKeywordText.Prepare("hello", MNNKeywordLanguage.English));
            Assert.Throws<ArgumentOutOfRangeException>(() => MNNKeywordText.Prepare("你好", (MNNKeywordLanguage)99));
        }

        [Test]
        public void PhraseAndInputBudgetsAreCheckedBeforeInference()
        {
            Assert.Throws<ArgumentException>(() => Prepare(new string('你', 65)));
            Assert.Throws<ArgumentException>(() => Prepare(new string('你', 32769)));
            Assert.Throws<ArgumentException>(() => Prepare(null));
        }

        [Test]
        public void HomophonesAndLegacyDuplicatesHaveOnePronunciationOwner()
        {
            Assert.Throws<ArgumentException>(() => Prepare("小爱\n小艾"));
            Assert.Throws<ArgumentException>(() => Prepare("小爱\nx iǎo ài @小爱"));
        }
    }
}
