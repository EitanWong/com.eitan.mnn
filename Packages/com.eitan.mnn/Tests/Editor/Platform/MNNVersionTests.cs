using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public class MNNVersionTests
    {
        [Test]
        public void GetVersion_ReturnsEngineVersion() => Assert.AreEqual("3.6.2", MNNVersion.GetVersion());
        [Test]
        public void IsLoaded_ReturnsTrue() => Assert.IsTrue(MNNVersion.IsLoaded());
        [Test]
        public void LogInfo_DoesNotThrow() => Assert.DoesNotThrow(MNNVersion.LogInfo);
    }
}
