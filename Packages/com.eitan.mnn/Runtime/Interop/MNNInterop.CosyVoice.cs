#if UNITY_EDITOR_OSX || (UNITY_STANDALONE_OSX && !UNITY_EDITOR && !ENABLE_IL2CPP) || ((UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR && ENABLE_IL2CPP)
#define MNN_TASK_CPP_ABI
#endif
using System;
using System.Runtime.InteropServices;
using MNN.Unity.Interop.Handles;

namespace MNN.Unity.Interop
{
    internal static partial class MNNInterop
    {
        // Official public Llm methods, using this package's existing bundled
        // Apple clang/libc++ 64-bit Mono contract. No new virtual slots/layouts.
#if MNN_TASK_CPP_ABI
#if UNITY_ANDROID && !UNITY_EDITOR
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Transformer3Llm13generate_initEPNSt6__ndk113basic_ostreamIcNS2_11char_traitsIcEEEEPKc")]
#else
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Transformer3Llm13generate_initEPNSt3__113basic_ostreamIcNS2_11char_traitsIcEEEEPKc")]
#endif
        private static extern void InitializeSpeechGeneration(IntPtr model, IntPtr stream, [MarshalAs(UnmanagedType.LPUTF8Str)] string end);
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Transformer3Llm10switchModeENS1_5StageE")]
        private static extern void SwitchSpeechStage(IntPtr model, int stage);
#else
        private static void InitializeSpeechGeneration(IntPtr model, IntPtr stream, string end) => throw UnsupportedAbi();
        private static void SwitchSpeechStage(IntPtr model, int stage) => throw UnsupportedAbi();
#endif
        internal static int[] CosyVoiceEncode(LlmHandle model, string text) => Tokenize(model.DangerousGetHandle(), text);
        internal static void CosyVoiceReset(LlmHandle model)
        {
            ResetLlm(model.DangerousGetHandle());
            InitializeSpeechGeneration(model.DangerousGetHandle(), IntPtr.Zero, "");
        }

        internal static void CosyVoiceForward(LlmHandle model, int[] tokens, bool prefill, float[] logits)
        {
            if (logits == null || logits.Length != MNNCosyVoiceSampler.CompactSize)
                throw new ArgumentException("Expected a reusable CosyVoice speech logits buffer.", nameof(logits));
            using (var ids = new IntVector(tokens))
            {
                SwitchSpeechStage(model.DangerousGetHandle(), prefill ? 0 : 1);
                var variable = ForwardIds(model.DangerousGetHandle(), ref ids.Value, prefill).Value;
                try
                {
                    IntPtr infoPtr = variable.Object == IntPtr.Zero ? IntPtr.Zero : GetVariableInfo(variable.Object);
                    if (infoPtr == IntPtr.Zero)
                        throw new MNNException(MNNErrorCode.NoExecution, "No CosyVoice logits.");
                    var info = Marshal.PtrToStructure<VariableInfo>(infoPtr);
                    int count = checked((int)info.Size.ToUInt64());
                    if (count <= 0 || count > 100000000 || count % MNNCosyVoiceSampler.Vocabulary != 0 ||
                        info.Type.Code != 2 || info.Type.Bits != 32 || info.Type.Lanes != 1)
                        throw new MNNException(MNNErrorCode.NoExecution, "Unexpected CosyVoice logits shape.");
                    IntPtr data = ReadFloatVariable(variable.Object);
                    if (data == IntPtr.Zero)
                        throw new MNNException(MNNErrorCode.NoExecution, "Cannot map CosyVoice logits.");
                    // readMap still synchronizes the native output; copy only the
                    // last row's speech range into managed memory, reusing it.
                    int offset = count - MNNCosyVoiceSampler.Vocabulary + MNNCosyVoiceSampler.Offset;
                    Marshal.Copy(IntPtr.Add(data, checked(offset * sizeof(float))), logits, 0, logits.Length);
                }
                finally
                {
                    if (variable.Object != IntPtr.Zero)
                        DestroyVariable(ref variable);
                }
            }
        }
    }
}
