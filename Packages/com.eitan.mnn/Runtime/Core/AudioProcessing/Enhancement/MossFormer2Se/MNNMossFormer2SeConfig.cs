using System;
using System.IO;
using UnityEngine;

namespace MNN.Unity
{
    [Serializable]
    public sealed class MNNMossFormer2SeConfig
    {
        public const string FileName = "mnn_mossformer2_se.json";
        public string repository, revision, source_revision, graph, coefficient_sha256;
        public int sample_rate, fft_size, hop_size, mel_fft_size, mel_bins, dither_seed;
        public float dither;
        public float[] analysis_window, mel_filters;

        public static MNNMossFormer2SeConfig Read(string directory) =>
            JsonUtility.FromJson<MNNMossFormer2SeConfig>(File.ReadAllText(Path.Combine(directory, FileName)))
            ?? throw new InvalidDataException("Invalid MossFormer2 SE profile.");
    }
}
