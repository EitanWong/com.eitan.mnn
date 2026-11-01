using System;
using System.IO;
using UnityEngine;

namespace MNN.Unity
{
    [Serializable]
    public sealed class MNNSortformerConfig
    {
        public const string FileName = "mnn_sortformer.json";
        public string repository, revision, nemo_version, checkpoint_nemo_version, preencode_graph, core_graph;
        public int sample_rate, speakers, mel_bins, chunk_frames, cache_frames, maximum_samples;
        public float[] analysis_window, mel_filters;

        public static MNNSortformerConfig Read(string directory) =>
            JsonUtility.FromJson<MNNSortformerConfig>(File.ReadAllText(Path.Combine(directory, FileName)))
            ?? throw new InvalidDataException("Invalid Sortformer profile.");
    }
}
