using System;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

namespace MNN.Unity
{
    internal static class MNNSileroModel
    {
        internal const string Repository = "Jeryuk/silero_vad-MNN";
        internal const string Revision = "e2502b184542017860ab1ca83c18f8742b221ecd";
        internal const string Graph = "silero_vad.mnn";
        internal const string GraphFp16 = "silero_vad_fp16.mnn";
        internal const long Bytes = 1486656;
        internal const string Hash = "20008d132c61be560704c6d81459b688ffce7244b46a0afdaa0247e2d09d2727";
        internal const long BytesFp16 = 1207880;
        internal const string HashFp16 = "3734d84018aef8b8eb9cb75ffec3f653afebf517798dbeefade22c4437aaf572";
        internal const string OfficialRepository = "snakers4/silero-vad";
        internal const string OfficialRevision = "84768cefdf5a3852400e9d8237f7315d14b64a08";
        internal const string OfficialGraph = "silero_v5_16k_fp32.mnn";
        private const long OfficialBytes = 1254332;
        private const string OfficialHash = "a82b806bbfb3af50646cbfc7dab94fffee5fadb3ad69ab641fb586706d225933";
        internal static bool IsOfficialProfile(MNNSileroVadConfig config) => config.repository == OfficialRepository &&
            config.source_revision == OfficialRevision && config.graph == OfficialGraph;
        internal static void VerifyOfficial(string directory, MNNSileroVadConfig config)
        {
            if (!IsOfficialProfile(config)) throw new InvalidDataException("Unknown official Silero profile.");
            VerifyGraph(directory, OfficialGraph, OfficialBytes, OfficialHash);
        }
        internal static bool IsRepository(string repository) => string.Equals(repository, Repository, StringComparison.OrdinalIgnoreCase);
        internal static string ResolveRevision(string revision)
        {
            if (string.IsNullOrWhiteSpace(revision) || revision == "main" || revision == Revision) return Revision;
            throw new ArgumentException("Silero VAD requires its verified export revision.", nameof(revision));
        }
        internal static void Verify(string directory)
        {
            VerifyGraph(directory, Graph, Bytes, Hash);
            string fp16 = Path.Combine(directory, GraphFp16);
            if (File.Exists(fp16)) VerifyGraph(directory, GraphFp16, BytesFp16, HashFp16);
        }
        internal static string PreferredGraph(string directory, MNNBackendType backend) =>
            backend != MNNBackendType.CPU && backend != MNNBackendType.Auto && File.Exists(Path.Combine(directory, GraphFp16)) ? GraphFp16 : Graph;
        internal static void Prepare(string directory)
        {
            if (File.Exists(Path.Combine(directory, MNNSileroVadConfig.FileName)))
            {
                var config = MNNSileroVadConfig.Read(directory);
                if (IsOfficialProfile(config)) { VerifyOfficial(directory, config); return; }
            }
            Verify(directory);
            File.WriteAllText(Path.Combine(directory, MNNSileroVadConfig.FileName), JsonUtility.ToJson(new MNNSileroVadConfig(), true));
        }
        internal static bool IsComplete(string directory)
        {
            try
            {
                var config = MNNSileroVadConfig.Read(directory);
                if (IsOfficialProfile(config)) { VerifyOfficial(directory, config); return true; }
                return IsRepository(config.repository) && new FileInfo(Path.Combine(directory, Graph)).Length == Bytes;
            }
            catch (Exception error) when (error is IOException || error is ArgumentException || error is UnauthorizedAccessException) { return false; }
        }
        private static void VerifyGraph(string directory, string graph, long bytes, string hash)
        {
            using (var stream = File.OpenRead(Path.Combine(directory, graph)))
            using (var sha = SHA256.Create())
                if (stream.Length != bytes || !string.Equals(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", ""), hash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Silero VAD graph failed verification: " + graph);
        }
    }
}
