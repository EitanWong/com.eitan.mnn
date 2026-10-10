#if UNITY_EDITOR_OSX || (UNITY_STANDALONE_OSX && !UNITY_EDITOR && !ENABLE_IL2CPP)
#define MNN_APPLE_CPP_ABI
#endif
using System;
using System.Runtime.InteropServices;
using System.Text;
using MNN.Unity.Interop.Handles;

namespace MNN.Unity.Interop
{
    internal static partial class MNNInterop
    {
#if MNN_APPLE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Transformer3Llm9createLLMERKNSt3__112basic_stringIcNS2_11char_traitsIcEENS2_9allocatorIcEEEE")]
        private static extern IntPtr CreateLlm(ref CppString config);
#else
        private static IntPtr CreateLlm(ref CppString config) => throw UnsupportedAbi();
#endif
#if MNN_APPLE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Transformer3Llm10set_configERKNSt3__112basic_stringIcNS2_11char_traitsIcEENS2_9allocatorIcEEEE")]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool ConfigureLlm(IntPtr model, ref CppString json);
#else
        private static bool ConfigureLlm(IntPtr model, ref CppString json) => throw UnsupportedAbi();
#endif
#if MNN_APPLE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Transformer3Llm7destroyEPS1_")]
        internal static extern void MNN_Llm_destroy(IntPtr model);
#else
        internal static void MNN_Llm_destroy(IntPtr model) => throw UnsupportedAbi();
#endif
#if MNN_APPLE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Transformer3Llm5resetEv")]
        private static extern void ResetLlm(IntPtr model);
#else
        private static void ResetLlm(IntPtr model) => throw UnsupportedAbi();
#endif
#if MNN_APPLE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Transformer3Llm8responseERKNSt3__112basic_stringIcNS2_11char_traitsIcEENS2_9allocatorIcEEEEPNS2_13basic_ostreamIcS5_EEPKci")]
        private static extern void RespondLlm(IntPtr model, ref CppString prompt, IntPtr output, [MarshalAs(UnmanagedType.LPUTF8Str)] string end, int limit);
#else
        private static void RespondLlm(IntPtr model, ref CppString prompt, IntPtr output, [MarshalAs(UnmanagedType.LPUTF8Str)] string end, int limit) => throw UnsupportedAbi();
#endif
#if MNN_APPLE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Transformer3Llm16tokenizer_encodeERKNSt3__112basic_stringIcNS2_11char_traitsIcEENS2_9allocatorIcEEEE")]
        private static extern CppVector EncodeText(IntPtr model, ref CppString text);
#else
        private static CppVector EncodeText(IntPtr model, ref CppString text) => throw UnsupportedAbi();
#endif
#if MNN_APPLE_CPP_ABI
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
            using (var path = new NativeString(config))
                model = CreateLlm(ref path.Value);
            return FinishLoad(model, options, reranker);
        }

        private static IntPtr FinishLoad(IntPtr model, string options, bool reranker = false)
        {
            if (model == IntPtr.Zero)
                throw new MNNException(MNNErrorCode.InvalidValue, "MNN model factory failed.");
            try
            {
                Configure(model, options);
                if (reranker)
                    Configure(model, "{\"all_logits\":true}");
                // Llm::load is virtual: respect Omni / Embedding overrides.
                if (!LoadVirtual(model))
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
        internal static int MNN_Llm_tokenize(LlmHandle model, string text) => Tokenize(model.DangerousGetHandle(), text).Length;
        private static int[] Tokenize(IntPtr model, string text)
        {
            using (var input = new NativeString(text))
                return TakeInts(EncodeText(model, ref input.Value));
        }

        internal static MNNLlmResult MNN_Llm_generate(LlmHandle handle, string prompt, int limit)
        {
            IntPtr model = handle.DangerousGetHandle();
            ResetLlm(model);
            using (var input = new NativeString(prompt))
                RespondLlm(model, ref input.Value, IntPtr.Zero, "", limit);
            var context = ReadContext(model);
            CheckGeneration(context);
            return new MNNLlmResult(Encoding.UTF8.GetString(StringBytes(ref context.GeneratedText)), VectorCount(context.Output, 4), context.Status == 2);
        }

        private static void CheckGeneration(LlmContext context)
        {
            if (context.Status != 1 && context.Status != 2)
                throw new MNNException(MNNErrorCode.NoExecution, "MNN generation failed with status " + context.Status);
        }
    }
}
