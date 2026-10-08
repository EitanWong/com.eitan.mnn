using System;

namespace MNN.Unity
{
    /// <summary>
    /// MNN运行时异常
    /// </summary>
    public class MNNException : Exception
    {
        /// <summary>
        /// 错误码
        /// </summary>
        public MNNErrorCode ErrorCode { get; }

        /// <summary>
        /// 创建MNN异常
        /// </summary>
        /// <param name="errorCode">错误码</param>
        /// <param name="message">错误消息</param>
        public MNNException(MNNErrorCode errorCode, string message)
            : base($"[MNN Error {errorCode}] {message}")
        {
            ErrorCode = errorCode;
        }

        /// <summary>
        /// 创建MNN异常
        /// </summary>
        /// <param name="errorCode">错误码</param>
        /// <param name="message">错误消息</param>
        /// <param name="innerException">内部异常</param>
        public MNNException(MNNErrorCode errorCode, string message, Exception innerException)
            : base($"[MNN Error {errorCode}] {message}", innerException)
        {
            ErrorCode = errorCode;
        }

        /// <summary>
        /// 检查错误码并抛出异常
        /// </summary>
        /// <param name="errorCode">错误码</param>
        /// <param name="operation">操作名称</param>
        public static void ThrowIfError(int errorCode, string operation)
        {
            if (errorCode == 0) return;

            var code = (MNNErrorCode)errorCode;
            var message = GetErrorMessage(code, operation);
            throw new MNNException(code, message);
        }

        /// <summary>
        /// 获取错误消息
        /// </summary>
        private static string GetErrorMessage(MNNErrorCode code, string operation)
        {
            return code switch
            {
                MNNErrorCode.OutOfMemory => $"{operation} failed: Out of memory",
                MNNErrorCode.NotSupport => $"{operation} failed: Operation not supported",
                MNNErrorCode.ComputeSizeError => $"{operation} failed: Compute size error",
                MNNErrorCode.NoExecution => $"{operation} failed: No execution available",
                MNNErrorCode.InvalidValue => $"{operation} failed: Invalid value",
                MNNErrorCode.InputDataError => $"{operation} failed: Input data error",
                MNNErrorCode.CallBackStop => $"{operation} stopped by callback",
                MNNErrorCode.TensorNotSupport => $"{operation} failed: Tensor not supported",
                MNNErrorCode.TensorNeedDivide => $"{operation} failed: Tensor needs divide",
                _ => $"{operation} failed with error code {code}"
            };
        }
    }
}
