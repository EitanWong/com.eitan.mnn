#if UNITY_EDITOR_OSX || (UNITY_STANDALONE_OSX && !UNITY_EDITOR && !ENABLE_IL2CPP)
#define MNN_APPLE_CPP_ABI
#endif
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using MNN.Unity.Interop.Handles;

namespace MNN.Unity.Interop
{
    internal static partial class MNNInterop
    {
#if MNN_APPLE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Transformer3Llm8responseERKNSt3__16vectorINS2_4pairINS2_12basic_stringIcNS2_11char_traitsIcEENS2_9allocatorIcEEEESA_EENS8_ISB_EEEEPNS2_13basic_ostreamIcS7_EEPKci")]
        private static extern void RespondChat(IntPtr model, ref CppVector messages, IntPtr output, [MarshalAs(UnmanagedType.LPUTF8Str)] string end, int limit);
#else
        private static void RespondChat(IntPtr model, ref CppVector messages, IntPtr output, string end, int limit) => throw UnsupportedAbi();
#endif
        // Official ChatMessages is vector<pair<string,string>> (llm.hpp). Borrowed
        // const storage under the existing MNN 3.6.1 Apple libc++ ABI contract only.
        // Each string is constructed/destructed by libc++; native response cannot retain this vector.
        private sealed class ChatVector : IDisposable
        {
            internal CppVector Value;
            private IntPtr _storage;
            private readonly List<NativeString> _strings = new List<NativeString>();
            internal ChatVector(IReadOnlyList<MNNChatMessage> messages, string image, string audio)
            {
                try
                {
                    const int pairSize = 48;
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
                        Marshal.StructureToPtr(text.Value, IntPtr.Add(_storage, i * pairSize + 24), false);
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

        internal static MNNMultimodalResult GenerateChat(LlmHandle handle, IReadOnlyList<MNNChatMessage> messages, string image, string audio, bool speech, int audioLimit, int limit, Action<MNNGenerationUpdate> progress = null, CancellationToken cancellation = default)
        {
            IntPtr model = handle.DangerousGetHandle();
            ResetLlm(model);
            var before = ReadContext(model);
            WaveCallback callback = null;
            bool cancelled = false;
            try
            {
                if (speech)
                {
                    Configure(model, "{\"talker_max_new_tokens\":" + audioLimit + "}");
                    callback = new WaveCallback(model);
                }

                using (var chat = new ChatVector(messages, image, audio))
                    RespondChat(model, ref chat.Value, IntPtr.Zero, "", progress == null ? limit : 0);
                if (progress != null)
                    cancelled = DecodeStreaming(model, limit, progress, cancellation);
                else
                    cancelled = cancellation.IsCancellationRequested;
                if (speech && !cancelled)
                    Virtual<GenerateWaveFunction>(model, 13)(model);
                cancelled |= cancellation.IsCancellationRequested;
                var context = ReadContext(model);
                if (!cancelled || context.Status != 0)
                    CheckGeneration(context);
                long visionUs = context.VisionUs - before.VisionUs, audioUs = context.AudioUs - before.AudioUs;
                float audioSeconds = context.AudioSeconds - before.AudioSeconds;
                if ((image != null && visionUs <= 0) || (audio != null && (audioUs <= 0 || audioSeconds <= 0)))
                    throw new MNNException(MNNErrorCode.NoExecution, "MNN could not process supplied media.");
                if (callback?.Collector.Error != null)
                    throw new MNNException(MNNErrorCode.NoExecution, "Waveform callback failed: " + callback.Collector.Error.Message);
                float[] waveform = callback == null ? Array.Empty<float>() : callback.Collector.Samples.ToArray();
                if (speech && !cancelled && waveform.Length == 0)
                    throw new MNNException(MNNErrorCode.NoExecution, "MNN produced no speech waveform.");
                return new MNNMultimodalResult(DecodeCompleteUtf8(StringBytes(ref context.GeneratedText)), VectorCount(context.Output, 4), !cancelled && context.Status == 2, waveform, waveform.Length > 0 ? 24000 : 0, visionUs, audioUs, audioSeconds, cancelled);
            }
            finally
            {
                callback?.Dispose();
            }
        }
    }
}
