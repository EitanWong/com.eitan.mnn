using System;
using System.IO;
using UnityEngine;

namespace MNN.Unity
{
    [Serializable]
    public sealed class MNNDcunetConfig
    {
        public const string FileName = "mnn_dcunet.json";
        public string weights;
        public string repository, revision, asteroid_version, checkpoint_asteroid_version, filterbanks_version, graph, input, output, normalization;
        public int sample_rate, speakers, minimum_samples, maximum_samples;
        public static MNNDcunetConfig Read(string directory) =>
            JsonUtility.FromJson<MNNDcunetConfig>(File.ReadAllText(Path.Combine(directory, FileName)))
            ?? throw new InvalidDataException("Invalid DCUNet profile.");
    }
}
