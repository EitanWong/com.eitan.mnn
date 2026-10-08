using NUnit.Framework;
using System;
using MNN.Unity;

namespace MNN.Unity.Tests
{
    /// <summary>
    /// MNN类型和枚举测试
    /// </summary>
    public class MNNTypesTests
    {
        [Test]
        public void BackendType_HasCorrectValues()
        {
            Assert.AreEqual(0, (int)MNNBackendType.CPU);
            Assert.AreEqual(1, (int)MNNBackendType.Metal);
            Assert.AreEqual(2, (int)MNNBackendType.CUDA);
            Assert.AreEqual(3, (int)MNNBackendType.OpenCL);
            Assert.AreEqual(4, (int)MNNBackendType.Auto);
        }

        [Test]
        public void ErrorCode_HasCorrectValues()
        {
            Assert.AreEqual(0, (int)MNNErrorCode.NoError);
            Assert.AreEqual(1, (int)MNNErrorCode.OutOfMemory);
            Assert.AreEqual(2, (int)MNNErrorCode.NotSupport);
        }

        [Test]
        public void DimensionType_HasCorrectValues()
        {
            Assert.AreEqual(0, (int)MNNDimensionType.TensorFlow);
            Assert.AreEqual(1, (int)MNNDimensionType.Caffe);
            Assert.AreEqual(2, (int)MNNDimensionType.CaffeC4);
        }
    }

    /// <summary>
    /// MNN异常测试
    /// </summary>
    public class MNNExceptionTests
    {
        [Test]
        public void Constructor_WithErrorCode_CreatesException()
        {
            var ex = new MNNException(MNNErrorCode.OutOfMemory, "Test message");

            Assert.AreEqual(MNNErrorCode.OutOfMemory, ex.ErrorCode);
            Assert.IsTrue(ex.Message.Contains("OutOfMemory"));
            Assert.IsTrue(ex.Message.Contains("Test message"));
        }

        [Test]
        public void ThrowIfError_WithNoError_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => MNNException.ThrowIfError(0, "Test"));
        }

        [Test]
        public void ThrowIfError_WithError_Throws()
        {
            var ex = Assert.Throws<MNNException>(() =>
                MNNException.ThrowIfError(1, "TestOperation"));

            Assert.AreEqual(MNNErrorCode.OutOfMemory, ex.ErrorCode);
            Assert.IsTrue(ex.Message.Contains("TestOperation"));
        }

        [Test]
        public void ThrowIfError_AllErrorCodes_HaveMessages()
        {
            var errorCodes = new[]
            {
                1, 2, 3, 4, 5, 10, 11, 20, 21
            };

            foreach (var code in errorCodes)
            {
                var ex = Assert.Throws<MNNException>(() =>
                    MNNException.ThrowIfError(code, "Test"));

                Assert.IsNotNull(ex.Message);
                Assert.IsTrue(ex.Message.Length > 0);
            }
        }
    }

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
            var original = new MNNSessionConfig
            {
                BackendType = MNNBackendType.OpenCL,
                ThreadCount = 8,
                MemoryMode = MNNMemoryMode.High
            };

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
            var config = new MNNSessionConfig
            {
                BackendType = MNNBackendType.Metal,
                ThreadCount = 4
            };

            var str = config.ToString();

            Assert.IsTrue(str.Contains("Metal"));
            Assert.IsTrue(str.Contains("4"));
        }
    }
}
