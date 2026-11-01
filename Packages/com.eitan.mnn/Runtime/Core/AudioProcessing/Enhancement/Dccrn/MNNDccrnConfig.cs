using System;
using System.IO;
using UnityEngine;

namespace MNN.Unity
{
    [Serializable]
    public sealed class MNNDccrnConfig
    {
        public const string FileName = "mnn_dccrn.json";
        public string repository, revision, source_revision, checkpoint_loading, normalization;
        public string graph, analysis_graph, encoder_graph, recurrent_graph, decoder_graph, external_weights;
        public int sample_rate, minimum_samples, maximum_samples, fft_size, window_size, hop_size;

        public static MNNDccrnConfig Read(string directory) =>
            JsonUtility.FromJson<MNNDccrnConfig>(File.ReadAllText(Path.Combine(directory, FileName)))
            ?? throw new InvalidDataException("Invalid Mayavoz DCCRN profile.");
    }
}
