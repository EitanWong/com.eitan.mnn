using Microsoft.Win32.SafeHandles;

namespace MNN.Unity.Interop.Handles
{
    internal sealed class LlmHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        // Accessed only under the owning MNNLlm lock. A replacement handle starts cold.
        internal bool ConversationCacheEnabled = true;
        internal bool ConversationCacheValid;
        // Load-time capability. Generation only changes reuse_kv/prompt_cache, not
        // speculative_type or has_talker. Replacement handles recompute this value.
        internal bool? ChatCacheSupported;
        internal LlmHandle(System.IntPtr pointer) : base(true)
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
