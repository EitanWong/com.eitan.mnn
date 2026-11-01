using System;
using System.IO;
using System.Security.Cryptography;

namespace MNN.Unity
{
    internal static class MNNFrcrnSeModel
    {
        internal const string Repository = "manyeyes/frcrn-se-16k-onnx";
        internal const string GraphFp32 = "frcrn_se_16k_fp32.mnn";
        internal const long BytesFp32 = 103978692;
        internal const string HashFp32 = "6b719a8d9be75808f66f836bff5a80052b5510be9c61d3046acd56c4ec23ef19";
        internal static bool IsRepository(string value) => string.Equals(value, Repository, StringComparison.OrdinalIgnoreCase);
        internal static bool IsComplete(string directory)
        {
            try
            {
                var config = MNNFrcrnSeConfig.Read(directory);
                return IsRepository(config.repository) && config.graph_fp32 == GraphFp32 && VerifyGraph(directory, GraphFp32, BytesFp32, HashFp32);
            }
            catch (Exception error) when (error is IOException || error is ArgumentException || error is UnauthorizedAccessException)
            {
                return false;
            }
        }
        internal static void Verify(string directory)
        {
            if (!IsComplete(directory))
                throw new InvalidDataException("FRCRN graph or profile failed verification.");
        }
        internal static string PreferredGraph(string directory, MNNBackendType backend) =>
            GraphFp32;
        private static bool VerifyGraph(string directory, string name, long bytes, string hash)
        {
            string path = Path.Combine(directory, name);
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return stream.Length == bytes && string.Equals(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", ""), hash, StringComparison.OrdinalIgnoreCase);
        }
    }
}
