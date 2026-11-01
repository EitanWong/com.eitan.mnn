using System;
using System.Linq;
using System.Threading;

namespace MNN.Unity
{
    // SenseVoiceSmall: PCM scaled by 32768, Hamming, snipped edges, 20–8000 Hz,
    // stack seven frames every six frames, then apply the model's fixed CMVN.
    internal static class MNNSenseVoiceFeatures
    {
        private sealed class Filter
        {
            internal int Start;
            internal double[] Weights;
        }

        private static readonly float[] NegativeMean =
        {
            -8.311879f, -8.600912f, -9.615928f, -10.43595f, -11.21292f, -11.88333f, -12.36243f, -12.63706f,
            -12.8818f, -12.83066f, -12.89103f, -12.95666f, -13.19763f, -13.40598f, -13.49113f, -13.5546f,
            -13.55639f, -13.51915f, -13.68284f, -13.53289f, -13.42107f, -13.65519f, -13.50713f, -13.75251f,
            -13.76715f, -13.87408f, -13.73109f, -13.70412f, -13.56073f, -13.53488f, -13.54895f, -13.56228f,
            -13.59408f, -13.62047f, -13.64198f, -13.66109f, -13.62669f, -13.58297f, -13.57387f, -13.4739f,
            -13.53063f, -13.48348f, -13.61047f, -13.64716f, -13.71546f, -13.79184f, -13.90614f, -14.03098f,
            -14.18205f, -14.35881f, -14.48419f, -14.60172f, -14.70591f, -14.83362f, -14.92122f, -15.00622f,
            -15.05122f, -15.03119f, -14.99028f, -14.92302f, -14.86927f, -14.82691f, -14.7972f, -14.76909f,
            -14.71356f, -14.61277f, -14.51696f, -14.42252f, -14.36405f, -14.30451f, -14.23161f, -14.19851f,
            -14.16633f, -14.15649f, -14.10504f, -13.99518f, -13.79562f, -13.3996f, -12.7767f, -11.71208f
        };

        private static readonly float[] InverseDeviation =
        {
            0.155775f, 0.154484f, 0.1527379f, 0.1518718f, 0.1506028f, 0.1489256f, 0.147067f, 0.1447061f,
            0.1436307f, 0.1443568f, 0.1451849f, 0.1455157f, 0.1452821f, 0.1445717f, 0.1439195f, 0.1435867f,
            0.1436018f, 0.1438781f, 0.1442086f, 0.1448844f, 0.1454756f, 0.145663f, 0.146268f, 0.1467386f,
            0.1472724f, 0.147664f, 0.1480913f, 0.1483739f, 0.1488841f, 0.1493636f, 0.1497088f, 0.1500379f,
            0.1502916f, 0.1505389f, 0.1506787f, 0.1507102f, 0.1505992f, 0.1505445f, 0.1505938f, 0.1508133f,
            0.1509569f, 0.1512396f, 0.1514625f, 0.1516195f, 0.1516156f, 0.1515561f, 0.1514966f, 0.1513976f,
            0.1512612f, 0.151076f, 0.1510596f, 0.1510431f, 0.151077f, 0.1511168f, 0.1511917f, 0.151023f,
            0.1508045f, 0.1505885f, 0.1503493f, 0.1502373f, 0.1501726f, 0.1500762f, 0.1500065f, 0.1499782f,
            0.150057f, 0.1502658f, 0.150469f, 0.1505335f, 0.1505505f, 0.1505328f, 0.1504275f, 0.1502438f,
            0.1499674f, 0.1497118f, 0.1494661f, 0.1493102f, 0.1493681f, 0.1495501f, 0.1499738f, 0.1509654f
        };

        private static readonly double[] Window = Enumerable.Range(0, 400)
            .Select(n => .54 - .46 * Math.Cos(2 * Math.PI * n / 399)).ToArray();
        private static readonly Filter[] Filters = CreateFilters();

        private static Filter[] CreateFilters()
        {
            double low = Mel(20), delta = (Mel(8000) - low) / 81;
            return Enumerable.Range(0, 80).Select(m =>
            {
                var weights = Enumerable.Range(0, 256).Select(k => Math.Max(0,
                    Math.Min((Mel(k * 16000.0 / 512) - low - m * delta) / delta,
                    (low + (m + 2) * delta - Mel(k * 16000.0 / 512)) / delta))).ToArray();
                int first = Array.FindIndex(weights, x => x > 0), last = Array.FindLastIndex(weights, x => x > 0);
                return new Filter {Start = first, Weights = weights.Skip(first).Take(last - first + 1).ToArray()};
            }).ToArray();
        }

        private static double Mel(double hz) => 1127 * Math.Log(1 + hz / 700);

        internal static float[] Extract(float[] samples, out int frames, CancellationToken token)
        {
            if (samples == null || samples.Length == 0)
                throw new ArgumentException("Expected nonempty 16 kHz mono PCM.", nameof(samples));
            int inputFrames = samples.Length < 400 ? 0 : (samples.Length - 400) / 160 + 1;
            if (inputFrames < 7) throw new ArgumentException("SenseVoice requires at least 85 ms of audio.", nameof(samples));
            frames = (inputFrames - 7) / 6 + 1;
            var output = new float[checked(inputFrames * 80)];
            var real = new double[512];
            var imaginary = new double[512];
            var window = new double[400];
            for (int f = 0; f < inputFrames; ++f)
            {
                if ((f & 31) == 0) token.ThrowIfCancellationRequested();
                double mean = 0;
                for (int n = 0; n < 400; ++n)
                {
                    int p = f * 160 + n;
                    window[n] = samples[p] * 32768.0;
                    mean += window[n];
                }
                mean /= 400;
                Array.Clear(real, 0, real.Length);
                Array.Clear(imaginary, 0, imaginary.Length);
                for (int n = 0; n < 400; ++n)
                    real[n] = ((window[n] - mean) - .97 * (window[Math.Max(0, n - 1)] - mean)) * Window[n];
                MNNSpeechDsp.Fft(real, imaginary, false);
                for (int m = 0; m < 80; ++m)
                {
                    var filter = Filters[m];
                    double sum = 0;
                    for (int j = 0; j < filter.Weights.Length; ++j)
                    {
                        int k = filter.Start + j;
                        sum += (real[k] * real[k] + imaginary[k] * imaginary[k]) * filter.Weights[j];
                    }
                    output[f * 80 + m] = (float)Math.Log(Math.Max(1.1920928955078125e-7, sum));
                }
            }
            var stacked = new float[checked(frames * 560)];
            for (int f = 0; f < frames; ++f)
            {
                if ((f & 31) == 0) token.ThrowIfCancellationRequested();
                for (int k = 0; k < 560; ++k)
                    stacked[f * 560 + k] = (output[f * 6 * 80 + k] + NegativeMean[k % 80]) * InverseDeviation[k % 80];
            }
            return stacked;
        }
    }
}
