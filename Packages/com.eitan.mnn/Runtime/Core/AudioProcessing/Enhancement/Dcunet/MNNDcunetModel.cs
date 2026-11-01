using System;
using System.IO;
using System.Security.Cryptography;

namespace MNN.Unity
{
    internal static class MNNDcunetModel
    {
        internal const string Graph = "dcunet_fp32.mnn";
        internal const string Weights = Graph + ".weight";

        internal static bool IsComplete(string directory)
        {
            try
            {
                var config = MNNDcunetConfig.Read(directory);
                if (config.repository != "JorisCos/DCUNet_Libri1Mix_enhsingle_16k" ||
                    config.revision != "3fa701427576e01e835ae415c8ed7516874b08dd" ||
                    config.asteroid_version != "0.7.0" || config.checkpoint_asteroid_version != "0.4.1rc0" ||
                    config.filterbanks_version != "0.4.0" || config.graph != Graph || config.weights != Weights ||
                    config.sample_rate != 16000 || config.speakers != 1 || config.input != "mixture" || config.output != "enhanced" ||
                    config.normalization != "none; original forward_wav" || config.minimum_samples != 1024 || config.maximum_samples != 48000)
                    return false;
                return Verify(directory, Graph, 62736, "159dd2d8b8725e61de06ba155b3aed142958995855984f2262413c59efa360a1") &&
                    Verify(directory, Weights, 39071580, "b791ccc0185f936ae385be1d652a6cbbc0fa531639d513e23eef48169db28140");
            }
            catch (Exception error) when (error is IOException || error is ArgumentException || error is UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static bool Verify(string directory, string filename, long bytes, string checksum)
        {
            using (var stream = File.OpenRead(Path.Combine(directory, filename)))
            using (var sha = SHA256.Create())
                return stream.Length == bytes && BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant() == checksum;
        }
    }
}
