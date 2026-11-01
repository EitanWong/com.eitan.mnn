#if UNITY_EDITOR_OSX || (UNITY_STANDALONE_OSX && !UNITY_EDITOR && !ENABLE_IL2CPP) || ((UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR && ENABLE_IL2CPP)
#define MNN_INFERENCE_CPP_ABI
#endif
using System;
using System.Runtime.InteropServices;
using MNN.Unity.Interop.Handles;

namespace MNN.Unity.Interop
{
    internal static partial class MNNInterop
    {
        // Official public pointer/scalar APIs. Uses the existing bundled
        // MNN 3.6.2 platform-specific ScheduleConfig ABI; no STL returns.
#if MNN_INFERENCE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Interpreter14getSessionInfoEPKNS_7SessionENS0_15SessionInfoCodeEPv")]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool GetSessionBackendInfo(InterpreterHandle interpreter, IntPtr session, int code, [Out] int[] values);
#else
        private static bool GetSessionBackendInfo(InterpreterHandle interpreter, IntPtr session, int code, int[] values) => throw UnsupportedAbi();
#endif
        internal static bool IsBackendAvailable(MNNBackendType type)
        {
            RequireAbi();
            // A tiny deterministic public MNN graph checks actual session creation,
            // not just registry membership (which can report unavailable devices).
            byte[] graph = Convert.FromBase64String(ProbeGraph);
            var pin = GCHandle.Alloc(graph, GCHandleType.Pinned);
            InterpreterHandle interpreter;
            try
            {
                interpreter = MNN_Interpreter_createFromBuffer(pin.AddrOfPinnedObject(), graph.Length);
            }
            finally
            {
                pin.Free();
            }

            using (interpreter)
            {
                if (interpreter == null || interpreter.IsInvalid)
                    return false;
                var config = new ScheduleConfig{type = type, numThread = 4, backupType = MNNBackendType.CPU};
                IntPtr session = MNN_Interpreter_createSession(interpreter, ref config);
                if (session == IntPtr.Zero)
                    return false;
                using (var owner = new SessionHandle(interpreter, session))
                {
                    if (SessionBackend(interpreter, session) != type) return false;
                    IntPtr input = MNN_Interpreter_getSessionInput(interpreter, owner, "input");
                    IntPtr output = MNN_Interpreter_getSessionOutput(interpreter, owner, "output");
                    if (input == IntPtr.Zero || output == IntPtr.Zero) return false;
                    var values = new[] { 0f, 1f, -2f, 3f };
                    IntPtr data = MNN_Tensor_map(input, MNNTensorMapType.Write, MNNDimensionType.Caffe);
                    if (data == IntPtr.Zero) return false;
                    try { Marshal.Copy(values, 0, data, values.Length); }
                    finally { MNN_Tensor_unmap(input, MNNTensorMapType.Write, MNNDimensionType.Caffe, data); }
                    if (MNN_Interpreter_runSession(interpreter, owner) != 0) return false;
                    data = MNN_Tensor_map(output, MNNTensorMapType.Read, MNNDimensionType.Caffe);
                    if (data == IntPtr.Zero) return false;
                    try { Marshal.Copy(data, values, 0, values.Length); }
                    finally { MNN_Tensor_unmap(output, MNNTensorMapType.Read, MNNDimensionType.Caffe, data); }
                    return values[0] == 1f && values[1] == 3f && values[2] == -3f && values[3] == 7f;
                }
            }
        }

        // Same 2*x+1 graph as Tests/Fixtures/affine.mnn, 584 bytes, no weights/downloads.
        private const string ProbeGraph = "GAAAABQAEAAAAAAABAAIAAAAAAAAAAwAFAAAABgCAABgAAAABAAAAAUAAABIAAAAOAAAACQAAAAUAAAABAAAAAYAAABvdXRwdXQAAAYAAABDb25zdDQAAAkAAABCaW5hcnlPcDMAAAAGAAAAQ29uc3QyAAAFAAAAaW5wdXQAAAAFAAAAWAEAAPAAAACgAAAATAAAAAQAAAB8////AAAABjAAAAAoAAAAFAAAAAgAAAAHAAAAAQAAAAQAAAAGAAAAb3V0cHV0AAAEAAQABAAAAAIAAAACAAAAAwAAABD///8AAAAHJAAAABQAAAAIAAAACwAAAAEAAAADAAAABgAAAENvbnN0NAAAZP///wAAAAEEAAAAAQAAAAAAgD8QABwACAAHAAwAEAAUABgAEAAAAAAAAAY4AAAALAAAABQAAAAIAAAABwAAAAEAAAACAAAACQAAAEJpbmFyeU9wMwAGAAgABAAGAAAAAgAAAAIAAAAAAAAAAQAAAKz///8AAAAHOAAAABQAAAAIAAAACwAAAAEAAAABAAAABgAAAENvbnN0MgAAFAAMAAAABwAAAAAAAAAAAAAACAAUAAAAAAAAAQQAAAABAAAAAAAAQBAAGAAAAAcACAAMABAAFAAQAAAAAAAAFSwAAAAUAAAACAAAACIAAAABAAAAAAAAAAUAAABpbnB1dAAKAAwACAAAAAcACgAAAAAAAAAEAAAAAgAAAAEAAAAEAAAAAAAKAAgAAAAAAAQACgAAAAQAAAAFAAAAMy42LjEAAAA=";
        internal static MNNBackendType SessionBackend(InterpreterHandle interpreter, IntPtr session)
        {
            // createSession uses one ScheduleConfig; official API requires >= 2 ints.
            var values = new[]{-1, -1};
            if (!GetSessionBackendInfo(interpreter, session, 2, values) || values[0] < 0)
                throw new MNNException(MNNErrorCode.NoExecution, "Cannot query the actual MNN session backend.");
            return (MNNBackendType)values[0];
        }
    }
}
