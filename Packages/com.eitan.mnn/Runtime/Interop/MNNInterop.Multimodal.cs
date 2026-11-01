#if UNITY_EDITOR_OSX || (UNITY_STANDALONE_OSX && !UNITY_EDITOR && !ENABLE_IL2CPP) || ((UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR && ENABLE_IL2CPP)
#define MNN_LLM_CPP_ABI
#endif
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR && ENABLE_IL2CPP
#define MNN_MOBILE_LLM_CPP_ABI
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
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Transformer3Llm11dump_configEv")]
        private static extern CppString DumpConfig(IntPtr model);
#else
        private static CppString DumpConfig(IntPtr model) => throw UnsupportedAbi();
#endif
        [Serializable]
        private sealed class CapabilityConfig
        {
            public bool is_visual, is_audio;
        }

        internal static string ReadLlmConfiguration(LlmHandle handle)
            => TakeString(DumpConfig(handle.DangerousGetHandle()));

        internal static int MNN_Llm_getCapabilities(LlmHandle handle)
        {
            var config = JsonUtility.FromJson<CapabilityConfig>(TakeString(DumpConfig(handle.DangerousGetHandle())));
            // Model talker flags alone do not establish a managed waveform ABI.
            return (config.is_visual ? 1 : 0) | (config.is_audio ? 2 : 0);
        }

        internal static MNNMultimodalResult GenerateMedia(LlmHandle handle, string prompt, string image, string audio, bool speech, int audioLimit, int limit)
        {
            if (speech) RequireWaveformAbi();
            IntPtr model = handle.DangerousGetHandle();
            handle.ConversationCacheValid = false;
            Configure(model, "{\"reuse_kv\":false,\"prompt_cache\":false}");
            ResetLlm(model);
            var before = ReadContext(model);
            // Official Omni string tokenizer accepts local file paths in these tags.
            string tagged = (image == null ? "" : "<img>" + image + "</img>\n") + (audio == null ? "" : "<audio>" + audio + "</audio>\n") + prompt;
            var textTimer = Stopwatch.StartNew();
            using (var text = new NativeString(tagged))
                RespondLlm(model, ref text.Value, IntPtr.Zero, "", limit);
            long textGenerationUs = ElapsedMicroseconds(textTimer);
            var context = ReadContext(model);
            CheckGeneration(context);
            long visionUs = context.VisionUs - before.VisionUs, audioUs = context.AudioUs - before.AudioUs;
            float audioSeconds = context.AudioSeconds - before.AudioSeconds;
            if ((image != null && visionUs <= 0) || (audio != null && (audioUs <= 0 || audioSeconds <= 0)))
                throw new MNNException(MNNErrorCode.NoExecution, "MNN could not process supplied media.");
            return new MNNMultimodalResult(Encoding.UTF8.GetString(StringBytes(ref context.GeneratedText)), VectorCount(context.Output, 4), context.Status == 2, Array.Empty<float>(), 0, visionUs, audioUs, audioSeconds, textGenerationUs, 0, tokenIds: CopyInts(context.Output), promptTokens: context.PromptLength, prefillUs: context.PrefillUs, decodeUs: context.DecodeUs, sampleUs: context.SampleUs);
        }
    }
}
