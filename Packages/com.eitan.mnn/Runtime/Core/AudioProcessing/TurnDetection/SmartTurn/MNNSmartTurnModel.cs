using System;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

namespace MNN.Unity
{
    internal static class MNNSmartTurnModel
    {
        internal const string Repository = "Jeryuk/smart-turn-v3.2-gpu-MNN";
        internal const string Revision = "1cc21f7b6f904eb1a5daadc27c6f4931d1f78794";
        internal const string Graph = "smart-turn-v3.2-gpu.mnn";
        internal const string GraphFp16 = "smart-turn-v3.2-gpu-fp16.mnn";
        internal const long Bytes = 9061752;
        internal const string Hash = "b55ea474ea173020d0d27d4d0995607ce306224851f046c6878dc01f5fdef0d6";
        internal const long BytesFp16 = 16103840;
        internal const string HashFp16 = "24bd2816e4a3880c9b7b80e0e0e76a829d0a73d1cba199376ee8245b15ad994c";
        internal const string OfficialRepository = "pipecat-ai/smart-turn-v3";
        internal const string OfficialRevision = "f766f81d3cfdf7737ac64aad813d91bbfd56bf93";
        internal const string OfficialGraph = "smart_turn_v32_fp32.mnn";
        private const long OfficialBytes = 32061112;
        private const string OfficialHash = "defa0e643e16dc7226fa5f80b5d07d813769df323423387ec3cfd01f78aa91cc";
        internal static bool IsOfficialProfile(MNNSmartTurnConfig config) => config.repository == OfficialRepository &&
            config.source_revision == OfficialRevision && config.graph == OfficialGraph;
        internal static bool IsRepository(string repository) => string.Equals(repository, Repository, StringComparison.OrdinalIgnoreCase);
        internal static string ResolveRevision(string revision)
        {
            if (string.IsNullOrWhiteSpace(revision) || revision == "main" || revision == Revision) return Revision;
            throw new ArgumentException("Smart Turn requires its verified export revision.", nameof(revision));
        }
        internal static void Verify(string directory)
        {
            if (IsOfficialProfile(MNNSmartTurnConfig.Read(directory)))
            {
                VerifyGraph(directory, OfficialGraph, OfficialBytes, OfficialHash);
                return;
            }
            VerifyGraph(directory, Graph, Bytes, Hash);
            string fp16 = Path.Combine(directory, GraphFp16);
            if (File.Exists(fp16)) VerifyGraph(directory, GraphFp16, BytesFp16, HashFp16);
        }
        internal static string PreferredGraph(string directory, MNNBackendType backend)
        {
            if (IsOfficialProfile(MNNSmartTurnConfig.Read(directory))) return OfficialGraph;
            return backend != MNNBackendType.CPU && backend != MNNBackendType.Auto && File.Exists(Path.Combine(directory, GraphFp16)) ? GraphFp16 : Graph;
        }
        internal static void Prepare(string directory)
        {
            if (File.Exists(Path.Combine(directory, MNNSmartTurnConfig.FileName)) &&
                IsOfficialProfile(MNNSmartTurnConfig.Read(directory))) { Verify(directory); return; }
            VerifyGraph(directory, Graph, Bytes, Hash);
            string fp16 = Path.Combine(directory, GraphFp16);
            if (File.Exists(fp16)) VerifyGraph(directory, GraphFp16, BytesFp16, HashFp16);
            File.WriteAllText(Path.Combine(directory, MNNSmartTurnConfig.FileName), JsonUtility.ToJson(new MNNSmartTurnConfig(), true));
        }
        internal static bool IsComplete(string directory)
        {
            try
            {
                if (IsOfficialProfile(MNNSmartTurnConfig.Read(directory))) { Verify(directory); return true; }
                return IsRepository(MNNSmartTurnConfig.Read(directory).repository) && new FileInfo(Path.Combine(directory, Graph)).Length == Bytes;
            }
            catch (Exception error) when (error is IOException || error is ArgumentException || error is UnauthorizedAccessException) { return false; }
        }
        private static void VerifyGraph(string directory, string graph, long bytes, string hash)
        {
            using (var stream = File.OpenRead(Path.Combine(directory, graph)))
            using (var sha = SHA256.Create())
                if (stream.Length != bytes || !string.Equals(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", ""), hash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Smart Turn graph failed verification: " + graph);
        }
    }
}
