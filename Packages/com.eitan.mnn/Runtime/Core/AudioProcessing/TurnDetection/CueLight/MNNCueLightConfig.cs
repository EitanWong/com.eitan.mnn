using System;
using System.IO;
using UnityEngine;

namespace MNN.Unity
{
    [Serializable]
    public sealed class MNNCueLightConfig
    {
        public const string FileName = "mnn_cuelight.json";
        public const string CoefficientsFile = "cuelight_coefficients.bin";
        public string repository, revision, checkpoint_sha256, cpu_graph, metal_graph, coefficient_sha256;
        public int sample_rate, frame_samples, maximum_samples, fft_size, window_samples, hop_samples,
            mel_bins, hidden_dimension, blocks, outputs, reset_silence_frames, reset_onset_frames, reset_replay_frames;

        internal static MNNCueLightConfig Read(string directory) =>
            JsonUtility.FromJson<MNNCueLightConfig>(File.ReadAllText(Path.Combine(directory, FileName)))
            ?? throw new InvalidDataException("Invalid CueLight profile.");
    }
}
