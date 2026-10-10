using NUnit.Framework;

namespace MNN.Unity.Tests
{
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
            var ex = Assert.Throws<MNNException>(() => MNNException.ThrowIfError(1, "TestOperation"));
            Assert.AreEqual(MNNErrorCode.OutOfMemory, ex.ErrorCode);
            Assert.IsTrue(ex.Message.Contains("TestOperation"));
        }

        [Test]
        public void ThrowIfError_AllErrorCodes_HaveMessages()
        {
            var errorCodes = new[]{1, 2, 3, 4, 5, 10, 11, 20, 21};
            foreach (var code in errorCodes)
            {
                var ex = Assert.Throws<MNNException>(() => MNNException.ThrowIfError(code, "Test"));
                Assert.IsNotNull(ex.Message);
                Assert.IsTrue(ex.Message.Length > 0);
            }
        }
    }
}
