using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public class MNNVersionTests
    {
        [Test]
        public void GetVersion_ReturnsEngineVersion() => StringAssert.StartsWith("3.6.", MNNVersion.GetVersion());
        [Test]
        public void IsLoaded_ReturnsTrue() => Assert.IsTrue(MNNVersion.IsLoaded());
        [Test]
        public void LogInfo_DoesNotThrow() => Assert.DoesNotThrow(MNNVersion.LogInfo);
    }
}
