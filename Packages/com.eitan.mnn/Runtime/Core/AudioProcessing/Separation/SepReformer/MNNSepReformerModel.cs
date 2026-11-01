using System;
using System.IO;
using System.Security.Cryptography;

namespace MNN.Unity
{
    internal static class MNNSepReformerModel
    {
        internal const string Graph = "sepreformer_fp32.mnn";
        internal const string Normalization = "none; original network output before audition file peak scaling";

        internal static bool IsComplete(string directory)
        {
            try
            {
                var config = MNNSepReformerConfig.Read(directory);
                if (config.repository != "dmlguq456/SepReformer" ||
                    config.revision != "6dd3d163cc7c1f3a5534f22daf68382a8ea135e9" ||
                    config.weights != "models/SepReformer_Base_WSJ0/log/scratch_weights/epoch.0180.pth" ||
                    config.graph != Graph || config.input != "mixture" || config.output != "separated" ||
                    config.normalization != Normalization || config.sample_rate != 8000 || config.speakers != 2 ||
                    config.padding_multiple != 4 || config.minimum_samples != 16 || config.maximum_samples != 32000)
                    return false;
                using (var stream = File.OpenRead(Path.Combine(directory, Graph)))
                using (var sha = SHA256.Create())
                    return stream.Length == 62426244 && BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant() ==
                        "e491ff9530c406bcbcda97fc0ae0332604357243460228079f4fd7ec1047eaa5";
            }
            catch (Exception error) when (error is IOException || error is ArgumentException || error is UnauthorizedAccessException)
            {
                return false;
            }
        }
    }
}
