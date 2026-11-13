#if UNITY_EDITOR_WIN || UNITY_EDITOR_LINUX || ((UNITY_STANDALONE_WIN && !UNITY_EDITOR) || (UNITY_STANDALONE_LINUX && !UNITY_EDITOR && !ENABLE_IL2CPP))
#define MNN_DESKTOP_TASK_ABI
#endif
#if UNITY_EDITOR_OSX || UNITY_EDITOR_WIN || UNITY_EDITOR_LINUX || ((UNITY_STANDALONE_WIN && !UNITY_EDITOR) || (UNITY_STANDALONE_LINUX && !UNITY_EDITOR && !ENABLE_IL2CPP)) || (UNITY_STANDALONE_OSX && !UNITY_EDITOR && !ENABLE_IL2CPP) || ((UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR && ENABLE_IL2CPP)
#define MNN_LLM_CPP_ABI
#endif
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR && ENABLE_IL2CPP
#define MNN_MOBILE_LLM_CPP_ABI
#endif
using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace MNN.Unity.Interop
{
    internal static partial class MNNInterop
    {
#if MNN_LLM_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Transformer3Llm8generateEi")]
        private static extern void GenerateStep(IntPtr model, int count);
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Transformer3Llm6stopedEv")]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool GenerationStopped(IntPtr model);
#else
        private static void GenerateStep(IntPtr model, int count) => throw UnsupportedAbi();
        private static bool GenerationStopped(IntPtr model) => throw UnsupportedAbi();
#endif
        // Official llm_demo.cpp uses response(..., 0) followed by generate(1).
        // Context snapshots are read only BETWEEN synchronous calls on the owning worker.
        // No ostream implementation, native polling thread or custom C++ code is involved.
        private static bool DecodeStreaming(IntPtr model, int limit, Action<string, int> progress, bool deltas, CancellationToken cancellation)
        {
            int previousCount = -1;
            int consumedBytes = 0;
            var decoder = new MNNStreamingText(deltas);
            byte[] buffer = Array.Empty<byte>();
            for (int step = 0; step < limit; ++step)
            {
                if (cancellation.IsCancellationRequested)
                    return true;
                GenerateStep(model, 1);
                var context = ReadContext(model);
                CheckGeneration(context);
                int count = VectorCount(context.Output, 4);
                int appended = CopyStringBytes(ref context.GeneratedText, consumedBytes, ref buffer);
                consumedBytes = checked(consumedBytes + appended);
                string text = decoder.Append(buffer, appended);
                if (text != null)
                {
                    progress?.Invoke(text, count);
                }

                if (GenerationStopped(model))
                    return false;
                if (count <= previousCount)
                    throw new MNNException(MNNErrorCode.NoExecution, "MNN decode made no progress.");
                previousCount = count;
            }

            return cancellation.IsCancellationRequested;
        }

        internal static string DecodeCompleteUtf8(byte[] bytes)
        {
            // A token can contain only part of a UTF-8 scalar. Do not show a replacement
            // character while waiting for the rest of a Chinese character or emoji.
            var chars = new char[Encoding.UTF8.GetMaxCharCount(bytes.Length)];
            int count = Encoding.UTF8.GetDecoder().GetChars(bytes, 0, bytes.Length, chars, 0, false);
            return new string (chars, 0, count);
        }
    }
}
