using System;
using System.IO;
using System.Security.Cryptography;

namespace MNN.Unity
{
    internal static class MNNDprnnModel
    {
        internal const string Graph = "dprnn_native_fp32.mnn";
        internal static bool IsComplete(string directory)
        {
            try
            {
                var config = MNNDprnnConfig.Read(directory);
                if (config.repository != "mpariente/DPRNNTasNet-ks2_WHAM_sepclean" ||
                    config.revision != "548f4f1c9476ed0af6b4bea3403247c71d5d7d46" || config.asteroid_version != "0.7.0" || config.filterbanks_version != "0.4.0" ||
                    config.graph != Graph || config.sample_rate != 8000 || config.speakers != 2 ||
                    config.input != "mixture" || config.output != "separated" || config.normalization != "none; original forward_wav" ||
                    config.minimum_samples != 2 || config.maximum_samples != 32000) return false;
                using (var stream = File.OpenRead(Path.Combine(directory,Graph)))
                using (var sha = SHA256.Create())
                    return stream.Length == 14779152 && BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant() ==
                        "b57a18438ea74a86151ed0ae3138b20595f0f24b65dc6a19d06732a0c6e35bdc";
            }
            catch (Exception error) when (error is IOException || error is ArgumentException || error is UnauthorizedAccessException)
            {
                return false;
            }
        }
    }
}
