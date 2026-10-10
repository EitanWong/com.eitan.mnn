using Microsoft.Win32.SafeHandles;

namespace MNN.Unity.Interop.Handles
{
    internal sealed class EmbeddingHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        internal EmbeddingHandle(System.IntPtr pointer) : base(true)
        {
            SetHandle(pointer);
        }

        protected override bool ReleaseHandle()
        {
            MNNInterop.MNN_Llm_destroy(handle);
            return true;
        }
    }
}
