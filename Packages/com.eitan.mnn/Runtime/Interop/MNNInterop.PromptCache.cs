#if UNITY_EDITOR_OSX || (UNITY_STANDALONE_OSX && !UNITY_EDITOR && !ENABLE_IL2CPP) || ((UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR && ENABLE_IL2CPP)
#define MNN_LLM_CPP_ABI
#endif
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using MNN.Unity.Interop.Handles;

namespace MNN.Unity.Interop
{
    internal static partial class MNNInterop
    {
#if MNN_LLM_CPP_ABI
#if UNITY_ANDROID && !UNITY_EDITOR
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZNK3MNN11Transformer3Llm19apply_chat_templateERKNSt6__ndk16vectorINS2_4pairINS2_12basic_stringIcNS2_11char_traitsIcEENS2_9allocatorIcEEEESA_EENS8_ISB_EEEE")]
#else
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZNK3MNN11Transformer3Llm19apply_chat_templateERKNSt3__16vectorINS2_4pairINS2_12basic_stringIcNS2_11char_traitsIcEENS2_9allocatorIcEEEESA_EENS8_ISB_EEEE")]
#endif
        private static extern CppString RenderChat(IntPtr model, ref CppVector messages);
#if UNITY_ANDROID && !UNITY_EDITOR
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Transformer3Llm8responseERKNSt6__ndk16vectorIiNS2_9allocatorIiEEEEPNS2_13basic_ostreamIcNS2_11char_traitsIcEEEEPKci")]
#else
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Transformer3Llm8responseERKNSt3__16vectorIiNS2_9allocatorIiEEEEPNS2_13basic_ostreamIcNS2_11char_traitsIcEEEEPKci")]
#endif
        private static extern void RespondIds(IntPtr model, ref CppVector ids, IntPtr output, [MarshalAs(UnmanagedType.LPUTF8Str)] string end, int limit);
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZNK3MNN11Transformer3Llm17getCurrentHistoryEv")]
        private static extern UIntPtr CurrentHistory(IntPtr model);
#else
        private static CppString RenderChat(IntPtr model, ref CppVector messages) => throw UnsupportedAbi();
        private static void RespondIds(IntPtr model, ref CppVector ids, IntPtr output, string end, int limit) => throw UnsupportedAbi();
        private static UIntPtr CurrentHistory(IntPtr model) => throw UnsupportedAbi();
#endif
        internal static string RenderConversation(LlmHandle handle, IReadOnlyList<MNNChatMessage> messages)
        {
            using (var chat = new ChatVector(messages, null, null))
                return TakeString(RenderChat(handle.DangerousGetHandle(), ref chat.Value));
        }

        private static bool CanCacheChat(LlmHandle handle, IReadOnlyList<MNNChatMessage> messages, string image, string audio, out MNNConversationCacheStatus status)
        {
            status = MNNConversationCacheStatus.Disabled;
            if (!handle.ConversationCacheEnabled) return false;
            status = MNNConversationCacheStatus.Media;
            if (image != null || audio != null) return false;
            foreach (var message in messages)
                if (message.Content.IndexOf("<img>", StringComparison.Ordinal) >= 0 ||
                    message.Content.IndexOf("<audio>", StringComparison.Ordinal) >= 0 ||
                    message.Content.IndexOf("<video>", StringComparison.Ordinal) >= 0) return false;
            // The pinned x64 build failed cached-vs-cold task quality (SmolLM2:
            // Italy -> Florence instead of Rome), with and without count/render calls.
            // Keep independent full prefill until native state continuation is verified.
            if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
            {
                status = MNNConversationCacheStatus.UnsupportedRuntime;
                return false;
            }
            // Speculative strategies have different accepted-token/KV bookkeeping.
            if (!handle.ChatCacheSupported.HasValue)
            {
                handle.ChatCacheSupported = SupportsChatCacheConfiguration(TakeString(DumpConfig(handle.DangerousGetHandle())));
            }
            status = handle.ChatCacheSupported.Value ? MNNConversationCacheStatus.Cold : MNNConversationCacheStatus.UnsupportedModel;
            return handle.ChatCacheSupported.Value;
        }

        internal static bool SupportsChatCacheConfiguration(string configuration)
        {
            var config = MNNModelData.Object(MNNModelData.Parse(configuration));
            int attention = config.TryGetValue("attention_mode", out var value) ? MNNModelData.Integer(value) :
                config.TryGetValue("quant_qkv", out value) ? MNNModelData.Integer(value) : 8;
            // The pinned CPU runtime failed cached-vs-full-prefill task quality
            // with Key INT8 and KV INT8. Loading mode 10 with reuse_kv=false
            // also bypasses the native load-time downgrade to mode 9; set_config
            // does not rebuild that hint. Keep quantized models on full prefill.
            if ((attention & 7) != 0) return false;
            if (config.TryGetValue("has_talker", out value) && value is bool talker && talker) return false;
            return !config.TryGetValue("speculative_type", out value) || string.IsNullOrEmpty(value as string);
        }

        private static void PrefillCachedChat(LlmHandle handle, IReadOnlyList<MNNChatMessage> messages, int limit, out int total, out int reused, out MNNConversationCacheStatus status)
        {
            IntPtr model = handle.DangerousGetHandle();
            int[] tokens = null;
            total = 0;
            reused = 0;
            status = handle.ConversationCacheValid ? MNNConversationCacheStatus.HistoryMismatch : MNNConversationCacheStatus.Cold;
            if (handle.ConversationCacheValid)
            {
                // Only tokenize ahead of response when there is history to compare.
                // A cold response already renders/tokenizes through the official
                // chat overload, including Omni's virtual tokenizer/response work.
                using (var chat = new ChatVector(messages, null, null))
                    tokens = Tokenize(model, TakeString(RenderChat(model, ref chat.Value)));
                total = tokens.Length;
                var context = ReadContext(model);
                int[] history = CopyInts(context.History);
                ulong computed = CurrentHistory(model).ToUInt64();
                // Never rewind a recurrent/linear attention state. Reuse requires the
                // ENTIRE actual token history to match the official newly rendered prompt.
                if (history.Length > 0 && tokens.Length > history.Length &&
                    computed == (ulong)context.AllLength &&
                    (computed == (ulong)history.Length || computed + 1 == (ulong)history.Length) &&
                    HasTokenPrefix(tokens, history))
                {
                    if (computed + 1 == (ulong)history.Length)
                    {
                        // AR leaves the terminal stop token sampled but not forwarded.
                        // Forward it once, without inserting it into history a second time.
                        using (var pending = new IntVector(new[] { history[history.Length - 1] }))
                        {
                            var logits = ForwardIds(model, ref pending.Value, false).Value;
                            try
                            {
                                if (logits.Object == IntPtr.Zero) throw new MNNException(MNNErrorCode.NoExecution, "MNN could not finalize cached history.");
                            }
                            finally { ReleaseShared(ref logits); }
                        }
                    }
                    if (CurrentHistory(model).ToUInt64() == (ulong)history.Length)
                        reused = history.Length;
                }
            }
            handle.ConversationCacheValid = false;
            if (reused == 0)
            {
                ResetLlm(model);
                Configure(model, "{\"reuse_kv\":false,\"prompt_cache\":false}");
                using (var chat = new ChatVector(messages, null, null))
                    RespondChat(model, ref chat.Value, IntPtr.Zero, "", limit);
                if (tokens == null)
                    total = ReadContext(model).PromptLength;
                return;
            }
            Configure(model, "{\"reuse_kv\":true,\"prompt_cache\":false}");
            status = MNNConversationCacheStatus.Reused;
            var suffix = new int[tokens.Length - reused];
            Array.Copy(tokens, reused, suffix, 0, suffix.Length);
            using (var ids = new IntVector(suffix))
                RespondIds(model, ref ids.Value, IntPtr.Zero, "", limit);
        }

        private static bool HasTokenPrefix(int[] tokens, int[] prefix)
        {
            if (tokens.Length < prefix.Length) return false;
            for (int i = 0; i < prefix.Length; ++i)
                if (tokens[i] != prefix[i]) return false;
            return true;
        }
    }
}
