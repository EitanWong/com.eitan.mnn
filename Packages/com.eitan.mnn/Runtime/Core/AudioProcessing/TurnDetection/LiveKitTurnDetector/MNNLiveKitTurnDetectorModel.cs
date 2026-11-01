using System;
using System.IO;
using System.Security.Cryptography;

namespace MNN.Unity
{
    internal static class MNNLiveKitTurnDetectorModel
    {
        internal const string Repository = "livekit/turn-detector";
        internal const string Graph = "turn_detector_fp32.mnn";
        internal const long Bytes = 262378316;
        internal const string GraphFp16 = "turn_detector_fp16.mnn";
        internal const long BytesFp16 = 131425228;
        internal const string HashFp16 = "442bd0a111c38b4270c46e6029a389153298e4936e9ea565c99386e55f842bdb";
        internal const string Hash = "fa268d9001d014acffe709b814d31896ef4e2ea009ce6d4447a31959500b8b36";

        internal static bool IsComplete(string directory)
        {
            try
            {
                var config = MNNLiveKitTurnDetectorConfig.Read(directory);
                return string.Equals(config.repository, Repository, StringComparison.OrdinalIgnoreCase) && File.Exists(Path.Combine(directory, "vocab.json")) &&
                    File.Exists(Path.Combine(directory, "merges.txt")) && File.Exists(Path.Combine(directory, "added_tokens.json")) &&
                    config.graph_cpu == Graph && config.graph_accelerator == GraphFp16 &&
                    VerifyGraph(directory, Graph, Bytes, Hash) && VerifyGraph(directory, GraphFp16, BytesFp16, HashFp16);
            }
            catch (Exception error) when (error is IOException || error is ArgumentException || error is UnauthorizedAccessException)
            {
                return false;
            }
        }

        internal static void Verify(string directory)
        {
            if (!IsComplete(directory))
                throw new InvalidDataException("LiveKit turn detector package failed verification.");
        }

        internal static string PreferredGraph(MNNBackendType backend) =>
            backend == MNNBackendType.CPU || backend == MNNBackendType.Auto ? Graph : GraphFp16;

        private static bool VerifyGraph(string directory, string graph, long bytes, string hash)
        {
            string path = Path.Combine(directory, graph);
            try
            {
                using (var stream = File.OpenRead(path))
                using (var sha = SHA256.Create())
                {
                    bool valid = stream.Length == bytes && string.Equals(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", ""), hash, StringComparison.OrdinalIgnoreCase);
                    return valid;
                }
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                return false;
            }
        }
    }
}
