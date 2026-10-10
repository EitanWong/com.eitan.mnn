using NUnit.Framework;

namespace MNN.Unity.Tests
{
    /// <summary>
    /// MNN会话配置测试
    /// </summary>
    public class MNNSessionConfigTests
    {
        [Test]
        public void Constructor_CreatesDefaultConfig()
        {
            var config = new MNNSessionConfig();
            Assert.AreEqual(MNNBackendType.Auto, config.BackendType);
            Assert.AreEqual(4, config.ThreadCount);
            Assert.AreEqual(MNNBackendType.CPU, config.BackupBackendType);
            Assert.AreEqual(MNNMemoryMode.Normal, config.MemoryMode);
            Assert.AreEqual(MNNPowerMode.Normal, config.PowerMode);
            Assert.AreEqual(MNNPrecisionMode.Normal, config.PrecisionMode);
        }

        [Test]
        public void CreateForCurrentPlatform_ReturnsValidConfig()
        {
            var config = MNNSessionConfig.CreateForCurrentPlatform();
            Assert.IsNotNull(config);
            Assert.IsTrue(config.ThreadCount > 0);
            Assert.IsTrue(config.ThreadCount <= 16); // Reasonable upper bound
        }

        [Test]
        public void CreateHighPerformance_HasCorrectSettings()
        {
            var config = MNNSessionConfig.CreateHighPerformance();
            Assert.AreEqual(MNNPowerMode.High, config.PowerMode);
            Assert.AreEqual(MNNPrecisionMode.Low, config.PrecisionMode);
        }

        [Test]
        public void CreateLowPower_HasCorrectSettings()
        {
            var config = MNNSessionConfig.CreateLowPower();
            Assert.AreEqual(MNNPowerMode.Low, config.PowerMode);
            Assert.IsTrue(config.ThreadCount <= 2);
        }

        [Test]
        public void CreateHighPrecision_HasCorrectSettings()
        {
            var config = MNNSessionConfig.CreateHighPrecision();
            Assert.AreEqual(MNNPrecisionMode.High, config.PrecisionMode);
        }

        [Test]
        public void Clone_CreatesIdenticalCopy()
        {
            var original = new MNNSessionConfig{BackendType = MNNBackendType.OpenCL, ThreadCount = 8, MemoryMode = MNNMemoryMode.High};
            var clone = original.Clone();
            Assert.AreEqual(original.BackendType, clone.BackendType);
            Assert.AreEqual(original.ThreadCount, clone.ThreadCount);
            Assert.AreEqual(original.MemoryMode, clone.MemoryMode);
            // Verify it's a different instance
            clone.ThreadCount = 4;
            Assert.AreNotEqual(original.ThreadCount, clone.ThreadCount);
        }

        [Test]
        public void ToString_ReturnsInformativeString()
        {
            var config = new MNNSessionConfig{BackendType = MNNBackendType.Metal, ThreadCount = 4};
            var str = config.ToString();
            Assert.IsTrue(str.Contains("Metal"));
            Assert.IsTrue(str.Contains("4"));
        }
    }
}
