using System;
using System.IO;
using UnityEngine;

namespace MNN.Unity
{
    [Serializable]
    public sealed class MNNSepReformerConfig
    {
        public const string FileName = "mnn_sepreformer.json";
        public string repository, revision, weights, graph, input, output, normalization;
        public int sample_rate, speakers, padding_multiple, minimum_samples, maximum_samples;

        public static MNNSepReformerConfig Read(string directory) =>
            JsonUtility.FromJson<MNNSepReformerConfig>(File.ReadAllText(Path.Combine(directory, FileName)))
            ?? throw new InvalidDataException("Invalid SepReformer profile.");
    }
}
