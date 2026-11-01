using System;
using System.IO;
using UnityEngine;

namespace MNN.Unity
{
    [Serializable]
    public sealed class MNNDeepFilterNet3Config
    {
        public const string FileName = "mnn_deepfilternet3.json";
        public string repository;
        public int sample_rate, fft_size, hop_size, erb_bins, df_bins;
        public string encoder, erb_decoder, df_decoder, encoder_fp32, erb_decoder_fp32, df_decoder_fp32, execution_mode;
        public int df_order, df_lookahead, conv_lookahead, max_audio_seconds;
        public float norm_alpha;
        public static MNNDeepFilterNet3Config Read(string directory) =>
            JsonUtility.FromJson<MNNDeepFilterNet3Config>(File.ReadAllText(Path.Combine(directory, FileName)))
            ?? throw new InvalidDataException("Invalid DeepFilterNet3 profile.");
    }
}
