using System;
using System.IO;
using UnityEngine;

namespace MNN.Unity
{
    [Serializable]
    public sealed class MNNSmartTurnConfig
    {
        public const string FileName = "mnn_smart_turn.json";
        public string repository = MNNSmartTurnModel.Repository;
        public string graph = "";
        public string source_revision = "";
        public static MNNSmartTurnConfig Read(string directory) =>
            JsonUtility.FromJson<MNNSmartTurnConfig>(File.ReadAllText(Path.Combine(directory, FileName)))
            ?? throw new InvalidDataException("Invalid Smart Turn profile.");
    }
}
