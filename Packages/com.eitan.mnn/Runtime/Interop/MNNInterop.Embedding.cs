#if UNITY_EDITOR_WIN || UNITY_EDITOR_LINUX || ((UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX) && !UNITY_EDITOR && !ENABLE_IL2CPP)
#define MNN_DESKTOP_TASK_ABI
#endif
#if UNITY_EDITOR_OSX || UNITY_EDITOR_WIN || UNITY_EDITOR_LINUX || ((UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX) && !UNITY_EDITOR && !ENABLE_IL2CPP) || (UNITY_STANDALONE_OSX && !UNITY_EDITOR && !ENABLE_IL2CPP) || ((UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR && ENABLE_IL2CPP)
#define MNN_TASK_CPP_ABI
#endif
using System;
using System.Runtime.InteropServices;
using MNN.Unity.Interop.Handles;

namespace MNN.Unity.Interop
{
    internal static partial class MNNInterop
    {
#if MNN_TASK_CPP_ABI
#if UNITY_ANDROID && !UNITY_EDITOR
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Transformer9Embedding15createEmbeddingERKNSt6__ndk112basic_stringIcNS2_11char_traitsIcEENS2_9allocatorIcEEEEb")]
#elif MNN_DESKTOP_TASK_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Transformer9Embedding15createEmbeddingERKNSt7__cxx1112basic_stringIcSt11char_traitsIcESaIcEEEb")]
#else
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Transformer9Embedding15createEmbeddingERKNSt3__112basic_stringIcNS2_11char_traitsIcEENS2_9allocatorIcEEEEb")]
#endif
        private static extern IntPtr CreateEmbedding(ref CppString path, [MarshalAs(UnmanagedType.I1)] bool load);
#else
        private static IntPtr CreateEmbedding(ref CppString path, [MarshalAs(UnmanagedType.I1)] bool load) => throw UnsupportedAbi();
#endif
#if MNN_TASK_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZNK3MNN11Transformer9Embedding3dimEv")]
        private static extern int EmbeddingDimension(IntPtr model);
#else
        private static int EmbeddingDimension(IntPtr model) => throw UnsupportedAbi();
#endif
#if MNN_TASK_CPP_ABI
#if UNITY_ANDROID && !UNITY_EDITOR
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Transformer9Embedding13txt_embeddingERKNSt6__ndk112basic_stringIcNS2_11char_traitsIcEENS2_9allocatorIcEEEE")]
#elif MNN_DESKTOP_TASK_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Transformer9Embedding13txt_embeddingERKNSt7__cxx1112basic_stringIcSt11char_traitsIcESaIcEEE")]
#else
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Transformer9Embedding13txt_embeddingERKNSt3__112basic_stringIcNS2_11char_traitsIcEENS2_9allocatorIcEEEE")]
#endif
        private static extern IndirectShared EmbedText(IntPtr model, ref CppString text);
#else
        private static IndirectShared EmbedText(IntPtr model, ref CppString text) => throw UnsupportedAbi();
#endif
#if MNN_TASK_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Transformer9Embedding4loadEv")]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool LoadEmbeddingModel(IntPtr model);
#else
        private static bool LoadEmbeddingModel(IntPtr model) => throw UnsupportedAbi();
#endif
        internal static EmbeddingHandle MNN_Embedding_create(string config, string options)
        {
            RequireAbi();
            using (var path = new NativeString(config, filePath: true))
                return new EmbeddingHandle(FinishLoad(CreateEmbedding(ref path.Value, false), options, embedding: true));
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
