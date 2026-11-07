#if UNITY_EDITOR_OSX || UNITY_EDITOR_WIN || UNITY_EDITOR_LINUX || ((UNITY_STANDALONE_OSX || UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX) && !UNITY_EDITOR && !ENABLE_IL2CPP) || ((UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR && ENABLE_IL2CPP)
#define MNN_INFERENCE_CPP_ABI
#endif
using System;
using System.Runtime.InteropServices;

namespace MNN.Unity.Interop
{
    internal static partial class MNNInterop
    {
#if MNN_INFERENCE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = MNNCoreSymbols.TensorMap)]
        internal static extern IntPtr MNN_Tensor_map(IntPtr tensor, MNNTensorMapType mapType, MNNDimensionType dimType);
#else
        internal static IntPtr MNN_Tensor_map(IntPtr tensor, MNNTensorMapType mapType, MNNDimensionType dimType) => throw UnsupportedAbi();
#endif
#if MNN_INFERENCE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = MNNCoreSymbols.TensorUnmap)]
        internal static extern void MNN_Tensor_unmap(IntPtr tensor, MNNTensorMapType mapType, MNNDimensionType dimType, IntPtr pointer);
#else
        internal static void MNN_Tensor_unmap(IntPtr tensor, MNNTensorMapType mapType, MNNDimensionType dimType, IntPtr pointer) => throw UnsupportedAbi();
#endif
#if MNN_INFERENCE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = MNNCoreSymbols.TensorDimensionType)]
        internal static extern int MNN_Tensor_getDimensionType(IntPtr tensor);
#else
        internal static int MNN_Tensor_getDimensionType(IntPtr tensor) => throw UnsupportedAbi();
#endif
#if UNITY_EDITOR_WIN || (UNITY_STANDALONE_WIN && !UNITY_EDITOR && !ENABLE_IL2CPP)
        // The official MSVC header uses a byte type code. Its non-POD member
        // return places this in RCX and explicit result storage in RDX.
        [StructLayout(LayoutKind.Sequential)]
        private struct WindowsHalideType
        {
            internal byte Code, Bits;
            internal ushort Lanes;
        }

        [DllImport(Lib, CallingConvention = Conv, EntryPoint = MNNCoreSymbols.TensorType)]
        private static extern void ReadWindowsTensorType(IntPtr tensor, out WindowsHalideType value);
        private static HalideType TensorType(IntPtr tensor)
        {
            ReadWindowsTensorType(tensor, out var value);
            return new HalideType { Code = value.Code, Bits = value.Bits, Lanes = value.Lanes };
        }
#elif MNN_INFERENCE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = MNNCoreSymbols.TensorType)]
        private static extern HalideType TensorType(IntPtr tensor);
#else
        private static HalideType TensorType(IntPtr tensor) => throw UnsupportedAbi();
#endif
        internal static int MNN_Tensor_getDataType(IntPtr tensor)
        {
            var type = TensorType(tensor);
            if (type.Lanes != 1)
                throw new NotSupportedException("Vector-lane tensor types are unsupported.");
            if (type.Code == 2 && type.Bits == 32)
                return (int)MNNDataType.Float;
            if (type.Code == 2 && type.Bits == 64)
                return (int)MNNDataType.Double;
            if (type.Code == 2 && type.Bits == 16)
                return (int)MNNDataType.Half;
            if (type.Code == 0 && type.Bits == 32)
                return (int)MNNDataType.Int32;
            if (type.Code == 0 && type.Bits == 64)
                return (int)MNNDataType.Int64;
            if (type.Code == 0 && type.Bits == 8)
                return (int)MNNDataType.Int8;
            if (type.Code == 1 && type.Bits == 8)
                return (int)MNNDataType.UInt8;
            throw new NotSupportedException("Unsupported Halide tensor type: " + type.Code + "/" + type.Bits);
        }

#if MNN_INFERENCE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = MNNCoreSymbols.TensorDimensions)]
        internal static extern int MNN_Tensor_getDimensions(IntPtr tensor);
#else
        internal static int MNN_Tensor_getDimensions(IntPtr tensor) => throw UnsupportedAbi();
#endif
#if UNITY_EDITOR_WIN || UNITY_EDITOR_LINUX || ((UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX) && !UNITY_EDITOR && !ENABLE_IL2CPP) || ((UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR && ENABLE_IL2CPP)
        // Read dimensions through the official scalar API on these compiler
        // contracts, avoiding owned STL returns and allocator crossings.
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = MNNCoreSymbols.TensorLength)]
        private static extern int TensorLength(IntPtr tensor, int index);
        internal static void MNN_Tensor_getShape(IntPtr tensor, int[] shape, int count)
        {
            if (shape == null || shape.Length != count || MNN_Tensor_getDimensions(tensor) != count)
                throw new ArgumentException("Tensor shape changed during access.");
            for (int i = 0; i < count; i++)
                shape[i] = TensorLength(tensor, i);
        }
#else
#if MNN_INFERENCE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZNK3MNN6Tensor5shapeEv")]
        private static extern CppVector TensorShape(IntPtr tensor);
#else
        private static CppVector TensorShape(IntPtr tensor) => throw UnsupportedAbi();
#endif
        internal static void MNN_Tensor_getShape(IntPtr tensor, int[] shape, int count)
        {
            var values = TakeInts(TensorShape(tensor));
            if (values.Length != count || shape.Length != count)
                throw new InvalidOperationException("Tensor shape changed during access.");
            Array.Copy(values, shape, count);
        }

#endif

#if MNN_INFERENCE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = MNNCoreSymbols.TensorElementSize)]
        internal static extern int MNN_Tensor_elementSize(IntPtr tensor);
#else
        internal static int MNN_Tensor_elementSize(IntPtr tensor) => throw UnsupportedAbi();
#endif
#if MNN_INFERENCE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = MNNCoreSymbols.TensorSize)]
        internal static extern int MNN_Tensor_size(IntPtr tensor);
#else
        internal static int MNN_Tensor_size(IntPtr tensor) => throw UnsupportedAbi();
#endif
#if MNN_INFERENCE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = MNNCoreSymbols.TensorDestroy)]
        internal static extern void MNN_Tensor_destroy(IntPtr tensor);
#else
        internal static void MNN_Tensor_destroy(IntPtr tensor) => throw UnsupportedAbi();
#endif
#if MNN_INFERENCE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = MNNCoreSymbols.TensorWait)]
        internal static extern int MNN_Tensor_wait(IntPtr tensor, MNNTensorMapType mapType, [MarshalAs(UnmanagedType.I1)] bool finish);
#else
        internal static int MNN_Tensor_wait(IntPtr tensor, MNNTensorMapType mapType, [MarshalAs(UnmanagedType.I1)] bool finish) => throw UnsupportedAbi();
#endif
    }
}
