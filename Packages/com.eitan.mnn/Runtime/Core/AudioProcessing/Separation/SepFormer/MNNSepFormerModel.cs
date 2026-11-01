using System;
using System.IO;
using System.Security.Cryptography;

namespace MNN.Unity
{
    internal static class MNNSepFormerModel
    {
        internal const string Graph = "sepformer_fp32.mnn";
        internal static bool IsComplete(string directory)
        {
            try
            {
                var config = MNNSepFormerConfig.Read(directory);
                if (config.repository != "speechbrain/sepformer-wsj02mix" ||
                    config.revision != "3a2826343a10e2d2e8a75f79aeab5ff3a2473531" || config.speechbrain_version != "1.0.3" ||
                    config.graph != Graph || config.sample_rate != 8000 || config.speakers != 2 ||
                    config.input != "mixture" || config.output != "separated" || config.normalization != "none" ||
                    config.minimum_samples != 16 || config.maximum_samples != 80000) return false;
                using (var stream = File.OpenRead(Path.Combine(directory,Graph)))
                using (var sha = SHA256.Create())
                    return stream.Length == 106243148 && BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant() ==
                        "cda45bf11cb268885e39ff00cf31bf5eed552d49741f580e122a37dd4538834b";
            }
            catch (Exception error) when (error is IOException || error is ArgumentException || error is UnauthorizedAccessException)
            {
                return false;
            }
        }
    }
}
