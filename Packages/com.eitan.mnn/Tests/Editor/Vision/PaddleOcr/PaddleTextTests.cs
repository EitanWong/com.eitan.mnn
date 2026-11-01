using System;
using System.IO;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public class PaddleTextTests
    {
        [Test]
        public void UniformImageGateComparesRgbPixelsWithoutIgnoringLowContrastGlyphs()
        {
            byte[] pixels = {31, 92, 201, 31, 92, 201};
            Assert.True(MNNPaddleTextRecognizer.IsUniform(new MNNRgbImage(pixels, 2, 1)));
            pixels[4] = 93;
            Assert.False(MNNPaddleTextRecognizer.IsUniform(new MNNRgbImage(pixels, 2, 1)));
        }

        [TestCase("ملاعلاب ابحرم", "مرحبا بالعالم")]
        [TestCase("2026ملاعلاب ابحرم", "مرحبا بالعالم2026")]
        [TestCase("ABC 123:/%+-*.", "ABC 123:/%+-*.")]
        [TestCase("ب🙂ا", "ا🙂ب")]
        [TestCase("", "")]
        public void ArabicLogicalOrderMatchesPaddleAndKeepsAsciiRuns(string visual, string logical)
        {
            Assert.AreEqual(logical, MNNPaddleTextRecognizer.ReverseArabic(visual));
        }

        [Test]
        public void CtcBlankSeparatesRepeatedSymbolsAndConfidenceExcludesBlank()
        {
            float[] values = {.01f,.9f,.09f, .01f,.8f,.19f, .9f,.05f,.05f, .01f,.7f,.29f, .01f,.09f,.9f};
            var result = MNNPaddleTextRecognizer.Decode(values, new[] {1,5,3}, new[] {"", "l", "!"});
            Assert.AreEqual("ll!", result.Text);
            Assert.That(result.Confidence, Is.EqualTo(2.5f/3).Within(1e-6));
            values[2] = float.NaN;
            Assert.Throws<InvalidDataException>(() => MNNPaddleTextRecognizer.Decode(values, new[] {1,5,3}, new[] {"", "l", "!"}));
            Assert.Throws<InvalidDataException>(() => MNNPaddleTextRecognizer.Decode(new float[6], new[] {1,2,3}, new[] {"", "x"}));
        }

        [Test]
        public void PreprocessingConvertsRgbToBgrWithZeroPaddingAndDynamicWidth()
        {
            var config = new MNNPaddleTextConfig {height=32, minimumWidth=8, maximumWidth=64};
            var pixels = new byte[32*3];
            for (int i=0;i<32;++i) pixels[i*3]=255;
            var result = MNNPaddleTextRecognizer.Preprocess(new MNNRgbImage(pixels,1,32),config,out int width);
            Assert.AreEqual(8,width);
            Assert.AreEqual(-1,result[0]);
            Assert.AreEqual(-1,result[256]);
            Assert.AreEqual(1,result[512]);
            Assert.AreEqual(0,result[1]);
            Assert.Throws<ArgumentException>(() => MNNPaddleTextRecognizer.Preprocess(new MNNRgbImage(new byte[300],100,1),config,out _));
        }
    }
}
