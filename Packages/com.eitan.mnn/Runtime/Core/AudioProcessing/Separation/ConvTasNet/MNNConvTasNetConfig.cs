using System;
using System.IO;
using UnityEngine;

namespace MNN.Unity
{
    [Serializable]
    public sealed class MNNConvTasNetConfig
    {
        public const string FileName = "mnn_convtasnet.json";
        public string repository, revision, asteroid_version, filterbanks_version, graph, input, output, normalization;
        public int sample_rate, speakers, minimum_samples, maximum_samples;
        public static MNNConvTasNetConfig Read(string directory) =>
            JsonUtility.FromJson<MNNConvTasNetConfig>(File.ReadAllText(Path.Combine(directory,FileName)))
            ?? throw new InvalidDataException("Invalid ConvTasNet profile.");
    }
}
