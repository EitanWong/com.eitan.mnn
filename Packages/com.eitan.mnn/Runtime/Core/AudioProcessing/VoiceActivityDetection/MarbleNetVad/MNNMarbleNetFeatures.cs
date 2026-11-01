using System;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>NeMo eval frontend: centered symmetric Hann, Slaney mel, no CMVN or dither.</summary>
    internal static class MNNMarbleNetFeatures
    {
        private const int FftSize = 512, WindowSize = 400, HopSize = 160, Bins = 80;
        private static readonly float[] Window = CreateWindow();
        private static readonly float[][] Filters = CreateFilters();

        private static readonly MNNNeMoMelFeatures Frontend = new MNNNeMoMelFeatures(Window, FlattenFilters());
        internal static float[] Extract(float[] samples, out int frames, CancellationToken token) =>
            Frontend.Extract(samples, 2, out frames, out _, token);

        private static float[] FlattenFilters()
        {
            var result = new float[Bins * (FftSize / 2 + 1)];
            for (int i = 0; i < Bins; ++i) Array.Copy(Filters[i], 0, result, i * Filters[i].Length, Filters[i].Length);
            return result;
        }

        private static float[] CreateWindow()
        {
            var window = new float[WindowSize];
            for (int i = 0; i < WindowSize; ++i) window[i] = (float)(.5 - .5 * Math.Cos(2 * Math.PI * i / (WindowSize - 1)));
            return window;
        }

        private static float[][] CreateFilters()
        {
            var edges = new double[Bins + 2];
            double melMax = 15 + Math.Log(8) / (Math.Log(6.4) / 27);
            for (int i = 0; i < edges.Length; ++i)
            {
                double mel = i * melMax / (Bins + 1);
                edges[i] = mel < 15 ? mel * (200.0 / 3) : 1000 * Math.Exp((mel - 15) * (Math.Log(6.4) / 27));
            }
            var filters = new float[Bins][];
            for (int bin = 0; bin < Bins; ++bin)
            {
                filters[bin] = new float[FftSize / 2 + 1];
                for (int i = 0; i < filters[bin].Length; ++i)
                {
                    double hz = i * 16000.0 / FftSize;
                    float triangle = (float)Math.Max(0, Math.Min((hz - edges[bin]) / (edges[bin + 1] - edges[bin]),
                        (edges[bin + 2] - hz) / (edges[bin + 2] - edges[bin + 1])));
                    filters[bin][i] = (float)(triangle * (2 / (edges[bin + 2] - edges[bin])));
                }
            }
            return filters;
        }
    }
}
