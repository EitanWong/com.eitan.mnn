using System;
using System.IO;

namespace MNN.Unity
{
    internal sealed class MNNCueLightFeatures
    {
        private readonly float[] _window = new float[400], _filters = new float[257 * 64];
        private readonly float[] _tail = new float[240];
        private readonly MNNFftPlan _fft = new MNNFftPlan(512, false);
        internal MNNCueLightFeatures(string directory)
        {
            byte[] data = File.ReadAllBytes(Path.Combine(directory, MNNCueLightConfig.CoefficientsFile));
            if (!BitConverter.IsLittleEndian || data.Length != 67392) throw new InvalidDataException("Invalid CueLight coefficients.");
            Buffer.BlockCopy(data, 0, _window, 0, 1600); Buffer.BlockCopy(data, 1600, _filters, 0, 257 * 64 * 4);
        }
        internal void Reset() => Array.Clear(_tail, 0, _tail.Length);
        internal float[] Push(float[] chunk)
        {
            var padded = new float[560]; Array.Copy(_tail, padded, 240); Array.Copy(chunk, 0, padded, 240, 320);
            var result = new float[128]; var re = new double[512]; var im = new double[512]; var power = new float[257];
            for (int hop = 0; hop < 2; ++hop)
            {
                Array.Clear(re, 0, 512); Array.Clear(im, 0, 512);
                for (int i = 0; i < 400; ++i) re[i] = padded[hop * 160 + i] * _window[i];
                _fft.Transform(re, im);
                for (int k = 0; k < 257; ++k)
                {
                    float r = (float)re[k], j = (float)im[k];
                    float magnitude = (float)Math.Sqrt((double)r * r + (double)j * j); power[k] = magnitude * magnitude;
                }
                for (int m = 0; m < 64; ++m)
                {
                    double sum = 0;
                    for (int k = 0; k < 257; ++k) sum += (double)power[k] * _filters[k * 64 + m];
                    float energy = (float)sum + 1e-6f;
                    float value = (float)Math.Log(energy);
                    if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidDataException("CueLight features overflowed.");
                    result[hop * 64 + m] = value;
                }
            }
            Array.Copy(padded, 320, _tail, 0, 240);
            return result;
        }
    }
}
