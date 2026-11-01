using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [Category("Integration"), Category("AvTfGridNet")]
    public sealed class AvTfGridNetSourceParityTests
    {
        [Test]
        public void PublicApiHasCompleteTargetExtraction()
        {
            var type = typeof(MNNBackendType).Assembly.GetType("MNN.Unity.MNNAvTfGridNetExtraction");
            Assert.NotNull(type, "AV-TFGridNet requires the complete waveform extraction API.");
            Assert.NotNull(type.GetMethod("Extract"));
            Assert.NotNull(type.GetMethod("ExtractWaveFile"));
        }
    }
}
