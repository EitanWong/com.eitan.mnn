#if UNITY_EDITOR_OSX || UNITY_EDITOR_WIN || UNITY_EDITOR_LINUX || ((UNITY_STANDALONE_OSX || UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX) && !UNITY_EDITOR && !ENABLE_IL2CPP) || ((UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR && ENABLE_IL2CPP)
#define MNN_INFERENCE_CPP_ABI
#endif
using System;
using System.Runtime.InteropServices;
using MNN.Unity.Interop.Handles;

namespace MNN.Unity.Interop
{
    internal static partial class MNNInterop
    {
#if MNN_INFERENCE_CPP_ABI
#if UNITY_EDITOR_WIN || (UNITY_STANDALONE_WIN && !UNITY_EDITOR)
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = MNNCoreSymbols.InterpreterFromFile)]
        private static extern IntPtr WindowsInterpreterFromFile(byte[] path);
        private static IntPtr InterpreterFromFile([MarshalAs(UnmanagedType.LPUTF8Str)] string path) => WindowsInterpreterFromFile(ToWindowsPathBytes(path));
#else
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = MNNCoreSymbols.InterpreterFromFile)]
        private static extern IntPtr InterpreterFromFile([MarshalAs(UnmanagedType.LPUTF8Str)] string path);
#endif
#else
        private static IntPtr InterpreterFromFile([MarshalAs(UnmanagedType.LPUTF8Str)] string path) => throw UnsupportedAbi();
#endif
        internal static InterpreterHandle MNN_Interpreter_createFromFile(string path)
        {
            RequireAbi();
            return new InterpreterHandle(InterpreterFromFile(path));
        }

#if MNN_INFERENCE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = MNNCoreSymbols.InterpreterFromBuffer)]
        private static extern IntPtr InterpreterFromBuffer(IntPtr buffer, UIntPtr size);
#else
        private static IntPtr InterpreterFromBuffer(IntPtr buffer, UIntPtr size) => throw UnsupportedAbi();
#endif
        internal static InterpreterHandle MNN_Interpreter_createFromBuffer(IntPtr buffer, long size)
        {
            RequireAbi();
            return new InterpreterHandle(InterpreterFromBuffer(buffer, (UIntPtr)checked((ulong)size)));
        }

#if MNN_INFERENCE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = MNNCoreSymbols.InterpreterDestroy)]
        internal static extern void MNN_Interpreter_destroy(IntPtr interpreter);
#else
        internal static void MNN_Interpreter_destroy(IntPtr interpreter) => throw UnsupportedAbi();
#endif
#if MNN_INFERENCE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = MNNCoreSymbols.CreateSession)]
        internal static extern IntPtr MNN_Interpreter_createSession(InterpreterHandle interpreter, ref ScheduleConfig config);
#else
        internal static IntPtr MNN_Interpreter_createSession(InterpreterHandle interpreter, ref ScheduleConfig config) => throw UnsupportedAbi();
#endif
#if MNN_INFERENCE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = MNNCoreSymbols.ReleaseSession)]
        [return: MarshalAs(UnmanagedType.I1)]
        internal static extern bool MNN_Interpreter_releaseSession(IntPtr interpreter, IntPtr session);
#else
        internal static bool MNN_Interpreter_releaseSession(IntPtr interpreter, IntPtr session) => throw UnsupportedAbi();
#endif
#if MNN_INFERENCE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = MNNCoreSymbols.RunSession)]
        internal static extern int MNN_Interpreter_runSession(InterpreterHandle interpreter, SessionHandle session);
#else
        internal static int MNN_Interpreter_runSession(InterpreterHandle interpreter, SessionHandle session) => throw UnsupportedAbi();
#endif
#if MNN_INFERENCE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = MNNCoreSymbols.ReleaseModel)]
        internal static extern void MNN_Interpreter_releaseModel(InterpreterHandle interpreter);
#else
        internal static void MNN_Interpreter_releaseModel(InterpreterHandle interpreter) => throw UnsupportedAbi();
#endif
#if MNN_INFERENCE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = MNNCoreSymbols.GetSessionInput)]
        internal static extern IntPtr MNN_Interpreter_getSessionInput(InterpreterHandle interpreter, SessionHandle session, [MarshalAs(UnmanagedType.LPUTF8Str)] string name);
#else
        internal static IntPtr MNN_Interpreter_getSessionInput(InterpreterHandle interpreter, SessionHandle session, [MarshalAs(UnmanagedType.LPUTF8Str)] string name) => throw UnsupportedAbi();
#endif
#if MNN_INFERENCE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = MNNCoreSymbols.GetSessionOutput)]
        internal static extern IntPtr MNN_Interpreter_getSessionOutput(InterpreterHandle interpreter, SessionHandle session, [MarshalAs(UnmanagedType.LPUTF8Str)] string name);
#else
        internal static IntPtr MNN_Interpreter_getSessionOutput(InterpreterHandle interpreter, SessionHandle session, [MarshalAs(UnmanagedType.LPUTF8Str)] string name) => throw UnsupportedAbi();
#endif
#if MNN_INFERENCE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = MNNCoreSymbols.ResizeTensor)]
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

#if MNN_INFERENCE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = MNNCoreSymbols.ResizeSession)]
        internal static extern void MNN_Interpreter_resizeSession(InterpreterHandle interpreter, SessionHandle session);
#else
        internal static void MNN_Interpreter_resizeSession(InterpreterHandle interpreter, SessionHandle session) => throw UnsupportedAbi();
#endif
#if MNN_INFERENCE_CPP_ABI
#if UNITY_EDITOR_WIN || (UNITY_STANDALONE_WIN && !UNITY_EDITOR)
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = MNNCoreSymbols.SetCacheFile)]
        private static extern void WindowsSetCacheFile(InterpreterHandle interpreter, byte[] path, UIntPtr keySize);
        private static void SetCacheFile(InterpreterHandle interpreter, [MarshalAs(UnmanagedType.LPUTF8Str)] string path, UIntPtr keySize) => WindowsSetCacheFile(interpreter, ToWindowsPathBytes(path), keySize);
#else
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = MNNCoreSymbols.SetCacheFile)]
        private static extern void SetCacheFile(InterpreterHandle interpreter, [MarshalAs(UnmanagedType.LPUTF8Str)] string path, UIntPtr keySize);
#endif
#else
        private static void SetCacheFile(InterpreterHandle interpreter, [MarshalAs(UnmanagedType.LPUTF8Str)] string path, UIntPtr keySize) => throw UnsupportedAbi();
#endif
        internal static void MNN_Interpreter_setCacheFile(InterpreterHandle interpreter, string path) => SetCacheFile(interpreter, path, (UIntPtr)128u);
#if MNN_INFERENCE_CPP_ABI
#if UNITY_EDITOR_WIN || (UNITY_STANDALONE_WIN && !UNITY_EDITOR)
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = MNNCoreSymbols.SetExternalFile)]
        private static extern void WindowsSetExternalFile(InterpreterHandle interpreter, byte[] path, UIntPtr flag);
        private static void SetExternalFile(InterpreterHandle interpreter, [MarshalAs(UnmanagedType.LPUTF8Str)] string path, UIntPtr flag) => WindowsSetExternalFile(interpreter, ToWindowsPathBytes(path), flag);
#else
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = MNNCoreSymbols.SetExternalFile)]
        private static extern void SetExternalFile(InterpreterHandle interpreter, [MarshalAs(UnmanagedType.LPUTF8Str)] string path, UIntPtr flag);
#endif
#else
        private static void SetExternalFile(InterpreterHandle interpreter, string path, UIntPtr flag) => throw UnsupportedAbi();
#endif
        internal static void MNN_Interpreter_setExternalFile(InterpreterHandle interpreter, string path) => SetExternalFile(interpreter, path, (UIntPtr)128u);
#if MNN_INFERENCE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = MNNCoreSymbols.UpdateCacheFile)]
        private static extern int UpdateCacheFile(InterpreterHandle interpreter, SessionHandle session, int flag);
#else
        private static int UpdateCacheFile(InterpreterHandle interpreter, SessionHandle session, int flag) => throw UnsupportedAbi();
#endif
        internal static void MNN_Interpreter_updateCacheFile(InterpreterHandle interpreter, SessionHandle session) => MNNException.ThrowIfError(UpdateCacheFile(interpreter, session, 0), "UpdateCacheFile");
    }
}
