#if UNITY_EDITOR_OSX || (UNITY_STANDALONE_OSX && !UNITY_EDITOR && !ENABLE_IL2CPP)
#define MNN_APPLE_CPP_ABI
#endif
using System;
using System.Runtime.InteropServices;
using MNN.Unity.Interop.Handles;

namespace MNN.Unity.Interop
{
    internal static partial class MNNInterop
    {
#if MNN_APPLE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Transformer3Llm7forwardERKNSt3__16vectorIiNS2_9allocatorIiEEEEb")]
        private static extern IndirectShared ForwardIds(IntPtr model, ref CppVector ids, [MarshalAs(UnmanagedType.I1)] bool prefill);
#else
        private static IndirectShared ForwardIds(IntPtr model, ref CppVector ids, [MarshalAs(UnmanagedType.I1)] bool prefill) => throw UnsupportedAbi();
#endif
        internal static RerankerHandle MNN_Reranker_create(string config, string options)
        {
            IntPtr model = OpenLlm(config, options, true);
            try
            {
                if (Tokenize(model, "yes").Length != 1 || Tokenize(model, "no").Length != 1)
                    throw new NotSupportedException("Reranking requires the Qwen3 yes/no tokenizer contract.");
                return new RerankerHandle(model);
            }
            catch
            {
                MNN_Llm_destroy(model);
                throw;
            }
        }

        internal static int MNN_Reranker_score(RerankerHandle handle, string query, string document, out float score)
        {
            IntPtr model = handle.DangerousGetHandle();
            string prompt = "<|im_start|>system\nJudge whether the Document meets the requirements based on the Query and the Instruct provided. Note that the answer can only be \"yes\" or \"no\".<|im_end|>\n<|im_start|>user\n" + "<Instruct>: Given a web search query, retrieve relevant passages that answer the query\n<Query>: " + query + "\n<Document>: " + document + "<|im_end|>\n<|im_start|>assistant\n<think>\n\n</think>\n\n";
            ResetLlm(model);
            try
            {
                using (var scope = new CpuScope())
                using (var ids = new IntVector(Tokenize(model, prompt)))
                {
                    var values = ReadVariable(ForwardIds(model, ref ids.Value, true).Value, out int width);
                    int yes = Tokenize(model, "yes")[0], no = Tokenize(model, "no")[0];
                    if (yes < 0 || no < 0 || yes >= width || no >= width || values.Length < width)
                    {
                        score = 0;
                        return -1;
                    }

                    double a = values[values.Length - width + yes], b = values[values.Length - width + no];
                    double max = Math.Max(a, b);
                    score = (float)(Math.Exp(a - max) / (Math.Exp(a - max) + Math.Exp(b - max)));
                    return 0;
                }
            }
            finally
            {
                ResetLlm(model);
            }
        }
    }
}
