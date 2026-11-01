using System;
using System.IO;
using UnityEngine;

namespace MNN.Unity
{
    [Serializable]
    public sealed class MNNDccrnetConfig
    {
        public const string FileName = "mnn_dccrnet.json";
        public string encoder_graph, recurrent_graph, decoder_graph;
        public string repository, revision, asteroid_version, checkpoint_asteroid_version, filterbanks_version, graph, input, output, normalization;
        public int sample_rate, speakers, minimum_samples, maximum_samples;
        public static MNNDccrnetConfig Read(string directory) =>
            JsonUtility.FromJson<MNNDccrnetConfig>(File.ReadAllText(Path.Combine(directory, FileName)))
            ?? throw new InvalidDataException("Invalid DCCRNet profile.");
    }
}
