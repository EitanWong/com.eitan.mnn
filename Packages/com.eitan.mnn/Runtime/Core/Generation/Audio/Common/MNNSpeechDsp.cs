// Managed frontend and synthesis math following the published IndexTTS-2.5,
// FireRedTTS3 and SeamlessM4T reference algorithms. See LICENSE.md.
using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace MNN.Unity
{
    internal static partial class MNNSpeechDsp
    {
        internal static float[] CausalMask(int length, int window = int.MaxValue)
        {
            if (length < 1 || length > 4096)
                throw new InvalidDataException("Speech attention length exceeds the supported bound.");
            var mask = new float[checked(length * length)];
            for (int i = 0; i < length; ++i)
                for (int j = 0; j < length; ++j)
                    if (j > i || i - j >= window)
                        mask[i * length + j] = -1e9f;
            return mask;
        }

        internal static float[] Join(params float[][] values)
        {
            var result = new float[values.Sum(x => x.Length)];
            int offset = 0;
            foreach (var value in values)
            {
                Array.Copy(value, 0, result, offset, value.Length);
                offset += value.Length;
            }

            return result;
        }

        internal static float[] Tail(float[] values, int count)
        {
            if (count < 0 || count > values.Length)
                throw new InvalidDataException("Unexpected speech tensor shape.");
            var tail = new float[count];
            Array.Copy(values, values.Length - count, tail, 0, count);
            return tail;
        }

        internal static void ValidateText(string text, int maximum = 200)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length > maximum || text.Contains("<|") || text.Any(c => char.IsControl(c) && !char.IsWhiteSpace(c)))
                throw new ArgumentException("Enter plain speech text (1–" + maximum + " characters); control tokens are unsupported.", nameof(text));
            new System.Text.UTF8Encoding(false, true).GetBytes(text);
        }

        internal static MNNGeneratedAudio Result(float[] wave, int rate, string text)
        {
            if (wave.Length < rate / 10 || wave.Any(x => float.IsNaN(x) || float.IsInfinity(x)) || wave.Sum(x => (double)x * x) / wave.Length < 1e-9)
                throw new InvalidDataException("Speech generation returned invalid or silent audio.");
            for (int i = 0; i < wave.Length; ++i)
                wave[i] = Math.Max(-1, Math.Min(1, wave[i]));
            return new MNNGeneratedAudio(wave, rate, text);
        }
    }
}
