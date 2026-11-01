using System;
using MNN.Unity.Interop.Handles;

namespace MNN.Unity.Interop
{
    internal static partial class MNNInterop
    {
        internal static float[] GenerateQwen3Tts(LlmHandle handle, string text, string language, int frames, string reference)
        {
            RequireWaveformAbi();
            return Array.Empty<float>();
        }

        // No supported managed representation of the bundled libc++ std::function
        // manager/invoker exists. Do not construct fake native objects or vtables.
        internal static void RequireWaveformAbi()
        {
            throw new PlatformNotSupportedException(
                "MNN waveform callbacks require std::function marshalling, which is not implemented by the bundled direct C++ ABI. Text and media input remain available.");
        }
    }
}
