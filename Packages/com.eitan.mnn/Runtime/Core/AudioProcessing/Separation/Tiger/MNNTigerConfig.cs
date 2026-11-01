using System;
using System.IO;
using UnityEngine;

namespace MNN.Unity
{
    [Serializable]
    public sealed class MNNTigerConfig
    {
        public const string FileName = "mnn_tiger.json";
        public string repository, revision, source_revision, graph, input, output, coefficient_sha256;
        public int sample_rate, speakers, fft_size, hop_size, minimum_samples, maximum_samples;
        public float[] analysis_window;
        public bool causal;

        public static MNNTigerConfig Read(string directory) =>
            JsonUtility.FromJson<MNNTigerConfig>(File.ReadAllText(Path.Combine(directory, FileName)))
            ?? throw new InvalidDataException("Invalid TIGER profile.");
    }
}
