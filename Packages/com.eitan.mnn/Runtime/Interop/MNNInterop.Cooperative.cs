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
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Transformer3Llm13generate_initEPNSt3__213basic_ostreamIcNS2_11char_traitsIcEEEEPKc")]
        private static extern void InitializeCooperativeGeneration(IntPtr model, IntPtr output, [MarshalAs(UnmanagedType.LPUTF8Str)] string end);
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Transformer3Llm8generateERKNSt3__26vectorIiNS2_9allocatorIiEEEEi")]
        private static extern CppVector PrefillCooperativeIds(IntPtr model, ref CppVector ids, int limit);
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZNK3MNN11Transformer3Llm19apply_chat_templateERKNSt3__212basic_stringIcNS2_11char_traitsIcEENS2_9allocatorIcEEEE")]
        private static extern CppString RenderCooperativePrompt(IntPtr model, ref CppString input);

        // Iterator boundaries are outside native calls: no suspended C++ stack,
        // borrowed native vectors, or native-call scope survives a Unity frame.
        internal static IEnumerable<MNNGenerationDelta> GenerateCooperatively(LlmHandle handle, string prompt,
            IReadOnlyList<MNNChatMessage> messages, int limit, Action<MNNMultimodalResult> complete,
            CancellationToken cancellation)
        {
            IntPtr model = handle.DangerousGetHandle();
            bool completed = false;
            bool initialized = false;
            int promptCount = 0;
            var timer = Stopwatch.StartNew();
            long firstTextUs = 0;
            handle.ConversationCacheValid = false;
            try
            {
                cancellation.ThrowIfCancellationRequested();
                Configure(model, "{\"reuse_kv\":false,\"prompt_cache\":false}");
                ResetLlm(model);
                string rendered = prompt;
                if (messages != null)
                    rendered = RenderConversation(handle, messages);
                else
                {
                    var config = MNNModelData.Object(MNNModelData.Parse(DumpConfigurationText(model)));
                    if (!config.TryGetValue("use_template", out var enabled) || !(enabled is bool value) || value)
                    {
                        using (var text = new NativeString(prompt))
                        {
                            string templated = TakeString(RenderCooperativePrompt(model, ref text.Value));
                            if (!string.IsNullOrEmpty(templated)) rendered = templated;
                        }
                    }
                }
                int[] tokens = Tokenize(model, rendered);
                promptCount = tokens.Length;
                if (tokens.Length == 0) throw new MNNException(MNNErrorCode.InvalidValue, "The rendered prompt has no tokens.");
                InitializeCooperativeGeneration(model, IntPtr.Zero, "");
                initialized = true;
                yield return default;
                // Official generate(ids,0) preserves KV between calls following
                // one generate_init. One token bounds work without altering weights.
                for (int offset = 0; offset < tokens.Length; ++offset)
                {
                    cancellation.ThrowIfCancellationRequested();
                    using (var input = new IntVector(new[] { tokens[offset] }))
                        TakeInts(PrefillCooperativeIds(model, ref input.Value, 0));
                    // generate(ids,0) leaves public LlmStatus RUNNING=0.
                    int status = ReadContext(model).Status;
                    if (status != 0) throw new MNNException(MNNErrorCode.NoExecution, "MNN prefill failed with status " + status);
                    yield return default;
                }
                var decoder = new MNNStreamingText(true);
                byte[] buffer = Array.Empty<byte>();
                int consumedBytes = 0, previousCount = -1;
                for (int step = 0; step < limit; ++step)
                {
                    cancellation.ThrowIfCancellationRequested();
                    GenerateStep(model, 1);
                    var context = ReadContext(model);
                    CheckGeneration(context);
                    int count = VectorCount(context.Output, 4);
                    int appended = CopyStringBytes(ref context.GeneratedText, consumedBytes, ref buffer);
                    consumedBytes = checked(consumedBytes + appended);
                    string text = decoder.Append(buffer, appended);
                    if (!string.IsNullOrEmpty(text) && firstTextUs == 0) firstTextUs = ElapsedMicroseconds(timer);
                    yield return new MNNGenerationDelta(text, count);
                    if (GenerationStopped(model)) break;
                    if (count <= previousCount) throw new MNNException(MNNErrorCode.NoExecution, "MNN decode made no progress.");
                    previousCount = count;
                }
                cancellation.ThrowIfCancellationRequested();
                var final = ReadContext(model);
                complete(new MNNMultimodalResult(DecodeCompleteUtf8(StringBytes(ref final.GeneratedText)),
                    VectorCount(final.Output, 4), final.Status == 2, Array.Empty<float>(), 0, 0, 0, 0,
                    textGenerationUs: ElapsedMicroseconds(timer), tokenIds: CopyInts(final.Output), promptTokens: tokens.Length,
                    prefillUs: final.PrefillUs, decodeUs: final.DecodeUs, sampleUs: final.SampleUs,
                    firstTextUs: firstTextUs, cacheStatus: MNNConversationCacheStatus.Disabled));
                completed = true;
            }
            finally
            {
                // Cancellation and callback exceptions discard partial KV state.
                if (!completed)
                {
                    if (cancellation.IsCancellationRequested)
                    {
                        var partial = ReadContext(model);
                        complete(new MNNMultimodalResult(initialized ? DecodeCompleteUtf8(StringBytes(ref partial.GeneratedText)) : "",
                            initialized ? VectorCount(partial.Output, 4) : 0, false, Array.Empty<float>(), 0, 0, 0, 0,
                            textGenerationUs: ElapsedMicroseconds(timer), cancelled: true,
                            tokenIds: initialized ? CopyInts(partial.Output) : Array.Empty<int>(), promptTokens: promptCount,
                            prefillUs: initialized ? partial.PrefillUs : 0, decodeUs: initialized ? partial.DecodeUs : 0,
                            sampleUs: initialized ? partial.SampleUs : 0, firstTextUs: firstTextUs,
                            cacheStatus: MNNConversationCacheStatus.Interrupted));
                    }
                    ResetLlm(model);
                }
            }
        }
#endif

        internal static MNNLlmResult GenerateTextStreaming(LlmHandle handle, string prompt, int limit,
            Action<MNNGenerationDelta> progress, CancellationToken cancellation)
        {
            IntPtr model = handle.DangerousGetHandle();
            handle.ConversationCacheValid = false;
            try
            {
                cancellation.ThrowIfCancellationRequested();
                Configure(model, "{\"reuse_kv\":false,\"prompt_cache\":false}");
                ResetLlm(model);
                using (var input = new NativeString(prompt)) RespondLlm(model, ref input.Value, IntPtr.Zero, "", 0);
                if (DecodeStreaming(model, limit, (text, count) => progress?.Invoke(new MNNGenerationDelta(text, count)), true, cancellation))
                    throw new OperationCanceledException(cancellation);
                var context = ReadContext(model);
                CheckGeneration(context);
                return new MNNLlmResult(DecodeCompleteUtf8(StringBytes(ref context.GeneratedText)), VectorCount(context.Output, 4),
                    context.Status == 2, context.PromptLength, context.LoadUs, context.PrefillUs, context.DecodeUs, context.SampleUs);
            }
            catch { ResetLlm(model); throw; }
        }
    }
}
