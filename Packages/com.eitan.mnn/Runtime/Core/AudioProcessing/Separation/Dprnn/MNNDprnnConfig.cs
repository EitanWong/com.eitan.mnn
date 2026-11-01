using System;
using System.IO;
using UnityEngine;

namespace MNN.Unity
{
    [Serializable]
    public sealed class MNNDprnnConfig
    {
        public const string FileName = "mnn_dprnn.json";
        public string repository, revision, asteroid_version, filterbanks_version, graph, input, output, normalization;
        public int sample_rate, speakers, minimum_samples, maximum_samples;
        public static MNNDprnnConfig Read(string directory) =>
            JsonUtility.FromJson<MNNDprnnConfig>(File.ReadAllText(Path.Combine(directory,FileName)))
            ?? throw new InvalidDataException("Invalid Dprnn profile.");
    }
}
