using System;
using System.IO;
using UnityEngine;

namespace MNN.Unity
{
    [Serializable]
    public sealed class MNNSepFormerConfig
    {
        public const string FileName = "mnn_sepformer.json";
        public string repository, revision, speechbrain_version, graph, input, output, normalization;
        public int sample_rate, speakers, minimum_samples, maximum_samples;
        public static MNNSepFormerConfig Read(string directory) =>
            JsonUtility.FromJson<MNNSepFormerConfig>(File.ReadAllText(Path.Combine(directory,FileName)))
            ?? throw new InvalidDataException("Invalid SepFormer profile.");
    }
}
