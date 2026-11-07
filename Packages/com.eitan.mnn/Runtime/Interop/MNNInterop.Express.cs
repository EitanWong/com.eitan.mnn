#if UNITY_EDITOR_WIN || UNITY_EDITOR_LINUX || ((UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX) && !UNITY_EDITOR && !ENABLE_IL2CPP)
#define MNN_DESKTOP_TASK_ABI
#endif
#if UNITY_EDITOR_OSX || UNITY_EDITOR_WIN || UNITY_EDITOR_LINUX || ((UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX) && !UNITY_EDITOR && !ENABLE_IL2CPP) || (UNITY_STANDALONE_OSX && !UNITY_EDITOR && !ENABLE_IL2CPP) || ((UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR && ENABLE_IL2CPP)
#define MNN_TASK_CPP_ABI
#endif
using System;
using System.Runtime.InteropServices;

namespace MNN.Unity.Interop
{
    internal static partial class MNNInterop
    {
        // Non-trivial 16-byte C++ returns use indirect result storage. A padded
        // 24-byte managed return selects indirect result storage on the pinned
        // ARM64 IL2CPP, Apple Mono and GNU desktop Mono ABIs.
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

#if MNN_TASK_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN7Express8Executor11newExecutorE14MNNForwardTypeRKNS_13BackendConfigEi")]
        private static extern IndirectShared NewExecutor(MNNBackendType type, ref BackendConfig config, int threads);
#else
        private static IndirectShared NewExecutor(MNNBackendType type, ref BackendConfig config, int threads) => throw UnsupportedAbi();
#endif
#if MNN_TASK_CPP_ABI
#if UNITY_ANDROID && !UNITY_EDITOR
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN7Express13ExecutorScopeC1ERKNSt6__ndk110shared_ptrINS0_8ExecutorEEE")]
#elif MNN_DESKTOP_TASK_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN7Express13ExecutorScopeC1ERKSt10shared_ptrINS0_8ExecutorEE")]
#else
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN7Express13ExecutorScopeC1ERKNSt3__110shared_ptrINS0_8ExecutorEEE")]
#endif
        private static extern void EnterScope(IntPtr scope, ref CppShared executor);
#else
        private static void EnterScope(IntPtr scope, ref CppShared executor) => throw UnsupportedAbi();
#endif
#if MNN_TASK_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN7Express13ExecutorScopeD1Ev")]
        private static extern void ExitScope(IntPtr scope);
#else
        private static void ExitScope(IntPtr scope) => throw UnsupportedAbi();
#endif
#if MNN_TASK_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN7Express8Variable7getInfoEv")]
        private static extern IntPtr GetVariableInfo(IntPtr variable);
#else
        private static IntPtr GetVariableInfo(IntPtr variable) => throw UnsupportedAbi();
#endif
#if MNN_TASK_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN7Express8Variable7readMapIfEEPKT_v")]
        private static extern IntPtr ReadFloatVariable(IntPtr variable);
#else
        private static IntPtr ReadFloatVariable(IntPtr variable) => throw UnsupportedAbi();
#endif
#if MNN_TASK_CPP_ABI
        #if UNITY_ANDROID && !UNITY_EDITOR
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN7Express4VARPD2Ev")]
#else
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN7Express4VARPD1Ev")]
#endif
        internal static extern void DestroyVariable(ref CppShared variable);
#else
        internal static void DestroyVariable(ref CppShared variable) => throw UnsupportedAbi();
#endif
#if MNN_TASK_CPP_ABI
#if UNITY_EDITOR_WIN || (UNITY_STANDALONE_WIN && !UNITY_EDITOR)
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN7Express8Variable4loadEPKc")]
        private static extern CppVector LoadWindowsVariables(byte[] path);
        internal static CppVector LoadVariables(string path) => LoadWindowsVariables(ToWindowsPathBytes(path));
#else
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN7Express8Variable4loadEPKc")]
        internal static extern CppVector LoadVariables(string path);
#endif
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = VariableLoadBufferSymbol)]
        internal static extern CppVector LoadVariables(byte[] graph, UIntPtr length);
#if UNITY_ANDROID && !UNITY_EDITOR
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN7Express8Variable7computeERKNSt6__ndk16vectorINS0_4VARPENS2_9allocatorIS4_EEEEb")]
#elif MNN_DESKTOP_TASK_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN7Express8Variable7computeERKSt6vectorINS0_4VARPESaIS3_EEb")]
#else
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN7Express8Variable7computeERKNSt3__16vectorINS0_4VARPENS2_9allocatorIS4_EEEEb")]
#endif
        private static extern void ComputeVariables(ref CppVector variables, [MarshalAs(UnmanagedType.I1)] bool forceCpu);
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN7Express8Variable5inputENS0_4VARPE")]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool InputVariable(IntPtr variable, ref CppShared source);
#if UNITY_ANDROID && !UNITY_EDITOR
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN7Express6_ConstEPKvNSt6__ndk16vectorIiNS3_9allocatorIiEEEENS0_15DimensionformatE13halide_type_t")]
#elif MNN_DESKTOP_TASK_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN7Express6_ConstEPKvSt6vectorIiSaIiEENS0_15DimensionformatE13halide_type_t")]
#else
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN7Express6_ConstEPKvNSt3__16vectorIiNS3_9allocatorIiEEEENS0_15DimensionformatE13halide_type_t")]
#endif
        private static extern IndirectShared ConstVariable(IntPtr data, ref CppVector shape, int format, HalideType type);
#else
        internal static CppVector LoadVariables(string path) => throw UnsupportedAbi();
        internal static CppVector LoadVariables(byte[] graph, UIntPtr length) => throw UnsupportedAbi();
        private static void ComputeVariables(ref CppVector variables, bool forceCpu) => throw UnsupportedAbi();
        private static bool InputVariable(IntPtr variable, ref CppShared source) => throw UnsupportedAbi();
        private static IndirectShared ConstVariable(IntPtr data, ref CppVector shape, int format, HalideType type) => throw UnsupportedAbi();
#endif
#if MNN_TASK_CPP_ABI
#if UNITY_ANDROID && !UNITY_EDITOR
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN7Express6Module4loadERKNSt6__ndk16vectorINS2_12basic_stringIcNS2_11char_traitsIcEENS2_9allocatorIcEEEENS7_IS9_EEEESD_PKhmPKNS1_6ConfigE")]
#elif MNN_DESKTOP_TASK_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = DesktopModuleLoadSymbol)]
#else
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN7Express6Module4loadERKNSt3__16vectorINS2_12basic_stringIcNS2_11char_traitsIcEENS2_9allocatorIcEEEENS7_IS9_EEEESD_PKhmPKNS1_6ConfigE")]
#endif
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
#if UNITY_EDITOR_WIN || (UNITY_STANDALONE_WIN && !UNITY_EDITOR)
        private const string VariableLoadBufferSymbol = "_ZN3MNN7Express8Variable4loadEPKhy";
        private const string DesktopModuleLoadSymbol = "_ZN3MNN7Express6Module4loadERKSt6vectorINSt7__cxx1112basic_stringIcSt11char_traitsIcESaIcEEESaIS8_EESC_PKhyPKNS1_6ConfigE";
#else
        private const string VariableLoadBufferSymbol = "_ZN3MNN7Express8Variable4loadEPKhm";
        private const string DesktopModuleLoadSymbol = "_ZN3MNN7Express6Module4loadERKSt6vectorINSt7__cxx1112basic_stringIcSt11char_traitsIcESaIcEEESaIS8_EESC_PKhmPKNS1_6ConfigE";
#endif
        internal static IntPtr LoadSingleInputModule(byte[] graph, string inputName, string outputName)
        {
            using (var from = new NativeString(inputName))
            using (var to = new NativeString(outputName))
            {
                var names = new[] {from.Value, to.Value};
                var pin = GCHandle.Alloc(names, GCHandleType.Pinned);
                try
                {
                    IntPtr p = pin.AddrOfPinnedObject();
                    int stride = Marshal.SizeOf<CppString>();
                    var inputs = new CppVector {Begin = p, End = IntPtr.Add(p, stride), Capacity = IntPtr.Add(p, stride)};
                    var outputs = new CppVector {Begin = IntPtr.Add(p, stride), End = IntPtr.Add(p, 2 * stride), Capacity = IntPtr.Add(p, 2 * stride)};
                    IntPtr module = LoadModule(ref inputs, ref outputs, graph, (UIntPtr)(uint)graph.Length, IntPtr.Zero);
                    if (module == IntPtr.Zero)
                        throw new MNNException(MNNErrorCode.NoExecution, "Cannot load MNN Module.");
                    return module;
                }
                finally
                {
                    pin.Free();
                }
            }
        }

        internal static float[] ForwardSingleInputModule(IntPtr module, CppShared input)
        {
            var output = ForwardModule(module, ref input).Value;
            try
            {
                if (output.Object == IntPtr.Zero)
                    throw new MNNException(MNNErrorCode.NoExecution, "No MNN Module output.");
                return ReadFloat(output);
            }
            finally
            {
                DestroyVariable(ref output);
            }
        }

        internal static float[] RunSingleInputModule(byte[] graph, string inputName, string outputName, CppShared input)
        {
            IntPtr module = LoadSingleInputModule(graph, inputName, outputName);
            try
            {
                return ForwardSingleInputModule(module, input);
            }
            finally
            {
                DestroyModule(module);
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

#if MNN_TASK_CPP_ABI
        // iOS resolves operator new from the linked system libc++ via __Internal.
        // A literal libc++.1 library name generates invalid FORCE_PINVOKE macros in Unity 2021 IL2CPP.
        #if UNITY_EDITOR_WIN || (UNITY_STANDALONE_WIN && !UNITY_EDITOR)
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_Znwy")]
#elif MNN_DESKTOP_TASK_ABI
        [DllImport("libstdc++.so.6", CallingConvention = Conv, EntryPoint = "_Znwm")]
#else
        [DllImport(Cxx, CallingConvention = Conv, EntryPoint = "_Znwm")]
#endif
        private static extern IntPtr NativeNew(UIntPtr size);
        #if MNN_DESKTOP_TASK_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZNK3MNN7Express8Variable4nameB5cxx11Ev")]
#else
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZNK3MNN7Express8Variable4nameEv")]
#endif
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

#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR && ENABLE_IL2CPP
#if UNITY_ANDROID
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZNSt6__ndk16vectorIN3MNN7Express4VARPENS_9allocatorIS3_EEED2Ev")]
#else
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZNSt3__16vectorIN3MNN7Express4VARPENS_9allocatorIS3_EEED1B9nqn220106Ev")]
#endif
        private static extern void VariableVectorDestroy(ref CppVector variables);
#endif
        internal static void DestroyVariables(ref CppVector variables)
        {
            try
            {
#if MNN_DESKTOP_TASK_ABI
                DesktopVariableVectorDestroy(ref variables);
#elif (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR && ENABLE_IL2CPP
                // Destroy both elements and allocation with the exact native instantiation.
                VariableVectorDestroy(ref variables);
#else
                int count = VectorCount(variables, 16);
                for (int i = count - 1; i >= 0; --i)
                {
                    var value = Marshal.PtrToStructure<CppShared>(IntPtr.Add(variables.Begin, i * 16));
                    DestroyVariable(ref value);
                }

                // Legacy macOS binary does not export vector<VARP>'s destructor.
                // Its pinned libc++ default allocator uses the same unsized delete
                // for int and VARP storage. This is an allocator ABI dependency,
                // not a portable C++ vector conversion. Elements are already destroyed.
                IntVectorDestroy(ref variables);
#endif
            }
            finally { variables = default; }
        }

        internal static float[] ReadFloat(CppShared variable)
        {
            return ReadFloat(variable, out _);
        }

        internal static float[] ReadFloat(CppShared variable, out int[] shape)
        {
            IntPtr ptr = GetVariableInfo(variable.Object);
            if (ptr == IntPtr.Zero)
                throw new MNNException(MNNErrorCode.NoExecution, "Cannot infer Express output shape.");
            var info = Marshal.PtrToStructure<VariableInfo>(ptr);
            int size = checked((int)info.Size.ToUInt64());
            shape = new int[VectorCount(info.Dimensions, 4)];
            Marshal.Copy(info.Dimensions.Begin, shape, 0, shape.Length);
            if (size <= 0 || size > 10000000 || info.Type.Code != 2 || info.Type.Bits != 32 || info.Type.Lanes != 1)
                throw new InvalidOperationException("Expected nonempty float32 Express output; shape=[" + string.Join(",", shape) +
                    "], size=" + size + ", type=" + info.Type.Code + ":" + info.Type.Bits + ":" + info.Type.Lanes + ".");
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

            internal ExpressExecutor(int threads, MNNBackendType backendType = MNNBackendType.Auto, MNNPrecisionMode precision = MNNPrecisionMode.Normal)
            {
                RequireAbi();
                Backend = MNNAcceleration.Resolve(backendType);
                var config = new BackendConfig{precision = precision};
                _executor = NewExecutor(Backend, ref config, threads).Value;
                if (_executor.Object == IntPtr.Zero && Backend != MNNBackendType.CPU)
                {
                    Backend = MNNBackendType.CPU;
                    _executor = NewExecutor(Backend, ref config, threads).Value;
                }

                if (_executor.Object == IntPtr.Zero)
                    throw new MNNException(MNNErrorCode.NoExecution, "Cannot create Express executor, including CPU fallback.");
            }

            internal IDisposable Enter()
            {
                if (_executor.Object == IntPtr.Zero)
                    throw new ObjectDisposedException(nameof(ExpressExecutor));
                return new ExpressScope(_executor);
            }
            public void Dispose() => ReleaseShared(ref _executor);
            private sealed class ExpressScope : IDisposable
            {
                private IntPtr _scope;
                internal ExpressScope(CppShared executor)
                {
                    // Public ExecutorScope has only its vptr under the pinned ABI.
                    _scope = Marshal.AllocHGlobal(IntPtr.Size);
                    try { EnterScope(_scope, ref executor); }
                    catch
                    {
                        Marshal.FreeHGlobal(_scope);
                        _scope = IntPtr.Zero;
                        throw;
                    }
                }

                public void Dispose()
                {
                    if (_scope == IntPtr.Zero) return;
                    try { ExitScope(_scope); }
                    finally
                    {
                        Marshal.FreeHGlobal(_scope);
                        _scope = IntPtr.Zero;
                    }
                }
            }
        }

        private sealed class CpuScope : IDisposable
        {
            private readonly ExpressExecutor _executor;
            private readonly IDisposable _scope;
            internal CpuScope()
            {
                _executor = new ExpressExecutor(4, MNNBackendType.CPU);
                try { _scope = _executor.Enter(); }
                catch { _executor.Dispose(); throw; }
            }

            public void Dispose()
            {
                try { _scope.Dispose(); }
                finally { _executor.Dispose(); }
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
