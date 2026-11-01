using System;
using System.IO;
using UnityEngine;

namespace MNN.Unity
{
    [Serializable]
    public sealed class MNNSudoRmRfConfig
    {
        public const string FileName = "mnn_sudormrf.json";
        public string repository, revision, weights, graph, input, output, normalization;
        public int sample_rate, speakers, padding_multiple, minimum_samples, maximum_samples;

        public static MNNSudoRmRfConfig Read(string directory) =>
            JsonUtility.FromJson<MNNSudoRmRfConfig>(File.ReadAllText(Path.Combine(directory, FileName)))
            ?? throw new InvalidDataException("Invalid SudoRMRF profile.");
    }
}
