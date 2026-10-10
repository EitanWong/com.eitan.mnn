using System.Runtime.InteropServices;

namespace MNN.Unity.Interop
{
    /// <summary>
    /// Shared P/Invoke settings. Declare native functions in task-specific partial files.
    /// Every declaration calls exports from the MNN library itself.
    /// </summary>
    internal static partial class MNNInterop
    {
        private const string Lib = MNNNative.LibraryName;
        private const CallingConvention Conv = MNNNative.Convention;
    }
}
