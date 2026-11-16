using System;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class RecognitionQualityTests
    {
        [Test]
        public void SelectsLowestMeasuredErrorOnTheSameDeviceLanguageAndCorpus()
        {
            var slow = new MNNRecognitionQuality("slow", "set-v1", "device-a", "zh", "CER", 1, 100, 500, true);
            var good = new MNNRecognitionQuality("good", "set-v1", "device-a", "zh", "CER", 2, 100, 80, true);
            var bad = new MNNRecognitionQuality("bad", "set-v1", "device-a", "zh", "CER", 9, 100, 20, true);
            Assert.That(MNNRecognitionQuality.Select(new[] { bad, good, slow }, "set-v1", "device-a", "zh", "CER", 200)?.ConfigurationId,
                Is.EqualTo("good"));
        }

        [Test]
        public void UnverifiedAndIncomparableMeasurementsCannotBecomeDefaults()
        {
            var unverified = new MNNRecognitionQuality("unverified", "set-v1", "device-a", "zh", "CER", 0, 100, 10, false);
            var different = new MNNRecognitionQuality("other", "set-v2", "device-a", "zh", "CER", 0, 100, 10, true);
            Assert.That(MNNRecognitionQuality.Select(new[] { unverified, different }, "set-v1", "device-a", "zh", "CER", 200), Is.Null);
            Assert.Throws<ArgumentOutOfRangeException>(() => new MNNRecognitionQuality("invalid", "set", "device", "zh", "CER", 0, 0, 1, true));
        }
    }
}
