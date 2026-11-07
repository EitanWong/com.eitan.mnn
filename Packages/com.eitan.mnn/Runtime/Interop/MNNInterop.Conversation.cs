#if UNITY_EDITOR_WIN || UNITY_EDITOR_LINUX || ((UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX) && !UNITY_EDITOR && !ENABLE_IL2CPP)
#define MNN_DESKTOP_TASK_ABI
#endif
#if UNITY_EDITOR_OSX || UNITY_EDITOR_WIN || UNITY_EDITOR_LINUX || ((UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX) && !UNITY_EDITOR && !ENABLE_IL2CPP) || (UNITY_STANDALONE_OSX && !UNITY_EDITOR && !ENABLE_IL2CPP) || ((UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR && ENABLE_IL2CPP)
#define MNN_LLM_CPP_ABI
#endif
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR && ENABLE_IL2CPP
#define MNN_MOBILE_LLM_CPP_ABI
#endif
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using MNN.Unity.Interop.Handles;

namespace MNN.Unity.Interop
{
    internal static partial class MNNInterop
    {
#if MNN_LLM_CPP_ABI
#if UNITY_ANDROID && !UNITY_EDITOR
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Transformer3Llm8responseERKNSt6__ndk16vectorINS2_4pairINS2_12basic_stringIcNS2_11char_traitsIcEENS2_9allocatorIcEEEESA_EENS8_ISB_EEEEPNS2_13basic_ostreamIcS7_EEPKci")]
#elif MNN_DESKTOP_TASK_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Transformer3Llm8responseERKSt6vectorISt4pairINSt7__cxx1112basic_stringIcSt11char_traitsIcESaIcEEES9_ESaISA_EEPSoPKci")]
#else
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Transformer3Llm8responseERKNSt3__16vectorINS2_4pairINS2_12basic_stringIcNS2_11char_traitsIcEENS2_9allocatorIcEEEESA_EENS8_ISB_EEEEPNS2_13basic_ostreamIcS7_EEPKci")]
#endif
        private static extern void RespondChat(IntPtr model, ref CppVector messages, IntPtr output, [MarshalAs(UnmanagedType.LPUTF8Str)] string end, int limit);
#else
        private static void RespondChat(IntPtr model, ref CppVector messages, IntPtr output, string end, int limit) => throw UnsupportedAbi();
#endif
        // Official ChatMessages is vector<pair<string,string>> (llm.hpp). Borrowed
        // const storage under the pinned MNN 3.6.2 compiler/STL ABI contract.
        // Each string keeps stable native storage; native response cannot retain this vector.
        private sealed class ChatVector : IDisposable
        {
            internal CppVector Value;
            private IntPtr _storage;
            private readonly List<NativeString> _strings = new List<NativeString>();
            internal ChatVector(IReadOnlyList<MNNChatMessage> messages, string image, string audio)
            {
                try
                {
                    int stringSize = Marshal.SizeOf<CppString>();
                    int pairSize = 2 * stringSize;
                    _storage = Marshal.AllocHGlobal(checked(messages.Count * pairSize));
                    for (int i = 0; i < messages.Count; ++i)
                    {
                        string content = messages[i].Content;
                        if (i == messages.Count - 1)
                            content = (image == null ? "" : "<img>" + image + "</img>\n") + (audio == null ? "" : "<audio>" + audio + "</audio>\n") + content;
                        var role = new NativeString(messages[i].Role.ToString().ToLowerInvariant());
                        _strings.Add(role);
                        var text = new NativeString(content);
                        _strings.Add(text);
                        Marshal.StructureToPtr(role.Value, IntPtr.Add(_storage, i * pairSize), false);
                        Marshal.StructureToPtr(text.Value, IntPtr.Add(_storage, i * pairSize + stringSize), false);
                    }

                    IntPtr end = IntPtr.Add(_storage, checked(messages.Count * pairSize));
                    Value = new CppVector{Begin = _storage, End = end, Capacity = end};
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            public void Dispose()
            {
                foreach (var text in _strings)
                    text.Dispose();
                _strings.Clear();
                if (_storage != IntPtr.Zero)
                    Marshal.FreeHGlobal(_storage);
                _storage = IntPtr.Zero;
                Value = default;
            }
        }

        internal static MNNMultimodalResult GenerateChat(LlmHandle handle, IReadOnlyList<MNNChatMessage> messages, string image, string audio, bool speech, int audioLimit, int limit, Action<MNNGenerationUpdate> progress = null, CancellationToken cancellation = default, Action<MNNGenerationDelta> delta = null)
        {
            // Reject unsupported speech before entering recovery, which needs a valid model.
            if (speech) RequireWaveformAbi();
            try
            {
                return GenerateChatCore(handle, messages, image, audio, speech, audioLimit, limit, progress, cancellation, delta);
            }
            catch
            {
                handle.ConversationCacheValid = false;
                ResetLlm(handle.DangerousGetHandle());
                throw;
            }
        }

        private static MNNMultimodalResult GenerateChatCore(LlmHandle handle, IReadOnlyList<MNNChatMessage> messages, string image, string audio, bool speech, int audioLimit, int limit, Action<MNNGenerationUpdate> progress, CancellationToken cancellation, Action<MNNGenerationDelta> delta)
        {
            IntPtr model = handle.DangerousGetHandle();
            bool cancelled;
            var textTimer = Stopwatch.StartNew();
            long firstTextUs = 0, callbackTicks = 0;
            bool firstTextDelivered = false;
            bool streaming = progress != null || delta != null;
            bool cache = CanCacheChat(handle, messages, image, audio, out var cacheStatus);
            int total = 0, reused = 0;
            if (!cache)
            {
                handle.ConversationCacheValid = false;
                ResetLlm(model);
                Configure(model, "{\"reuse_kv\":false,\"prompt_cache\":false}");
            }
            var before = ReadContext(model);
            if (cache)
                PrefillCachedChat(handle, messages, streaming ? 0 : limit, out total, out reused, out cacheStatus);
            else
                using (var chat = new ChatVector(messages, image, audio))
                    RespondChat(model, ref chat.Value, IntPtr.Zero, "", streaming ? 0 : limit);
            if (streaming)
                cancelled = DecodeStreaming(model, limit, (text, count) =>
                {
                    if (!firstTextDelivered)
                    {
                        firstTextDelivered = true;
                        firstTextUs = ElapsedMicroseconds(textTimer);
                    }
                    long started = Stopwatch.GetTimestamp();
                    try
                    {
                        if (delta != null)
                            delta(new MNNGenerationDelta(text, count));
                        else
                            progress(new MNNGenerationUpdate(text, count));
                    }
                    finally { callbackTicks += Stopwatch.GetTimestamp() - started; }
                }, delta != null, cancellation);
            else
                cancelled = cancellation.IsCancellationRequested;
            long textGenerationUs = ElapsedMicroseconds(textTimer);
            cancelled |= cancellation.IsCancellationRequested;
            var context = ReadContext(model);
            if (!cancelled || context.Status != 0)
                CheckGeneration(context);
            long visionUs = context.VisionUs - before.VisionUs, audioUs = context.AudioUs - before.AudioUs;
            float audioSeconds = context.AudioSeconds - before.AudioSeconds;
            if ((image != null && visionUs <= 0) || (audio != null && (audioUs <= 0 || audioSeconds <= 0)))
                throw new MNNException(MNNErrorCode.NoExecution, "MNN could not process supplied media.");
            var result = new MNNMultimodalResult(DecodeCompleteUtf8(StringBytes(ref context.GeneratedText)), VectorCount(context.Output, 4), !cancelled && context.Status == 2, Array.Empty<float>(), 0, visionUs, audioUs, audioSeconds, textGenerationUs, 0, cancelled, CopyInts(context.Output), cache ? total - reused : context.PromptLength, context.PrefillUs, context.DecodeUs, context.SampleUs, firstTextUs, callbackTicks * 1000000L / Stopwatch.Frequency, reused, cache ? total : context.PromptLength, cancelled ? MNNConversationCacheStatus.Interrupted : cacheStatus);
            handle.ConversationCacheValid = cache && !cancelled;
            if (cancelled)
            {
                ResetLlm(model);
            }
            return result;
        }
    }
}
