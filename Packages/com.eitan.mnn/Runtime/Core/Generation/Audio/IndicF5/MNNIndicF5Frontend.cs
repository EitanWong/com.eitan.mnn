using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace MNN.Unity
{
    // IndicF5 character vocabulary and torchaudio Vocos magnitude/HTK features.
    internal sealed class MNNIndicF5Frontend
    {
        private readonly Dictionary<string, int> _tokens = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly double[,] _filters = new double[100, 513];
        private readonly double[] _window = Enumerable.Range(0, 1024).Select(i => .5 - .5 * Math.Cos(2 * Math.PI * i / 1024)).ToArray();

        internal MNNIndicF5Frontend(string vocabulary)
        {
            int i = 0;
            foreach (string line in File.ReadLines(vocabulary, Encoding.UTF8)) _tokens.Add(line, i++);
            if (!_tokens.TryGetValue(" ", out int space) || space != 0) throw new InvalidDataException("Invalid IndicF5 character vocabulary.");
            double high = 2595 * Math.Log10(1 + 12000.0 / 700);
            var hz = Enumerable.Range(0, 102).Select(n => 700 * (Math.Pow(10, high * n / 101 / 2595) - 1)).ToArray();
            for (int m = 0; m < 100; ++m)
            for (int k = 0; k < 513; ++k)
            {
                double f = k * 24000.0 / 1024;
                _filters[m, k] = Math.Max(0, Math.Min((f - hz[m]) / (hz[m + 1] - hz[m]), (hz[m + 2] - f) / (hz[m + 2] - hz[m + 1])));
            }
        }

        internal int[] Encode(string text)
        {
            text = text.Replace(';', ',').Replace('“', '"').Replace('”', '"').Replace('‘', '\'').Replace('’', '\'');
            var values = new List<int>();
            for (int i = 0; i < text.Length; ++i)
            {
                string token = char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]) ? text.Substring(i++, 2) : text[i].ToString();
                values.Add(_tokens.TryGetValue(token, out int id) ? id : 0);
            }
            return values.ToArray();
        }

        internal float[] Mel(float[] pcm, out int frames, CancellationToken cancellationToken)
        {
            frames = pcm.Length / 256 + 1;
            var output = new float[frames * 100]; var re = new double[1024]; var im = new double[1024]; var magnitude = new double[513];
            for (int t = 0; t < frames; ++t)
            {
                cancellationToken.ThrowIfCancellationRequested(); Array.Clear(im, 0, im.Length);
                for (int k = 0; k < 1024; ++k)
                {
                    int at = t * 256 + k - 512;
                    if (at < 0) at = -at;
                    if (at >= pcm.Length) at = pcm.Length * 2 - 2 - at;
                    re[k] = pcm[at] * _window[k];
                }
                MNNSpeechDsp.Fft(re, im, false);
                for (int k = 0; k < 513; ++k) magnitude[k] = Math.Sqrt(re[k] * re[k] + im[k] * im[k]);
                for (int m = 0; m < 100; ++m)
                {
                    double sum = 0;
                    for (int k = 0; k < 513; ++k) sum += magnitude[k] * _filters[m, k];
                    output[t * 100 + m] = (float)Math.Log(Math.Max(1e-5, sum));
                }
            }
            return output;
        }
    }
}
