#if UNITY_EDITOR_OSX || (UNITY_STANDALONE_OSX && !UNITY_EDITOR && !ENABLE_IL2CPP)
#define MNN_APPLE_CPP_ABI
#endif
using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace MNN.Unity.Interop
{
    internal static partial class MNNInterop
    {
#if MNN_APPLE_CPP_ABI
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
        private static bool DecodeStreaming(IntPtr model, int limit, Action<MNNGenerationUpdate> progress, CancellationToken cancellation)
        {
            int previousCount = -1;
            string previousText = null;
            for (int step = 0; step < limit; ++step)
            {
                if (cancellation.IsCancellationRequested)
                    return true;
                GenerateStep(model, 1);
                var context = ReadContext(model);
                CheckGeneration(context);
                int count = VectorCount(context.Output, 4);
                string text = DecodeCompleteUtf8(StringBytes(ref context.GeneratedText));
                if (text != previousText)
                {
                    progress?.Invoke(new MNNGenerationUpdate(text, count));
                    previousText = text;
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
