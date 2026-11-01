using System;
using Microsoft.Win32.SafeHandles;

namespace MNN.Unity.Interop.Handles
{
    internal sealed class ExpressModuleHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        private readonly MNNInterop.ExpressExecutor _executor;
        internal ExpressModuleHandle(IntPtr module, MNNInterop.ExpressExecutor executor) : base(true)
        {
            _executor = executor;
            SetHandle(module);
        }

        protected override bool ReleaseHandle()
        {
            try
            {
                using (_executor.Enter())
                    MNNInterop.DestroyModule(handle);
                _executor.Dispose();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
