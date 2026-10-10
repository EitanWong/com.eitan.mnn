using Microsoft.Win32.SafeHandles;

namespace MNN.Unity.Interop.Handles
{
    // Owns the returned vector<VARP> and executor for the explicitly supported Apple ABI.
    internal sealed class ExpressVariablesHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        private MNNInterop.CppVector _variables;
        private readonly MNNInterop.ExpressExecutor _executor;
        internal ExpressVariablesHandle(MNNInterop.CppVector variables, MNNInterop.ExpressExecutor executor) : base(true)
        {
            _variables = variables;
            _executor = executor;
            SetHandle(variables.Begin);
        }

        protected override bool ReleaseHandle()
        {
            try
            {
                using (_executor.Enter())
                    MNNInterop.DestroyVariables(ref _variables);
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
