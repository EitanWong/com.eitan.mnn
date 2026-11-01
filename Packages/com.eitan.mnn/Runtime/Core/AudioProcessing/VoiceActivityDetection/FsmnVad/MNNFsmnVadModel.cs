using System;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

namespace MNN.Unity
{
    internal static class MNNFsmnVadModel
    {
        internal const string Repository = "iic/speech_fsmn_vad_zh-cn-16k-common-pytorch";
        internal const string Graph = "fsmn_vad_fp32.mnn";
        internal const long Bytes = 1750900;
        internal const string Hash = "ce3bfdc3e7decf5ad5a85dc813cebc5d70554f9e5528cb748e705c9ca2cc7665";

        internal static bool IsRepository(string value) => string.Equals(value, Repository, StringComparison.OrdinalIgnoreCase);
        internal static bool IsComplete(string directory)
        {
            try
            {
                var config = MNNFsmnVadConfig.Read(directory);
                MNNFsmnFeatures.ValidateCmvn(config);
                return IsRepository(config.repository) && config.graph == Graph && VerifyGraph(directory, false);
            }
            catch (Exception error) when (error is IOException || error is ArgumentException || error is UnauthorizedAccessException)
            {
                return false;
            }
        }
        internal static void Verify(string directory)
        {
            if (!VerifyGraph(directory, true))
                throw new InvalidDataException("FSMN VAD graph failed verification.");
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
                    if (!valid && throwOnFailure) throw new InvalidDataException("FSMN VAD graph hash or size mismatch.");
                    return valid;
                }
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                if (throwOnFailure) throw;
                return false;
            }
        }
    }
}
