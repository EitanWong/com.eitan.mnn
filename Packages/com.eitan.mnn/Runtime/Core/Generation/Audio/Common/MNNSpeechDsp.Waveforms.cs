using System;
using System.Collections.Generic;
using System.Linq;

namespace MNN.Unity
{
    internal static partial class MNNSpeechDsp
    {
        internal static float[] JoinWaveforms(IReadOnlyList<float[]> waves, int sampleRate)
        {
            if (waves.Count == 1)
                return waves[0];
            int pause = sampleRate * 80 / 1000;
            int count = checked(waves.Sum(wave => wave.Length) + pause * (waves.Count - 1));
            var result = new float[count];
            int offset = 0;
            foreach (float[] wave in waves)
            {
                Array.Copy(wave, 0, result, offset, wave.Length);
                // Five milliseconds at joins remove discontinuities without
                // overlapping or truncating speech from adjacent segments.
                int fade = Math.Min(sampleRate * 5 / 1000, wave.Length / 2);
                for (int i = 0; i < fade; ++i)
                {
                    float gain = i / (float)fade;
                    result[offset + i] *= gain;
                    result[offset + wave.Length - 1 - i] *= gain;
                }

                offset += wave.Length + pause;
            }

            return result;
        }
    }
}
