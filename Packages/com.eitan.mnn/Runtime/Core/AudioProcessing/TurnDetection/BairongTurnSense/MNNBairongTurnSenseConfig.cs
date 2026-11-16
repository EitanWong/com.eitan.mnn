using System;
using System.IO;
using UnityEngine;

namespace MNN.Unity
{
    [Serializable]
    internal sealed class MNNBairongTurnSenseConfig
    {
        internal const string FileName = "mnn_bairong_turnsense.json";
        public string source_repository, source_revision, version, graph_sha256, cmvn_sha256;
        public string graph = "model.mnn";
        public long graph_bytes;
        internal static MNNBairongTurnSenseConfig Read(string directory)
        {
            var config = JsonUtility.FromJson<MNNBairongTurnSenseConfig>(File.ReadAllText(Path.Combine(directory, FileName)));
            if (config == null || config.source_repository != "brgroup/TurnSense" ||
                config.source_revision != "341dd68c9df87d74d35b9cee63a1dcf0fff24966" ||
                (config.version != "1.0" && config.version != "1.1") || config.graph != "model.mnn" || config.graph_bytes < 16)
                throw new InvalidDataException("Unsupported Bairong TurnSense export profile.");
            return config;
        }
        internal static void Verify(string directory)
        {
            var config = Read(directory);
            string graph = Path.Combine(directory, config.graph), cmvn = Path.Combine(directory, "am.mvn");
            if (!File.Exists(graph) || new FileInfo(graph).Length != config.graph_bytes ||
                MNNModelDeliveryManifest.ComputeSha256(graph) != config.graph_sha256 ||
                !File.Exists(cmvn) || MNNModelDeliveryManifest.ComputeSha256(cmvn) != config.cmvn_sha256)
                throw new InvalidDataException("Bairong TurnSense deployment checksum mismatch.");
        }
        internal static bool IsComplete(string directory)
        {
            try { Verify(directory); return true; }
            catch (Exception e) when (e is IOException || e is ArgumentException || e is UnauthorizedAccessException) { return false; }
        }
    }
}
