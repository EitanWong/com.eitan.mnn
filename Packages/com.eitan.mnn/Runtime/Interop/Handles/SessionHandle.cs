using System;
using Microsoft.Win32.SafeHandles;

namespace MNN.Unity.Interop.Handles
{
    /// <summary>
    /// MNN Session的安全句柄
    /// </summary>
    internal sealed class SessionHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        private readonly InterpreterHandle _interpreter;
        private bool _retained;
        internal SessionHandle(InterpreterHandle interpreter, IntPtr handle) : base(true)
        {
            _interpreter = interpreter;
            interpreter.DangerousAddRef(ref _retained);
            SetHandle(handle);
        }

        protected override bool ReleaseHandle()
        {
            try
            {
                if (!IsInvalid && _retained)
                    MNNInterop.MNN_Interpreter_releaseSession(_interpreter.DangerousGetHandle(), handle);
            }
            finally
            {
                if (_retained)
                {
                    _interpreter.DangerousRelease();
                    _retained = false;
                }
            }

            return true;
        }
    }
}
