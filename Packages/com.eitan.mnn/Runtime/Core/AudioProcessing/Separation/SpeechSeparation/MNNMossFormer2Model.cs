using System;
using System.IO;
using System.Security.Cryptography;

namespace MNN.Unity
{
    internal static class MNNMossFormer2Model
    {
        internal const string Repository = "alibabasglab/MossFormer2_SS_16K";
        internal const string Graph = "mossformer2_ss_16k_fp32.mnn";
        internal const long Bytes = 229546208;
        internal const string Hash = "cc3e47d3ef3192f6f6b8f07e40982c6a3e161f21654cfe022d359f0a018061af";

        internal static bool IsComplete(string directory)
        {
            try
            {
                var config = MNNMossFormer2Config.Read(directory);
                return string.Equals(config.repository, Repository, StringComparison.OrdinalIgnoreCase) &&
                    config.sample_rate == 16000 && config.speakers == 2 && config.graph == Graph && VerifyGraph(directory, false);
            }
            catch (Exception error) when (error is IOException || error is ArgumentException || error is UnauthorizedAccessException)
            {
                return false;
            }
        }

        internal static void Verify(string directory)
        {
            if (!IsComplete(directory))
                throw new InvalidDataException("MossFormer2 separation graph failed verification.");
        }

        private static bool VerifyGraph(string directory, bool throwOnFailure)
        {
            string path = Path.Combine(directory, Graph);
            try
            {
                using (var stream = File.OpenRead(path))
                using (var sha = SHA256.Create())
                {
                    bool valid = stream.Length == Bytes && string.Equals(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", ""), Hash, StringComparison.OrdinalIgnoreCase);
                    if (!valid && throwOnFailure)
                        throw new InvalidDataException("MossFormer2 graph hash or size mismatch.");
                    return valid;
                }
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                if (throwOnFailure)
                    throw;
                return false;
            }
        }
    }
}
