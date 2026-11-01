using System;
using System.IO;
using UnityEngine;

namespace MNN.Unity
{
    [Serializable]
    public sealed class MNNDptnetConfig
    {
        public const string FileName = "mnn_dptnet.json";
        public string repository, revision, asteroid_version, checkpoint_asteroid_version, filterbanks_version, graph, input, output, normalization;
        public int sample_rate, speakers, minimum_samples, maximum_samples;
        public static MNNDptnetConfig Read(string directory) =>
            JsonUtility.FromJson<MNNDptnetConfig>(File.ReadAllText(Path.Combine(directory, FileName)))
            ?? throw new InvalidDataException("Invalid DPTNet profile.");
    }
}
