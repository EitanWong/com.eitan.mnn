using System;
using System.IO;
using UnityEngine;

namespace MNN.Unity
{
    [Serializable]
    public sealed class MNNSpExPlusConfig
    {
        public const string FileName = "mnn_spexplus.json";
        public string repository, revision, source_revision, reference_graph, audio_graph;
        public string reference_input, reference_output, audio_input, audio_output, normalization;
        public int sample_rate, embedding_size, minimum_samples, maximum_samples;
        public int minimum_reference_samples, maximum_reference_samples;
        public bool causal;

        public static MNNSpExPlusConfig Read(string directory) =>
            JsonUtility.FromJson<MNNSpExPlusConfig>(File.ReadAllText(Path.Combine(directory, FileName)))
            ?? throw new InvalidDataException("Invalid SpEx+ profile.");
    }
}
