using System;
using System.IO;
using UnityEngine;

namespace MNN.Unity
{
    [Serializable]
    public sealed class MNNSileroVadConfig
    {
        public const string FileName = "mnn_silero_vad.json";
        public string repository = MNNSileroModel.Repository;
        public string graph;
        public string source_revision;
        public static MNNSileroVadConfig Read(string directory) =>
            JsonUtility.FromJson<MNNSileroVadConfig>(File.ReadAllText(Path.Combine(directory, FileName)))
            ?? throw new InvalidDataException("Invalid Silero VAD profile.");
    }
}
