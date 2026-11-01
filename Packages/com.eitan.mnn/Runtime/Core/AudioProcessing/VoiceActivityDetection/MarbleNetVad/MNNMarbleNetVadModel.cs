using System;
using System.IO;
using System.Security.Cryptography;

namespace MNN.Unity
{
    internal static class MNNMarbleNetVadModel
    {
        internal const string Repository = "TigreGotico/frame-vad-marblenet-onnx";
        internal const string Graph = "marblenet_fp32.mnn";
        internal const long Bytes = 372552;
        internal const string Hash = "599ca44f912d82198fcf85c61c1319fdb252ab7d814836eed7d74b48d29c6134";

        internal static bool IsRepository(string value) => string.Equals(value, Repository, StringComparison.OrdinalIgnoreCase);

        internal static bool IsComplete(string directory)
        {
            try
            {
                var config = MNNMarbleNetVadConfig.Read(directory);
                return IsRepository(config.repository) && config.graph == Graph && VerifyGraph(directory, false);
            }
            catch (Exception error) when (error is IOException || error is ArgumentException || error is UnauthorizedAccessException)
            {
                return false;
            }
        }

        internal static void Verify(string directory)
        {
            var config = MNNMarbleNetVadConfig.Read(directory);
            if (!IsRepository(config.repository) || config.graph != Graph || !VerifyGraph(directory, true))
                throw new InvalidDataException("MarbleNet VAD graph or profile failed verification.");
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
                        throw new InvalidDataException("MarbleNet VAD graph hash or size mismatch.");
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
