using System;
using System.IO;
using UnityEngine;

namespace MNN.Unity
{
    /// <summary>Identifies the pinned Paraformer Chinese online 8404-token graph protocol.</summary>
    [Serializable]
    public sealed class MNNParaformerConfig
    {
        public const string FileName = "mnn_paraformer.json";
        public string repository = MNNParaformerModel.Repository;
        public static MNNParaformerConfig Read(string directory) =>
            JsonUtility.FromJson<MNNParaformerConfig>(File.ReadAllText(Path.Combine(directory, FileName)))
            ?? throw new InvalidDataException("Invalid Paraformer profile.");
    }
}
