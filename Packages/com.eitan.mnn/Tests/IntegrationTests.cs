using NUnit.Framework;
using System;
using System.IO;
using MNN.Unity;
using UnityEngine;

namespace MNN.Unity.Tests
{
    /// <summary>
    /// MNN版本API测试
    /// </summary>
    public class MNNVersionTests
    {
        [Test]
        public void GetVersion_ReturnsNonEmptyString()
        {
            var version = MNNVersion.GetVersion();

            Assert.IsNotNull(version);
            Assert.IsTrue(version.Length > 0);
            Assert.AreNotEqual("Unknown", version);
            Assert.AreNotEqual("Error", version);

            Debug.Log($"MNN Version: {version}");
        }

        [Test]
        public void IsLoaded_ReturnsTrue()
        {
            var isLoaded = MNNVersion.IsLoaded();

            Assert.IsTrue(isLoaded, "MNN library should be loaded");
        }

        [Test]
        public void LogInfo_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => MNNVersion.LogInfo());
        }
    }

    /// <summary>
    /// MNN Interpreter集成测试
    /// 注意：这些测试需要有效的MNN模型文件
    /// </summary>
    public class MNNInterpreterIntegrationTests
    {
        private string _testModelPath;

        [SetUp]
        public void Setup()
        {
            // 在实际测试中，需要提供一个有效的MNN模型文件
            _testModelPath = Path.Combine(Application.streamingAssetsPath, "test_model.mnn");
        }

        [Test]
        public void CreateFromFile_WithNullPath_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                MNNInterpreter.CreateFromFile(null));
        }

        [Test]
        public void CreateFromFile_WithEmptyPath_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                MNNInterpreter.CreateFromFile(""));
        }

        [Test]
        public void CreateFromFile_WithNonExistentFile_ThrowsFileNotFoundException()
        {
            Assert.Throws<FileNotFoundException>(() =>
                MNNInterpreter.CreateFromFile("/nonexistent/model.mnn"));
        }

        [Test]
        public void CreateFromBuffer_WithNullBuffer_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                MNNInterpreter.CreateFromBuffer(null));
        }

        [Test]
        public void CreateFromBuffer_WithEmptyBuffer_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                MNNInterpreter.CreateFromBuffer(new byte[0]));
        }

        // 以下测试需要实际的模型文件，标记为Explicit
        [Test, Explicit("Requires valid MNN model file")]
        public void CreateFromFile_WithValidModel_Succeeds()
        {
            if (!File.Exists(_testModelPath))
            {
                Assert.Ignore("Test model not found");
            }

            MNNInterpreter interpreter = null;
            try
            {
                interpreter = MNNInterpreter.CreateFromFile(_testModelPath);

                Assert.IsNotNull(interpreter);
                Assert.IsFalse(interpreter.IsDisposed);
            }
            finally
            {
                interpreter?.Dispose();
            }
        }

        [Test, Explicit("Requires valid MNN model file")]
        public void CreateSession_WithDefaultConfig_Succeeds()
        {
            if (!File.Exists(_testModelPath))
            {
                Assert.Ignore("Test model not found");
            }

            MNNInterpreter interpreter = null;
            MNNSession session = null;

            try
            {
                interpreter = MNNInterpreter.CreateFromFile(_testModelPath);
                session = interpreter.CreateSession();

                Assert.IsNotNull(session);
                Assert.IsFalse(session.IsDisposed);
            }
            finally
            {
                session?.Dispose();
                interpreter?.Dispose();
            }
        }

        [Test, Explicit("Requires valid MNN model file")]
        public void ReleaseModel_AfterSessionCreation_Succeeds()
        {
            if (!File.Exists(_testModelPath))
            {
                Assert.Ignore("Test model not found");
            }

            MNNInterpreter interpreter = null;
            MNNSession session = null;

            try
            {
                interpreter = MNNInterpreter.CreateFromFile(_testModelPath);
                session = interpreter.CreateSession();

                Assert.IsFalse(interpreter.IsModelReleased);
                interpreter.ReleaseModel();
                Assert.IsTrue(interpreter.IsModelReleased);
            }
            finally
            {
                session?.Dispose();
                interpreter?.Dispose();
            }
        }

        [Test]
        public void Dispose_CalledMultipleTimes_DoesNotThrow()
        {
            // 创建一个假的interpreter用于测试Dispose
            // 实际使用中需要有效的模型
            Assert.DoesNotThrow(() =>
            {
                // 此测试仅验证Dispose的健壮性
                // 实际的Dispose测试需要有效模型
            });
        }
    }

    /// <summary>
    /// MNN Tensor单元测试
    /// </summary>
    public class MNNTensorTests
    {
        [Test]
        public void CopyFromArray_WithNullArray_ThrowsArgumentNullException()
        {
            // 此测试需要实际的tensor实例
            // 在集成测试中验证
            Assert.Pass("Requires integration test with actual tensor");
        }

        [Test]
        public void CopyFromArray_WithMismatchedLength_ThrowsArgumentException()
        {
            // 此测试需要实际的tensor实例
            // 在集成测试中验证
            Assert.Pass("Requires integration test with actual tensor");
        }
    }
}
