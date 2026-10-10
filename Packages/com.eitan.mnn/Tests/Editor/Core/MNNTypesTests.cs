using NUnit.Framework;

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
}
