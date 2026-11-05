using System;

namespace MNN.Unity
{
    /// <summary>Recovers relative source energy from a mixture despite independent output gains.</summary>
    public static class MNNAudioMixtureContribution
    {
        // No allocation. False means insufficient evidence; both output fractions are zero.
        // These are evidence gates, not calibrated confidence probabilities.
        private const double MaximumSquaredCorrelation = .95 * .95, MinimumExplainedEnergy = .6;

        /// <summary>Measures two source energy fractions without allocating. Inputs use the same sample clock.
        /// Returns false and zero fractions for silence, correlated candidates or insufficient explained energy.
        /// These fractions are evidence for source selection, not calibrated speech probabilities.</summary>
        public static bool TryEstimate(float[] mixture, float[] first, float[] second,
            out float firstContribution, out float secondContribution)
        {
            firstContribution = secondContribution = 0;
            if (mixture == null || first == null || second == null ||
                mixture.Length != first.Length || mixture.Length != second.Length)
                throw new ArgumentException("Mixture and separated sources must have the same sample clock and length.");
            if (mixture.Length < 4) return false;
            double meanMix = Mean(mixture), meanFirst = Mean(first), meanSecond = Mean(second);
            double mixtureEnergy = 0, firstEnergy = 0, secondEnergy = 0, cross = 0, firstMix = 0, secondMix = 0;
            for (int i = 0; i < mixture.Length; ++i)
            {
                double x = mixture[i] - meanMix, a = first[i] - meanFirst, b = second[i] - meanSecond;
                mixtureEnergy += x * x; firstEnergy += a * a; secondEnergy += b * b;
                cross += a * b; firstMix += a * x; secondMix += b * x;
            }
            if (mixtureEnergy <= 1e-12) return false;
            // A separator may return one source plus silence/DC. Use the same
            // explained-energy contract; it does not require another speech detector.
            if (firstEnergy <= 1e-12 || secondEnergy <= 1e-12)
            {
                bool firstPresent = firstEnergy > 1e-12;
                double energy = firstPresent ? firstEnergy : secondEnergy;
                double covariance = firstPresent ? firstMix : secondMix;
                if (energy <= 1e-12 || covariance * covariance / energy / mixtureEnergy < MinimumExplainedEnergy)
                    return false;
                firstContribution = firstPresent ? 1 : 0;
                secondContribution = firstPresent ? 0 : 1;
                return true;
            }
            double product = firstEnergy * secondEnergy;
            if (cross * cross >= MaximumSquaredCorrelation * product)
                return false;
            double determinant = product - cross * cross;
            // Joint least squares accounts for correlation between the two candidate waveforms.
            double gainFirst = (firstMix * secondEnergy - secondMix * cross) / determinant;
            double gainSecond = (secondMix * firstEnergy - firstMix * cross) / determinant;
            double explained = gainFirst * firstMix + gainSecond * secondMix;
            if (explained / mixtureEnergy < MinimumExplainedEnergy) return false;
            double energyFirst = gainFirst * gainFirst * firstEnergy, energySecond = gainSecond * gainSecond * secondEnergy;
            double total = energyFirst + energySecond;
            if (total <= 1e-12) return false;
            firstContribution = (float)(energyFirst / total);
            secondContribution = (float)(energySecond / total);
            return true;
        }

        private static double Mean(float[] samples)
        {
            double sum = 0;
            foreach (float value in samples)
            {
                if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentException("Supply finite audio.");
                sum += value;
            }
            return sum / samples.Length;
        }
    }
}
