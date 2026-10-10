#if UNITY_EDITOR_OSX || (UNITY_STANDALONE_OSX && !UNITY_EDITOR && !ENABLE_IL2CPP)
#define MNN_APPLE_CPP_ABI
#endif
using System;
using System.Runtime.InteropServices;
using MNN.Unity.Interop.Handles;

namespace MNN.Unity.Interop
{
    internal static partial class MNNInterop
    {
#if MNN_APPLE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Interpreter14createFromFileEPKc")]
        private static extern InterpreterHandle InterpreterFromFile([MarshalAs(UnmanagedType.LPUTF8Str)] string path);
#else
        private static InterpreterHandle InterpreterFromFile([MarshalAs(UnmanagedType.LPUTF8Str)] string path) => throw UnsupportedAbi();
#endif
        internal static InterpreterHandle MNN_Interpreter_createFromFile(string path)
        {
            RequireAbi();
            return InterpreterFromFile(path);
        }

#if MNN_APPLE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Interpreter16createFromBufferEPKvm")]
        private static extern InterpreterHandle InterpreterFromBuffer(IntPtr buffer, UIntPtr size);
#else
        private static InterpreterHandle InterpreterFromBuffer(IntPtr buffer, UIntPtr size) => throw UnsupportedAbi();
#endif
        internal static InterpreterHandle MNN_Interpreter_createFromBuffer(IntPtr buffer, long size)
        {
            RequireAbi();
            return InterpreterFromBuffer(buffer, (UIntPtr)checked((ulong)size));
        }

#if MNN_APPLE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Interpreter7destroyEPS0_")]
        internal static extern void MNN_Interpreter_destroy(IntPtr interpreter);
#else
        internal static void MNN_Interpreter_destroy(IntPtr interpreter) => throw UnsupportedAbi();
#endif
#if MNN_APPLE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Interpreter13createSessionERKNS_14ScheduleConfigE")]
        internal static extern IntPtr MNN_Interpreter_createSession(InterpreterHandle interpreter, ref ScheduleConfig config);
#else
        internal static IntPtr MNN_Interpreter_createSession(InterpreterHandle interpreter, ref ScheduleConfig config) => throw UnsupportedAbi();
#endif
#if MNN_APPLE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Interpreter14releaseSessionEPNS_7SessionE")]
        [return: MarshalAs(UnmanagedType.I1)]
        internal static extern bool MNN_Interpreter_releaseSession(IntPtr interpreter, IntPtr session);
#else
        internal static bool MNN_Interpreter_releaseSession(IntPtr interpreter, IntPtr session) => throw UnsupportedAbi();
#endif
#if MNN_APPLE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZNK3MNN11Interpreter10runSessionEPNS_7SessionE")]
        internal static extern int MNN_Interpreter_runSession(InterpreterHandle interpreter, SessionHandle session);
#else
        internal static int MNN_Interpreter_runSession(InterpreterHandle interpreter, SessionHandle session) => throw UnsupportedAbi();
#endif
#if MNN_APPLE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Interpreter12releaseModelEv")]
        internal static extern void MNN_Interpreter_releaseModel(InterpreterHandle interpreter);
#else
        internal static void MNN_Interpreter_releaseModel(InterpreterHandle interpreter) => throw UnsupportedAbi();
#endif
#if MNN_APPLE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Interpreter15getSessionInputEPKNS_7SessionEPKc")]
        internal static extern IntPtr MNN_Interpreter_getSessionInput(InterpreterHandle interpreter, SessionHandle session, [MarshalAs(UnmanagedType.LPUTF8Str)] string name);
#else
        internal static IntPtr MNN_Interpreter_getSessionInput(InterpreterHandle interpreter, SessionHandle session, [MarshalAs(UnmanagedType.LPUTF8Str)] string name) => throw UnsupportedAbi();
#endif
#if MNN_APPLE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Interpreter16getSessionOutputEPKNS_7SessionEPKc")]
        internal static extern IntPtr MNN_Interpreter_getSessionOutput(InterpreterHandle interpreter, SessionHandle session, [MarshalAs(UnmanagedType.LPUTF8Str)] string name);
#else
        internal static IntPtr MNN_Interpreter_getSessionOutput(InterpreterHandle interpreter, SessionHandle session, [MarshalAs(UnmanagedType.LPUTF8Str)] string name) => throw UnsupportedAbi();
#endif
#if MNN_APPLE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Interpreter12resizeTensorEPNS_6TensorERKNSt3__16vectorIiNS3_9allocatorIiEEEE")]
        private static extern void ResizeTensor(InterpreterHandle interpreter, IntPtr tensor, ref CppVector dimensions);
#else
        private static void ResizeTensor(InterpreterHandle interpreter, IntPtr tensor, ref CppVector dimensions) => throw UnsupportedAbi();
#endif
        internal static void MNN_Interpreter_resizeTensor(InterpreterHandle interpreter, IntPtr tensor, IntPtr dims, int count)
        {
            var values = new int[count];
            Marshal.Copy(dims, values, 0, count);
            using (var vector = new IntVector(values))
                ResizeTensor(interpreter, tensor, ref vector.Value);
        }

#if MNN_APPLE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Interpreter13resizeSessionEPNS_7SessionE")]
        internal static extern void MNN_Interpreter_resizeSession(InterpreterHandle interpreter, SessionHandle session);
#else
        internal static void MNN_Interpreter_resizeSession(InterpreterHandle interpreter, SessionHandle session) => throw UnsupportedAbi();
#endif
#if MNN_APPLE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Interpreter12setCacheFileEPKcm")]
        private static extern void SetCacheFile(InterpreterHandle interpreter, [MarshalAs(UnmanagedType.LPUTF8Str)] string path, UIntPtr keySize);
#else
        private static void SetCacheFile(InterpreterHandle interpreter, [MarshalAs(UnmanagedType.LPUTF8Str)] string path, UIntPtr keySize) => throw UnsupportedAbi();
#endif
        internal static void MNN_Interpreter_setCacheFile(InterpreterHandle interpreter, string path) => SetCacheFile(interpreter, path, (UIntPtr)128u);
#if MNN_APPLE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Interpreter15setExternalFileEPKcm")]
        private static extern void SetExternalFile(InterpreterHandle interpreter, [MarshalAs(UnmanagedType.LPUTF8Str)] string path, UIntPtr flag);
#else
        private static void SetExternalFile(InterpreterHandle interpreter, string path, UIntPtr flag) => throw UnsupportedAbi();
#endif
        internal static void MNN_Interpreter_setExternalFile(InterpreterHandle interpreter, string path) => SetExternalFile(interpreter, path, (UIntPtr)128u);
#if MNN_APPLE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Interpreter15updateCacheFileEPNS_7SessionEi")]
        private static extern int UpdateCacheFile(InterpreterHandle interpreter, SessionHandle session, int flag);
#else
        private static int UpdateCacheFile(InterpreterHandle interpreter, SessionHandle session, int flag) => throw UnsupportedAbi();
#endif
        internal static void MNN_Interpreter_updateCacheFile(InterpreterHandle interpreter, SessionHandle session) => MNNException.ThrowIfError(UpdateCacheFile(interpreter, session, 0), "UpdateCacheFile");
    }
}
