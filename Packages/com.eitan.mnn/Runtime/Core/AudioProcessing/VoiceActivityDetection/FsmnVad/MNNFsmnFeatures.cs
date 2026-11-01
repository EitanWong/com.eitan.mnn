using System;
using System.IO;
using System.Threading;

namespace MNN.Unity
{
    internal static class MNNFsmnFeatures
    {
        private const int Bins = 80;

        internal static float[] ExtractLfr(float[] samples, CancellationToken token, int lfrM = 5, int lfrN = 1)
        {
            if (samples == null || samples.Length == 0) throw new ArgumentException("Expected nonempty 16 kHz mono PCM.", nameof(samples));
            if (lfrM < 1 || lfrM > 15 || lfrN < 1 || lfrN > lfrM) throw new ArgumentOutOfRangeException(nameof(lfrM));
            var fbank = MNNKaldiFeatures.ExtractFsmn(samples, out int frames, token);
            int outputFrames = (frames + lfrN - 1) / lfrN;
            var result = new float[checked(outputFrames * lfrM * Bins)];
            int leftContext = (lfrM - 1) / 2;
            for (int output = 0; output < outputFrames; ++output)
            {
                if ((output & 31) == 0) token.ThrowIfCancellationRequested();
                for (int stack = 0; stack < lfrM; ++stack)
                {
                    int source = Math.Max(0, Math.Min(frames - 1, output * lfrN + stack - leftContext));
                    Array.Copy(fbank, source * Bins, result, (output * lfrM + stack) * Bins, Bins);
                }
            }
            return result;
        }

        internal static void ValidateCmvn(MNNFsmnVadConfig config)
        {
            int dimension = Bins * config.lfr_m;
            if (config.cmvn_shift == null || config.cmvn_scale == null || config.cmvn_shift.Length != dimension || config.cmvn_scale.Length != dimension)
                throw new InvalidDataException("FSMN requires the source model's 400-dimensional CMVN shift and scale.");
            for (int i = 0; i < dimension; ++i)
                if (double.IsNaN(config.cmvn_shift[i]) || double.IsInfinity(config.cmvn_shift[i]) ||
                    double.IsNaN(config.cmvn_scale[i]) || double.IsInfinity(config.cmvn_scale[i]) || config.cmvn_scale[i] <= 0)
                    throw new InvalidDataException("FSMN CMVN contains an invalid shift or scale.");
        }

        internal static void ApplyCmvn(float[] features, MNNFsmnVadConfig config)
        {
            int dimension = config.cmvn_shift.Length;
            for (int i = 0; i < features.Length; ++i)
            {
                int bin = i % dimension;
                features[i] = (float)((features[i] + config.cmvn_shift[bin]) * config.cmvn_scale[bin]);
            }
        }
    }
}
