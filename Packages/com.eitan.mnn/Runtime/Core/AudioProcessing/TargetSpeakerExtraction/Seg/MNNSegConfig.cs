using System;
using System.IO;
using UnityEngine;

namespace MNN.Unity
{
    [Serializable]
    public sealed class MNNSegConfig
    {
        public const string FileName = "mnn_seg.json";
        public string repository, revision, source_revision, graph;
        public string audio_input, gesture_input, audio_output, normalization;
        public int sample_rate, gesture_fps, gesture_features, minimum_samples, maximum_samples;
        public bool causal;

        public static MNNSegConfig Read(string directory) =>
            JsonUtility.FromJson<MNNSegConfig>(File.ReadAllText(Path.Combine(directory, FileName)))
            ?? throw new InvalidDataException("Invalid SEG profile.");
    }
}
