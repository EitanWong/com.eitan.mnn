#if UNITY_EDITOR_OSX || (UNITY_STANDALONE_OSX && !UNITY_EDITOR && !ENABLE_IL2CPP)
#define MNN_APPLE_CPP_ABI
#endif
using System;
using System.Runtime.InteropServices;
using System.Text;
using MNN.Unity.Interop.Handles;
using UnityEngine;

namespace MNN.Unity.Interop
{
    internal static partial class MNNInterop
    {
#if MNN_APPLE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Transformer3Llm11dump_configEv")]
        private static extern CppString DumpConfig(IntPtr model);
#else
        private static CppString DumpConfig(IntPtr model) => throw UnsupportedAbi();
#endif
        [Serializable]
        private sealed class CapabilityConfig
        {
            public bool is_visual, is_audio, has_talker;
            public string talker_type, model_type;
            public int image_pad, hidden_size;
        }

        internal static int MNN_Llm_getCapabilities(LlmHandle handle)
        {
            var config = JsonUtility.FromJson<CapabilityConfig>(TakeString(DumpConfig(handle.DangerousGetHandle())));
            bool speech = config.has_talker && string.IsNullOrEmpty(config.talker_type) && string.IsNullOrEmpty(config.model_type) && config.image_pad == 151655 && config.hidden_size == 2048;
            return (config.is_visual ? 1 : 0) | (config.is_audio ? 2 : 0) | (speech ? 4 : 0);
        }

        internal static MNNMultimodalResult GenerateMedia(LlmHandle handle, string prompt, string image, string audio, bool speech, int audioLimit, int limit)
        {
            IntPtr model = handle.DangerousGetHandle();
            ResetLlm(model);
            var before = ReadContext(model);
            // Official Omni string tokenizer accepts local file paths in these tags.
            string tagged = (image == null ? "" : "<img>" + image + "</img>\n") + (audio == null ? "" : "<audio>" + audio + "</audio>\n") + prompt;
            WaveCallback callback = null;
            try
            {
                if (speech)
                {
                    Configure(model, "{\"talker_max_new_tokens\":" + audioLimit + "}");
                    callback = new WaveCallback(model);
                }

                using (var text = new NativeString(tagged))
                    RespondLlm(model, ref text.Value, IntPtr.Zero, "", limit);
                if (speech)
                    Virtual<GenerateWaveFunction>(model, 13)(model);
                var context = ReadContext(model);
                CheckGeneration(context);
                long visionUs = context.VisionUs - before.VisionUs, audioUs = context.AudioUs - before.AudioUs;
                float audioSeconds = context.AudioSeconds - before.AudioSeconds;
                if ((image != null && visionUs <= 0) || (audio != null && (audioUs <= 0 || audioSeconds <= 0)))
                    throw new MNNException(MNNErrorCode.NoExecution, "MNN could not process supplied media.");
                if (callback?.Collector.Error != null)
                    throw new MNNException(MNNErrorCode.NoExecution, "Waveform callback failed: " + callback.Collector.Error.Message);
                float[] waveform = callback == null ? Array.Empty<float>() : callback.Collector.Samples.ToArray();
                if (speech && waveform.Length == 0)
                    throw new MNNException(MNNErrorCode.NoExecution, "MNN produced no speech waveform.");
                return new MNNMultimodalResult(Encoding.UTF8.GetString(StringBytes(ref context.GeneratedText)), VectorCount(context.Output, 4), context.Status == 2, waveform, speech ? 24000 : 0, visionUs, audioUs, audioSeconds);
            }
            finally
            {
                callback?.Dispose();
            }
        }
    }
}
