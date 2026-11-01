using System;
using System.IO;
using System.Security.Cryptography;

namespace MNN.Unity
{
    internal static class MNNConvTasNetModel
    {
        internal const string Graph = "convtasnet_fp32.mnn";
        internal static bool IsComplete(string directory)
        {
            try
            {
                var config = MNNConvTasNetConfig.Read(directory);
                if (config.repository != "JorisCos/ConvTasNet_Libri2Mix_sepclean_8k" ||
                    config.revision != "fe2524d2ab745ab0f235804d57155a7e7cfe10ae" || config.asteroid_version != "0.7.0" || config.filterbanks_version != "0.4.0" ||
                    config.graph != Graph || config.sample_rate != 8000 || config.speakers != 2 ||
                    config.input != "mixture" || config.output != "separated" || config.normalization != "none; original forward_wav" ||
                    config.minimum_samples != 16 || config.maximum_samples != 80000) return false;
                using (var stream = File.OpenRead(Path.Combine(directory,Graph)))
                using (var sha = SHA256.Create())
                    return stream.Length == 20165352 && BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant() ==
                        "9df9378d0f46b8f2fcc9255391db66c64255b6190619da59ab456194131aec49";
            }
            catch (Exception error) when (error is IOException || error is ArgumentException || error is UnauthorizedAccessException)
            {
                return false;
            }
        }
    }
}
