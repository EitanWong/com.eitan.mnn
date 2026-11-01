using System;
using System.IO;
using System.Security.Cryptography;

namespace MNN.Unity
{
    internal static class MNNSudoRmRfModel
    {
        internal const string Graph = "sudormrf_fp32.mnn";
        internal const string Normalization = "mean and unbiased std + 1e-9; restore std and mean to each speaker";

        internal static bool IsComplete(string directory)
        {
            try
            {
                var config = MNNSudoRmRfConfig.Read(directory);
                if (config.repository != "etzinis/sudo_rm_rf" ||
                    config.revision != "cd00f2e21f7ad6281360cdf24ade36f84b0fbad6" ||
                    config.weights != "10.5281/zenodo.6299852/Improved_Sudormrf_U16_Bases512_WSJ02mix.pt" ||
                    config.graph != Graph || config.input != "mixture" || config.output != "separated" ||
                    config.normalization != Normalization || config.sample_rate != 8000 || config.speakers != 2 ||
                    config.padding_multiple != 320 || config.minimum_samples != 2 || config.maximum_samples != 32000)
                    return false;
                using (var stream = File.OpenRead(Path.Combine(directory, Graph)))
                using (var sha = SHA256.Create())
                    return stream.Length == 20471356 && BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant() ==
                        "d6e087411dd52356818ebc56514d62af077e4252805ba8f321cf33a31567e7b7";
            }
            catch (Exception error) when (error is IOException || error is ArgumentException || error is UnauthorizedAccessException)
            {
                return false;
            }
        }
    }
}
