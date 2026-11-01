using System;
using System.IO;
using UnityEngine;

namespace MNN.Unity
{
    [Serializable]
    public sealed class MNNFrcrnSeConfig
    {
        public const string FileName = "mnn_frcrn_se.json";
        public string repository = "manyeyes/frcrn-se-16k-onnx";
        public int sample_rate = 16000;
        public string input = "inputs";
        public string output = "outputs";
        public int fft_size = 640;
        public int hop_size = 320;
        public int frequency_bins = 321;
        public string graph_fp16;
        public string graph_fp32 = "frcrn_se_16k_fp32.mnn";
        public static MNNFrcrnSeConfig Read(string directory) =>
            JsonUtility.FromJson<MNNFrcrnSeConfig>(File.ReadAllText(Path.Combine(directory, FileName)))
            ?? throw new InvalidDataException("Invalid FRCRN profile.");
    }
}
