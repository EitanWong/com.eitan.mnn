using System;
using Microsoft.Win32.SafeHandles;

namespace MNN.Unity.Interop.Handles
{
    /// <summary>
    /// MNN Interpreter的安全句柄
    /// </summary>
    internal sealed class InterpreterHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        private InterpreterHandle() : base(true)
        {
        }

        internal InterpreterHandle(IntPtr pointer) : base(true)
        {
            SetHandle(pointer);
        }

        protected override bool ReleaseHandle()
        {
            if (!IsInvalid)
            {
                MNNInterop.MNN_Interpreter_destroy(handle);
            }

            return true;
        }
    }
}
