using System;
using System.IO;
using UnityEngine;

namespace MNN.Unity
{
    [Serializable]
    public sealed class MNNAvConvTasNetConfig
    {
        public const string FileName = "mnn_av_convtasnet.json";
        public string repository, revision, source_revision, visual_graph, audio_graph;
        public string visual_input, visual_output, audio_input, audio_output, normalization;
        public int sample_rate, frames_per_second, face_size, visual_features, minimum_samples, maximum_samples;
        public bool causal;

        public static MNNAvConvTasNetConfig Read(string directory) =>
            JsonUtility.FromJson<MNNAvConvTasNetConfig>(File.ReadAllText(Path.Combine(directory, FileName)))
            ?? throw new InvalidDataException("Invalid AV-ConvTasNet profile.");
    }
}
