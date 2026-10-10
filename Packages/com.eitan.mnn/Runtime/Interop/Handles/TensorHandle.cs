using Microsoft.Win32.SafeHandles;

namespace MNN.Unity.Interop.Handles
{
    /// <summary>
    /// MNN Tensor的句柄（不拥有内存，不需要释放）
    /// </summary>
    internal sealed class TensorHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        internal TensorHandle() : base(false)
        {
        }

        protected override bool ReleaseHandle()
        {
            // Tensor由Interpreter/Session管理，不需要手动释放
            return true;
        }
    }
}
