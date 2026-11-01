using System;
using System.IO;
using UnityEngine;
namespace MNN.Unity
{
    [Serializable]
    public sealed class MNNGtcrnConfig
    {
        public const string FileName = "mnn_gtcrn.json";
        public string repository = MNNGtcrnModel.Repository;
        public int sample_rate = 16000, fft_size = 512, hop_size = 256, frequency_bins = 257;
        public string graph_cpu = MNNGtcrnModel.GraphCpu;
        public string graph_accelerator = MNNGtcrnModel.GraphAccelerator;
        public static MNNGtcrnConfig Read(string directory) =>
            JsonUtility.FromJson<MNNGtcrnConfig>(File.ReadAllText(Path.Combine(directory, FileName)))
            ?? throw new InvalidDataException("Invalid GTCRN profile.");
    }
}
