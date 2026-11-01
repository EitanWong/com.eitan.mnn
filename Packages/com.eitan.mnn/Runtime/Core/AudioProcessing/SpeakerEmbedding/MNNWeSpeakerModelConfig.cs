using System;
using System.IO;
using UnityEngine;

namespace MNN.Unity
{
    [Serializable]
    public sealed class MNNWeSpeakerModelConfig
    {
        public const string FileName = "mnn_wespeaker.json";
        public string repository = MNNWeSpeakerModel.Repository;
        public string graph = MNNWeSpeakerModel.Graph;
        public string graph_fp16 = "";
        public string input = "feats";
        public string output = "embs";
        public int embedding_dimension = 256;
        public static MNNWeSpeakerModelConfig Read(string directory) =>
            JsonUtility.FromJson<MNNWeSpeakerModelConfig>(File.ReadAllText(Path.Combine(directory, FileName)))
            ?? throw new InvalidDataException("Invalid WeSpeaker profile.");
    }
}
