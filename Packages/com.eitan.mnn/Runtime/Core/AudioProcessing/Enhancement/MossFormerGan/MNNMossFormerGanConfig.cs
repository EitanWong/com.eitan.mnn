using System;
using System.IO;
using UnityEngine;

namespace MNN.Unity
{
    [Serializable]
    public sealed class MNNMossFormerGanConfig
    {
        public const string FileName = "mnn_mossformergan.json";
        public string repository, revision, source_revision, graph;
        public float[] analysis_window;
        public int sample_rate, fft_size, hop_size;
        public float power_compression;
        public string normalization;
        public static MNNMossFormerGanConfig Read(string directory) =>
            JsonUtility.FromJson<MNNMossFormerGanConfig>(File.ReadAllText(Path.Combine(directory, FileName)))
            ?? throw new InvalidDataException("Invalid MossFormerGAN profile.");
    }
}
