#if UNITY_EDITOR_OSX || (UNITY_STANDALONE_OSX && !UNITY_EDITOR && !ENABLE_IL2CPP)
#define MNN_APPLE_CPP_ABI
#endif
using System;
using System.Runtime.InteropServices;

namespace MNN.Unity.Interop
{
    internal static partial class MNNInterop
    {
#if MNN_APPLE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN6Tensor3mapENS0_7MapTypeENS0_13DimensionTypeE")]
        internal static extern IntPtr MNN_Tensor_map(IntPtr tensor, MNNTensorMapType mapType, MNNDimensionType dimType);
#else
        internal static IntPtr MNN_Tensor_map(IntPtr tensor, MNNTensorMapType mapType, MNNDimensionType dimType) => throw UnsupportedAbi();
#endif
#if MNN_APPLE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN6Tensor5unmapENS0_7MapTypeENS0_13DimensionTypeEPv")]
        internal static extern void MNN_Tensor_unmap(IntPtr tensor, MNNTensorMapType mapType, MNNDimensionType dimType, IntPtr pointer);
#else
        internal static void MNN_Tensor_unmap(IntPtr tensor, MNNTensorMapType mapType, MNNDimensionType dimType, IntPtr pointer) => throw UnsupportedAbi();
#endif
#if MNN_APPLE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZNK3MNN6Tensor16getDimensionTypeEv")]
        internal static extern int MNN_Tensor_getDimensionType(IntPtr tensor);
#else
        internal static int MNN_Tensor_getDimensionType(IntPtr tensor) => throw UnsupportedAbi();
#endif
#if MNN_APPLE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZNK3MNN6Tensor7getTypeEv")]
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

#if MNN_APPLE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZNK3MNN6Tensor10dimensionsEv")]
        internal static extern int MNN_Tensor_getDimensions(IntPtr tensor);
#else
        internal static int MNN_Tensor_getDimensions(IntPtr tensor) => throw UnsupportedAbi();
#endif
#if MNN_APPLE_CPP_ABI
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

#if MNN_APPLE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZNK3MNN6Tensor11elementSizeEv")]
        internal static extern int MNN_Tensor_elementSize(IntPtr tensor);
#else
        internal static int MNN_Tensor_elementSize(IntPtr tensor) => throw UnsupportedAbi();
#endif
#if MNN_APPLE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZNK3MNN6Tensor4sizeEv")]
        internal static extern int MNN_Tensor_size(IntPtr tensor);
#else
        internal static int MNN_Tensor_size(IntPtr tensor) => throw UnsupportedAbi();
#endif
#if MNN_APPLE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN6Tensor7destroyEPS0_")]
        internal static extern void MNN_Tensor_destroy(IntPtr tensor);
#else
        internal static void MNN_Tensor_destroy(IntPtr tensor) => throw UnsupportedAbi();
#endif
#if MNN_APPLE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN6Tensor4waitENS0_7MapTypeEb")]
        internal static extern int MNN_Tensor_wait(IntPtr tensor, MNNTensorMapType mapType, [MarshalAs(UnmanagedType.I1)] bool finish);
#else
        internal static int MNN_Tensor_wait(IntPtr tensor, MNNTensorMapType mapType, [MarshalAs(UnmanagedType.I1)] bool finish) => throw UnsupportedAbi();
#endif
    }
}
