#if UNITY_EDITOR_OSX || (UNITY_STANDALONE_OSX && !UNITY_EDITOR && !ENABLE_IL2CPP)
#define MNN_APPLE_CPP_ABI
#endif
using System;
using System.Runtime.InteropServices;

namespace MNN.Unity.Interop
{
    internal static partial class MNNInterop
    {
        // Non-trivial 16-byte C++ returns use indirect result storage. A padded
        // 24-byte managed return selects the same sret ABI on Apple arm64/x64.
        [StructLayout(LayoutKind.Sequential)]
        private struct IndirectShared
        {
            internal CppShared Value;
            internal ulong Padding;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct VariableInfo
        {
            internal int Order;
            internal CppVector Dimensions;
            internal HalideType Type;
            internal UIntPtr Size;
        }

#if MNN_APPLE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN7Express8Executor11newExecutorE14MNNForwardTypeRKNS_13BackendConfigEi")]
        private static extern IndirectShared NewExecutor(MNNBackendType type, ref BackendConfig config, int threads);
#else
        private static IndirectShared NewExecutor(MNNBackendType type, ref BackendConfig config, int threads) => throw UnsupportedAbi();
#endif
#if MNN_APPLE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN7Express13ExecutorScopeC1ERKNSt3__110shared_ptrINS0_8ExecutorEEE")]
        private static extern void EnterScope(IntPtr scope, ref CppShared executor);
#else
        private static void EnterScope(IntPtr scope, ref CppShared executor) => throw UnsupportedAbi();
#endif
#if MNN_APPLE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN7Express13ExecutorScopeD1Ev")]
        private static extern void ExitScope(IntPtr scope);
#else
        private static void ExitScope(IntPtr scope) => throw UnsupportedAbi();
#endif
#if MNN_APPLE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN7Express8Variable7getInfoEv")]
        private static extern IntPtr GetVariableInfo(IntPtr variable);
#else
        private static IntPtr GetVariableInfo(IntPtr variable) => throw UnsupportedAbi();
#endif
#if MNN_APPLE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN7Express8Variable7readMapIfEEPKT_v")]
        private static extern IntPtr ReadFloatVariable(IntPtr variable);
#else
        private static IntPtr ReadFloatVariable(IntPtr variable) => throw UnsupportedAbi();
#endif
#if MNN_APPLE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN7Express4VARPD1Ev")]
        private static extern void DestroyVariable(ref CppShared variable);
#else
        private static void DestroyVariable(ref CppShared variable) => throw UnsupportedAbi();
#endif
#if MNN_APPLE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN7Express8Variable4loadEPKc")]
        internal static extern CppVector LoadVariables(string path);
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN7Express8Variable7computeERKNSt3__16vectorINS0_4VARPENS2_9allocatorIS4_EEEEb")]
        private static extern void ComputeVariables(ref CppVector variables, [MarshalAs(UnmanagedType.I1)] bool forceCpu);
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN7Express8Variable5inputENS0_4VARPE")]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool InputVariable(IntPtr variable, ref CppShared source);
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN7Express6_ConstEPKvNSt3__16vectorIiNS3_9allocatorIiEEEENS0_15DimensionformatE13halide_type_t")]
        private static extern IndirectShared ConstVariable(IntPtr data, ref CppVector shape, int format, HalideType type);
#else
        internal static CppVector LoadVariables(string path) => throw UnsupportedAbi();
        private static void ComputeVariables(ref CppVector variables, bool forceCpu) => throw UnsupportedAbi();
        private static bool InputVariable(IntPtr variable, ref CppShared source) => throw UnsupportedAbi();
        private static IndirectShared ConstVariable(IntPtr data, ref CppVector shape, int format, HalideType type) => throw UnsupportedAbi();
#endif
#if MNN_APPLE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN7Express6Module4loadERKNSt3__16vectorINS2_12basic_stringIcNS2_11char_traitsIcEENS2_9allocatorIcEEEENS7_IS9_EEEESD_PKhmPKNS1_6ConfigE")]
        private static extern IntPtr LoadModule(ref CppVector inputs, ref CppVector outputs, byte[] graph, UIntPtr length, IntPtr config);
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN7Express6Module7forwardENS0_4VARPE")]
        private static extern IndirectShared ForwardModule(IntPtr module, ref CppShared input);
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN7Express6Module7destroyEPS1_")]
        internal static extern void DestroyModule(IntPtr module);
#else
        private static IntPtr LoadModule(ref CppVector inputs, ref CppVector outputs, byte[] graph, UIntPtr length, IntPtr config) => throw UnsupportedAbi();
        private static IndirectShared ForwardModule(IntPtr module, ref CppShared input) => throw UnsupportedAbi();
        internal static void DestroyModule(IntPtr module) => throw UnsupportedAbi();
#endif
        internal static float[] RunSingleInputModule(byte[] graph, string inputName, string outputName, CppShared input)
        {
            using (var from = new NativeString(inputName))
            using (var to = new NativeString(outputName))
            {
                var names = new[]{from.Value, to.Value};
                var pin = GCHandle.Alloc(names, GCHandleType.Pinned);
                IntPtr module = IntPtr.Zero;
                CppShared output = default;
                try
                {
                    IntPtr p = pin.AddrOfPinnedObject();
                    var inputs = new CppVector{Begin = p, End = IntPtr.Add(p, 24), Capacity = IntPtr.Add(p, 24)};
                    var outputs = new CppVector{Begin = IntPtr.Add(p, 24), End = IntPtr.Add(p, 48), Capacity = IntPtr.Add(p, 48)};
                    module = LoadModule(ref inputs, ref outputs, graph, (UIntPtr)(uint)graph.Length, IntPtr.Zero);
                    if (module == IntPtr.Zero)
                        throw new MNNException(MNNErrorCode.NoExecution, "Cannot load specialized MNN Module.");
                    output = ForwardModule(module, ref input).Value;
                    if (output.Object == IntPtr.Zero)
                        throw new MNNException(MNNErrorCode.NoExecution, "No MNN Module output.");
                    return ReadFloat(output);
                }
                finally
                {
                    ReleaseShared(ref output);
                    if (module != IntPtr.Zero)
                        DestroyModule(module);
                    pin.Free();
                }
            }
        }

        internal static void Compute(CppVector variables) => ComputeVariables(ref variables, false);
        internal static bool Input(CppShared variable, CppShared source) => InputVariable(variable.Object, ref source);
        internal static CppShared Constant(IntPtr data, int[] shape, int format, HalideType type)
        {
            // _Const moves its by-value vector into Variable::Info. Use native-owned storage.
            var begin = NativeNew((UIntPtr)(uint)(shape.Length * 4));
            Marshal.Copy(shape, 0, begin, shape.Length);
            var dims = new CppVector{Begin = begin, End = IntPtr.Add(begin, shape.Length * 4), Capacity = IntPtr.Add(begin, shape.Length * 4)};
            try
            {
                return ConstVariable(data, ref dims, format, type).Value;
            }
            finally
            {
                IntVectorDestroy(ref dims);
            }
        }

#if MNN_APPLE_CPP_ABI
        [DllImport(Cxx, CallingConvention = Conv, EntryPoint = "_Znwm")]
        private static extern IntPtr NativeNew(UIntPtr size);
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZNK3MNN7Express8Variable4nameEv")]
        private static extern IntPtr VariableName(IntPtr variable);
#else
        private static IntPtr NativeNew(UIntPtr size) => throw UnsupportedAbi();
        private static IntPtr VariableName(IntPtr variable) => throw UnsupportedAbi();
#endif
        internal static string GetName(IntPtr variable)
        {
            var value = Marshal.PtrToStructure<CppString>(VariableName(variable));
            return System.Text.Encoding.UTF8.GetString(StringBytes(ref value));
        }

        internal static void DestroyVariables(ref CppVector variables)
        {
            int count = VectorCount(variables, 16);
            for (int i = count - 1; i >= 0; --i)
            {
                var value = Marshal.PtrToStructure<CppShared>(IntPtr.Add(variables.Begin, i * 16));
                DestroyVariable(ref value);
            }

            // Both vector instantiations use libc++'s default allocator/operator delete.
            // VARP destructors above release each shared owner before freeing storage
            // through the destructor instantiated in the bundled MNN library.
            IntVectorDestroy(ref variables);
            variables = default;
        }

        internal static float[] ReadFloat(CppShared variable)
        {
            IntPtr ptr = GetVariableInfo(variable.Object);
            if (ptr == IntPtr.Zero)
                throw new MNNException(MNNErrorCode.NoExecution, "Cannot infer Express output shape.");
            var info = Marshal.PtrToStructure<VariableInfo>(ptr);
            int size = checked((int)info.Size.ToUInt64());
            if (size <= 0 || size > 10000000 || info.Type.Code != 2 || info.Type.Bits != 32 || info.Type.Lanes != 1)
                throw new InvalidOperationException("Expected nonempty float32 Express output.");
            var data = ReadFloatVariable(variable.Object);
            if (data == IntPtr.Zero)
                throw new MNNException(MNNErrorCode.NoExecution, "Cannot map Express output.");
            var result = new float[size];
            Marshal.Copy(data, result, 0, size);
            return result;
        }

        internal sealed class ExpressExecutor : IDisposable
        {
            private CppShared _executor;
            internal MNNBackendType Backend { get; private set; }

            internal ExpressExecutor(int threads, MNNBackendType backendType = MNNBackendType.Auto)
            {
                RequireAbi();
                Backend = MNNAcceleration.Resolve(backendType);
                var config = new BackendConfig{precision = MNNPrecisionMode.Normal};
                _executor = NewExecutor(Backend, ref config, threads).Value;
                if (_executor.Object == IntPtr.Zero && Backend != MNNBackendType.CPU)
                {
                    Backend = MNNBackendType.CPU;
                    _executor = NewExecutor(Backend, ref config, threads).Value;
                }

                if (_executor.Object == IntPtr.Zero)
                    throw new MNNException(MNNErrorCode.NoExecution, "Cannot create Express executor, including CPU fallback.");
            }

            internal IDisposable Enter() => new ExpressScope(_executor);
            public void Dispose() => ReleaseShared(ref _executor);
            private sealed class ExpressScope : IDisposable
            {
                private IntPtr _scope;
                internal ExpressScope(CppShared executor)
                {
                    _scope = Marshal.AllocHGlobal(8);
                    EnterScope(_scope, ref executor);
                }

                public void Dispose()
                {
                    ExitScope(_scope);
                    Marshal.FreeHGlobal(_scope);
                    _scope = IntPtr.Zero;
                }
            }
        }

        private sealed class CpuScope : IDisposable
        {
            private CppShared _executor;
            private IntPtr _scope;
            internal CpuScope()
            {
                var config = new BackendConfig{precision = MNNPrecisionMode.Normal};
                _executor = NewExecutor(MNNBackendType.CPU, ref config, 4).Value;
                if (_executor.Object == IntPtr.Zero)
                    throw new MNNException(MNNErrorCode.NoExecution, "Cannot create CPU executor.");
                _scope = Marshal.AllocHGlobal(8);
                EnterScope(_scope, ref _executor);
            }

            public void Dispose()
            {
                ExitScope(_scope);
                Marshal.FreeHGlobal(_scope);
                ReleaseShared(ref _executor);
            }
        }

        private static float[] ReadVariable(CppShared variable, out int width)
        {
            try
            {
                IntPtr infoPtr = variable.Object == IntPtr.Zero ? IntPtr.Zero : GetVariableInfo(variable.Object);
                if (infoPtr == IntPtr.Zero)
                    throw new MNNException(MNNErrorCode.NoExecution, "No tensor result.");
                var info = Marshal.PtrToStructure<VariableInfo>(infoPtr);
                int count = checked((int)info.Size.ToUInt64());
                int rank = VectorCount(info.Dimensions, 4);
                width = rank == 0 ? count : Marshal.ReadInt32(info.Dimensions.End, -4);
                if (count <= 0 || count > 100000000 || info.Type.Code != 2 || info.Type.Bits != 32 || info.Type.Lanes != 1)
                    throw new MNNException(MNNErrorCode.NoExecution, "Expected a nonempty float32 result.");
                IntPtr data = ReadFloatVariable(variable.Object);
                if (data == IntPtr.Zero)
                    throw new MNNException(MNNErrorCode.NoExecution, "Cannot map result.");
                var output = new float[count];
                Marshal.Copy(data, output, 0, count);
                return output;
            }
            finally
            {
                if (variable.Object != IntPtr.Zero)
                    DestroyVariable(ref variable);
            }
        }
    }
}
