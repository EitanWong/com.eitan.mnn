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
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Transformer9Embedding15createEmbeddingERKNSt3__112basic_stringIcNS2_11char_traitsIcEENS2_9allocatorIcEEEEb")]
        private static extern IntPtr CreateEmbedding(ref CppString path, [MarshalAs(UnmanagedType.I1)] bool load);
#else
        private static IntPtr CreateEmbedding(ref CppString path, [MarshalAs(UnmanagedType.I1)] bool load) => throw UnsupportedAbi();
#endif
#if MNN_APPLE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZNK3MNN11Transformer9Embedding3dimEv")]
        private static extern int EmbeddingDimension(IntPtr model);
#else
        private static int EmbeddingDimension(IntPtr model) => throw UnsupportedAbi();
#endif
#if MNN_APPLE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Transformer9Embedding13txt_embeddingERKNSt3__112basic_stringIcNS2_11char_traitsIcEENS2_9allocatorIcEEEE")]
        private static extern IndirectShared EmbedText(IntPtr model, ref CppString text);
#else
        private static IndirectShared EmbedText(IntPtr model, ref CppString text) => throw UnsupportedAbi();
#endif
        internal static EmbeddingHandle MNN_Embedding_create(string config, string options)
        {
            RequireAbi();
            using (var path = new NativeString(config))
                return new EmbeddingHandle(FinishLoad(CreateEmbedding(ref path.Value, false), options));
        }

        internal static int MNN_Embedding_getDimension(EmbeddingHandle handle) => EmbeddingDimension(handle.DangerousGetHandle());
        internal static int MNN_Embedding_encode(EmbeddingHandle handle, string text, float[] values, int capacity)
        {
            using (var scope = new CpuScope())
            using (var input = new NativeString(text))
            {
                var output = ReadVariable(EmbedText(handle.DangerousGetHandle(), ref input.Value).Value, out _);
                if (output.Length > capacity)
                    return -1;
                Array.Copy(output, values, output.Length);
                return output.Length;
            }
        }
    }
}
