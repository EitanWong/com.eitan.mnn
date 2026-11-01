using System;
using System.IO;
using UnityEngine;

namespace MNN.Unity
{
    [Serializable]
    public sealed class MNNMossFormer2Config
    {
        public const string FileName = "mnn_mossformer2_ss.json";
        public string repository = MNNMossFormer2Model.Repository;
        public int sample_rate = 16000;
        public int speakers = 2;
        public string input = "inputs";
        public string[] outputs = {"spk0", "spk1"};
        public string graph = MNNMossFormer2Model.Graph;
        public int conversion_optimize_level = 1;

        public static MNNMossFormer2Config Read(string directory) =>
            JsonUtility.FromJson<MNNMossFormer2Config>(File.ReadAllText(Path.Combine(directory, FileName)))
            ?? throw new InvalidDataException("Invalid MossFormer2 separation profile.");
    }
}
