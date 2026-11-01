using System;
using System.IO;
using UnityEngine;

namespace MNN.Unity
{
    /// <summary>Identifies the verified Qwen3-ASR 0.6B INT8 asset set.</summary>
    [Serializable]
    public sealed class MNNQwen3AsrConfig
    {
        public const string FileName = "mnn_qwen3_asr.json";
        public string repository = MNNQwen3AsrModel.Repository;
        public static MNNQwen3AsrConfig Read(string directory) =>
            JsonUtility.FromJson<MNNQwen3AsrConfig>(File.ReadAllText(Path.Combine(directory, FileName)))
            ?? throw new InvalidDataException("Invalid Qwen3-ASR profile.");
    }
}
