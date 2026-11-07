#if UNITY_EDITOR_WIN || UNITY_EDITOR_LINUX || ((UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX) && !UNITY_EDITOR && !ENABLE_IL2CPP)
#define MNN_DESKTOP_TASK_ABI
#endif
#if UNITY_EDITOR_OSX || UNITY_EDITOR_WIN || UNITY_EDITOR_LINUX || ((UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX) && !UNITY_EDITOR && !ENABLE_IL2CPP) || (UNITY_STANDALONE_OSX && !UNITY_EDITOR && !ENABLE_IL2CPP) || ((UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR && ENABLE_IL2CPP)
#define MNN_LLM_CPP_ABI
#endif
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using MNN.Unity.Interop.Handles;
using UnityEngine;

namespace MNN.Unity.Interop
{
    internal static partial class MNNInterop
    {
#if MNN_LLM_CPP_ABI
        // Official exported members, shared across all supported compiler ABIs.
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Transformer3Llm4loadEv")]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool LoadTextModel(IntPtr model);

        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Transformer4Omni4loadEv")]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool LoadOmniModel(IntPtr model);

#else
        private static bool LoadTextModel(IntPtr model) => throw UnsupportedAbi();
        private static bool LoadOmniModel(IntPtr model) => throw UnsupportedAbi();
#endif

        [Serializable]
        private sealed class ModelKind
        {
            public bool is_visual, is_audio, has_talker;
        }

        private static bool LoadModel(IntPtr model, bool embedding)
        {
            // createLLM uses these merged public flags to select Llm vs Omni.
            var kind = JsonUtility.FromJson<ModelKind>(DumpConfigurationText(model));
            // Embedding's factory always returns Embedding, irrespective of media flags.
            if (embedding) return LoadEmbeddingModel(model);
            return kind.is_visual || kind.is_audio || kind.has_talker
                ? LoadOmniModel(model) : LoadTextModel(model);
        }

#if MNN_LLM_CPP_ABI
#if UNITY_ANDROID && !UNITY_EDITOR
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Transformer3Llm7forwardERKNSt6__ndk16vectorIiNS2_9allocatorIiEEEEb")]
#elif MNN_DESKTOP_TASK_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Transformer3Llm7forwardERKSt6vectorIiSaIiEEb")]
#else
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Transformer3Llm7forwardERKNSt3__16vectorIiNS2_9allocatorIiEEEEb")]
#endif
        private static extern IndirectShared ForwardIds(IntPtr model, ref CppVector ids, [MarshalAs(UnmanagedType.I1)] bool prefill);
#else
        private static IndirectShared ForwardIds(IntPtr model, ref CppVector ids, [MarshalAs(UnmanagedType.I1)] bool prefill) => throw UnsupportedAbi();
#endif
#if MNN_LLM_CPP_ABI
#if UNITY_ANDROID && !UNITY_EDITOR
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Transformer3Llm9createLLMERKNSt6__ndk112basic_stringIcNS2_11char_traitsIcEENS2_9allocatorIcEEEE")]
#elif MNN_DESKTOP_TASK_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Transformer3Llm9createLLMERKNSt7__cxx1112basic_stringIcSt11char_traitsIcESaIcEEE")]
#else
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Transformer3Llm9createLLMERKNSt3__112basic_stringIcNS2_11char_traitsIcEENS2_9allocatorIcEEEE")]
#endif
        private static extern IntPtr CreateLlm(ref CppString config);
#else
        private static IntPtr CreateLlm(ref CppString config) => throw UnsupportedAbi();
#endif
#if MNN_LLM_CPP_ABI
#if UNITY_ANDROID && !UNITY_EDITOR
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Transformer3Llm10set_configERKNSt6__ndk112basic_stringIcNS2_11char_traitsIcEENS2_9allocatorIcEEEE")]
#elif MNN_DESKTOP_TASK_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Transformer3Llm10set_configERKNSt7__cxx1112basic_stringIcSt11char_traitsIcESaIcEEE")]
#else
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Transformer3Llm10set_configERKNSt3__112basic_stringIcNS2_11char_traitsIcEENS2_9allocatorIcEEEE")]
#endif
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool ConfigureLlm(IntPtr model, ref CppString json);
#else
        private static bool ConfigureLlm(IntPtr model, ref CppString json) => throw UnsupportedAbi();
#endif
#if MNN_LLM_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Transformer3Llm7destroyEPS1_")]
        internal static extern void MNN_Llm_destroy(IntPtr model);
#else
        internal static void MNN_Llm_destroy(IntPtr model) => throw UnsupportedAbi();
#endif
#if MNN_LLM_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Transformer3Llm5resetEv")]
        private static extern void ResetLlm(IntPtr model);
#else
        private static void ResetLlm(IntPtr model) => throw UnsupportedAbi();
#endif
#if MNN_LLM_CPP_ABI
#if UNITY_ANDROID && !UNITY_EDITOR
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Transformer3Llm8responseERKNSt6__ndk112basic_stringIcNS2_11char_traitsIcEENS2_9allocatorIcEEEEPNS2_13basic_ostreamIcS5_EEPKci")]
#elif MNN_DESKTOP_TASK_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Transformer3Llm8responseERKNSt7__cxx1112basic_stringIcSt11char_traitsIcESaIcEEEPSoPKci")]
#else
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Transformer3Llm8responseERKNSt3__112basic_stringIcNS2_11char_traitsIcEENS2_9allocatorIcEEEEPNS2_13basic_ostreamIcS5_EEPKci")]
#endif
        private static extern void RespondLlm(IntPtr model, ref CppString prompt, IntPtr output, [MarshalAs(UnmanagedType.LPUTF8Str)] string end, int limit);
#else
        private static void RespondLlm(IntPtr model, ref CppString prompt, IntPtr output, [MarshalAs(UnmanagedType.LPUTF8Str)] string end, int limit) => throw UnsupportedAbi();
#endif
#if MNN_LLM_CPP_ABI
#if UNITY_ANDROID && !UNITY_EDITOR
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Transformer3Llm16tokenizer_encodeERKNSt6__ndk112basic_stringIcNS2_11char_traitsIcEENS2_9allocatorIcEEEE")]
#elif MNN_DESKTOP_TASK_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Transformer3Llm16tokenizer_encodeERKNSt7__cxx1112basic_stringIcSt11char_traitsIcESaIcEEE")]
#else
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Transformer3Llm16tokenizer_encodeERKNSt3__112basic_stringIcNS2_11char_traitsIcEENS2_9allocatorIcEEEE")]
#endif
        private static extern CppVector EncodeText(IntPtr model, ref CppString text);
#else
        private static CppVector EncodeText(IntPtr model, ref CppString text) => throw UnsupportedAbi();
#endif
#if MNN_LLM_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZNK3MNN11Transformer3Llm10getContextEv")]
        private static extern IntPtr GetLlmContext(IntPtr model);
#else
        private static IntPtr GetLlmContext(IntPtr model) => throw UnsupportedAbi();
#endif
        // Public LlmContext prefix from official llm.hpp. Read only after the
        // synchronous native call completes, under the managed model's lock.
        [StructLayout(LayoutKind.Sequential)]
        private struct LlmContext
        {
            internal int PromptLength, GeneratedLength, AllLength;
            internal IntPtr Stream;
            internal CppString End;
            internal long LoadUs, VisionUs, AudioUs, PrefillUs, DecodeUs, SampleUs, FirstAudioUs;
            internal float Pixels, AudioSeconds;
            internal int CurrentToken;
            internal CppVector History, Output;
            internal CppString GeneratedText;
            internal int Status;
        }

        private static LlmContext ReadContext(IntPtr model)
        {
            IntPtr ptr = GetLlmContext(model);
            if (ptr == IntPtr.Zero)
                throw new MNNException(MNNErrorCode.NoExecution, "MNN returned no LLM context.");
            return Marshal.PtrToStructure<LlmContext>(ptr);
        }

        private static long ElapsedMicroseconds(Stopwatch timer) => timer.ElapsedTicks * 1000000L / Stopwatch.Frequency;

        private static void Configure(IntPtr model, string json)
        {
            using (var text = new NativeString(json))
                if (!ConfigureLlm(model, ref text.Value))
                    throw new MNNException(MNNErrorCode.InvalidValue, "MNN rejected model options.");
        }

        private static IntPtr OpenLlm(string config, string options, bool reranker = false)
        {
            RequireAbi();
            IntPtr model;
            using (var path = new NativeString(config, filePath: true))
                model = CreateLlm(ref path.Value);
            return FinishLoad(model, options, reranker);
        }

        private static IntPtr FinishLoad(IntPtr model, string options, bool reranker = false, bool embedding = false)
        {
            if (model == IntPtr.Zero)
                throw new MNNException(MNNErrorCode.InvalidValue, "MNN model factory failed.");
            try
            {
                Configure(model, options);
                ConfigureLegacyChatTemplate(model);
                if (reranker)
                    Configure(model, "{\"all_logits\":true}");
                if (!LoadModel(model, embedding))
                    throw new MNNException(MNNErrorCode.InvalidValue, "MNN failed to load model weights.");
                return model;
            }
            catch
            {
                MNN_Llm_destroy(model);
                throw;
            }
        }

        internal static LlmHandle MNN_Llm_create(string config, string options) => new LlmHandle(OpenLlm(config, options));
        internal static int MNN_Llm_tokenize(LlmHandle model, string text)
        {
            using (var input = new NativeString(text))
            {
                var tokens = EncodeText(model.DangerousGetHandle(), ref input.Value);
                try
                {
                    return VectorCount(tokens, 4);
                }
                finally
                {
                    IntVectorDestroy(ref tokens);
                }
            }
        }
        private static int[] Tokenize(IntPtr model, string text)
        {
            using (var input = new NativeString(text))
                return TakeInts(EncodeText(model, ref input.Value));
        }

        internal static MNNLlmResult MNN_Llm_generate(LlmHandle handle, string prompt, int limit)
        {
            IntPtr model = handle.DangerousGetHandle();
            handle.ConversationCacheValid = false;
            Configure(model, "{\"reuse_kv\":false,\"prompt_cache\":false}");
            ResetLlm(model);
            using (var input = new NativeString(prompt))
                RespondLlm(model, ref input.Value, IntPtr.Zero, "", limit);
            var context = ReadContext(model);
            CheckGeneration(context);
            return new MNNLlmResult(Encoding.UTF8.GetString(StringBytes(ref context.GeneratedText)), VectorCount(context.Output, 4), context.Status == 2, context.PromptLength, context.LoadUs, context.PrefillUs, context.DecodeUs, context.SampleUs);
        }

        private static void CheckGeneration(LlmContext context)
        {
            if (context.Status != 1 && context.Status != 2)
                throw new MNNException(MNNErrorCode.NoExecution, "MNN generation failed with status " + context.Status);
        }
    }
}
