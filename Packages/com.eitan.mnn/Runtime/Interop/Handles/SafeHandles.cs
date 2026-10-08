using System;
using System.Runtime.InteropServices;
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

        protected override bool ReleaseHandle()
        {
            if (!IsInvalid)
            {
                MNNInterop.MNN_Interpreter_destroy(handle);
            }
            return true;
        }
    }

    /// <summary>
    /// MNN Session的安全句柄
    /// </summary>
    internal sealed class SessionHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        private readonly InterpreterHandle _interpreter;

        internal SessionHandle(InterpreterHandle interpreter) : base(true)
        {
            _interpreter = interpreter;
        }

        protected override bool ReleaseHandle()
        {
            if (!IsInvalid && _interpreter != null && !_interpreter.IsInvalid)
            {
                MNNInterop.MNN_Interpreter_releaseSession(_interpreter, handle);
            }
            return true;
        }
    }

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
