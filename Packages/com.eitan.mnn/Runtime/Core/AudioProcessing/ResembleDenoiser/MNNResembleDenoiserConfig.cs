using System;
using System.IO;
using UnityEngine;

namespace MNN.Unity
{
    [Serializable]
    public sealed class MNNResembleDenoiserConfig
    {
        public const string FileName = "mnn_resemble_denoiser.json";
        public string repository, revision, source_revision, graph, input, output, coefficient_sha256;
        public int sample_rate, fft_size, hop_size, right_padding, minimum_samples, maximum_samples;
        public float[] analysis_window;
        public bool causal;

        public static MNNResembleDenoiserConfig Read(string directory) =>
            JsonUtility.FromJson<MNNResembleDenoiserConfig>(File.ReadAllText(Path.Combine(directory, FileName)))
            ?? throw new InvalidDataException("Invalid Resemble Denoiser profile.");
    }
}
