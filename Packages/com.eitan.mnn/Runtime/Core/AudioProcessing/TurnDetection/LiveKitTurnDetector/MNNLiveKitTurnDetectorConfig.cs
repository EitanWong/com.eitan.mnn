using System;
using System.IO;
using UnityEngine;

namespace MNN.Unity
{
    [Serializable]
    public sealed class MNNLiveKitTurnDetectorConfig
    {
        public const string FileName = "mnn_livekit_turn_detector.json";
        public string repository = MNNLiveKitTurnDetectorModel.Repository;
        public string revision = "v1.2.0";
        public string input = "input_ids";
        public string output = "prob";
        public string graph_cpu = MNNLiveKitTurnDetectorModel.Graph;
        public string graph_accelerator = MNNLiveKitTurnDetectorModel.GraphFp16;
        public int max_position_embeddings = 8192;
        public string tokenizer = "tokenizer.json";

        public static MNNLiveKitTurnDetectorConfig Read(string directory) =>
            JsonUtility.FromJson<MNNLiveKitTurnDetectorConfig>(File.ReadAllText(Path.Combine(directory, FileName)))
            ?? throw new InvalidDataException("Invalid LiveKit turn detector profile.");
    }
}
