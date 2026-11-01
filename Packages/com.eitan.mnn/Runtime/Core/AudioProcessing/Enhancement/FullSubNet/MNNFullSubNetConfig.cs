using System;
using System.IO;
using UnityEngine;

namespace MNN.Unity
{
    [Serializable]
    public sealed class MNNFullSubNetConfig
    {
        public const string FileName = "mnn_fullsubnet.json";
        public string repository = "Audio-WestlakeU/FullSubNet";
        public string source_revision;
        public string graph = "fullsubnet_fp32.mnn";
        public string fullband_graph = "fullsubnet_fullband_32_fp32.mnn";
        public string subband_graph = "fullsubnet_subband_32_fp32.mnn";
        public string normalization = "offline_laplace_norm";
        public int recurrent_block_frames = 32;
        public int sample_rate = 16000, fft_size = 512, hop_size = 256, look_ahead = 2;
        public static MNNFullSubNetConfig Read(string directory) =>
            JsonUtility.FromJson<MNNFullSubNetConfig>(File.ReadAllText(Path.Combine(directory, FileName)))
            ?? throw new InvalidDataException("Invalid FullSubNet profile.");
    }
}
