using System;
using System.IO;
using System.Security.Cryptography;

namespace MNN.Unity
{
    internal static class MNNDptnetModel
    {
        internal const string Graph = "dptnet_native_fp32.mnn";
        internal static bool IsComplete(string directory)
        {
            try
            {
                var config = MNNDptnetConfig.Read(directory);
                if (config.repository != "JorisCos/DPTNet_Libri1Mix_enhsingle_16k" ||
                    config.revision != "935f441c53e44d40ca0cf138f71e850defc8bea5" || config.asteroid_version != "0.7.0" ||
                    config.checkpoint_asteroid_version != "0.4.1rc0" || config.filterbanks_version != "0.4.0" ||
                    config.graph != Graph || config.sample_rate != 16000 || config.speakers != 1 ||
                    config.input != "mixture" || config.output != "enhanced" || config.normalization != "none; original forward_wav" ||
                    config.minimum_samples != 16 || config.maximum_samples != 48000) return false;
                using (var stream = File.OpenRead(Path.Combine(directory, Graph)))
                using (var sha = SHA256.Create())
                    return stream.Length == 11546372 && BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant() ==
                        "7634461f17a4c1d902fd4f03be83c1e8ca16431008dcfb4352e61995e54ba17a";
            }
            catch (Exception error) when (error is IOException || error is ArgumentException || error is UnauthorizedAccessException)
            {
                return false;
            }
        }
    }
}
